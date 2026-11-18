using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonsNShops
{
    internal enum TransportEventKind { Connected, Message, Closed, Failed }

    internal readonly struct TransportEvent
    {
        public readonly int Peer;
        public readonly TransportEventKind Kind;
        public readonly string Message;

        public TransportEvent(int peer, TransportEventKind kind, string message = null)
        {
            Peer = peer;
            Kind = kind;
            Message = message;
        }
    }

    // A Steam transport can supply the same ordered, reliable messages and peer lifecycle.
    internal interface ISessionTransport : IDisposable
    {
        void Listen(int port);
        void Connect(string address, int port);
        bool Poll(out TransportEvent message);
        void Send(int peer, string message);
        void Close(int peer);
    }

    internal sealed class TcpTransport : ISessionTransport
    {
        private const int MaxFrame = 128 * 1024;
        private readonly ConcurrentQueue<TransportEvent> inbox = new();
        private readonly ConcurrentDictionary<int, Link> links = new();
        private readonly CancellationTokenSource lifetime = new();
        private TcpListener listener;
        private TcpClient connecting;
        private int nextPeer;

        public void Listen(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(4);
            _ = AcceptLoop(lifetime.Token);
        }

        public void Connect(string address, int port)
        {
            connecting = new TcpClient();
            _ = Dial(connecting, address, port, lifetime.Token);
        }

        public bool Poll(out TransportEvent message) => inbox.TryDequeue(out message);

        public void Send(int peer, string message)
        {
            if (links.TryGetValue(peer, out Link link) && !link.Queue(message))
                Close(peer);
        }

        public void Close(int peer)
        {
            if (!links.TryRemove(peer, out Link link)) return;
            link.Dispose();
            inbox.Enqueue(new TransportEvent(peer, TransportEventKind.Closed));
        }

        public void Dispose()
        {
            lifetime.Cancel();
            listener?.Stop();
            connecting?.Close();
            foreach (int id in links.Keys)
                Close(id);
            lifetime.Dispose();
        }

        private async Task AcceptLoop(CancellationToken stop)
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (links.Count >= 4) client.Dispose();
                    else Attach(client, stop);
                }
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                if (!stop.IsCancellationRequested)
                    inbox.Enqueue(new TransportEvent(0, TransportEventKind.Failed));
            }
        }

        private async Task Dial(TcpClient client, string address, int port, CancellationToken stop)
        {
            try
            {
                await client.ConnectAsync(address, port).ConfigureAwait(false);
                Attach(client, stop);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or ArgumentException)
            {
                if (!stop.IsCancellationRequested)
                    inbox.Enqueue(new TransportEvent(0, TransportEventKind.Failed));
            }
        }

        private void Attach(TcpClient client, CancellationToken stop)
        {
            if (stop.IsCancellationRequested)
            {
                client.Dispose();
                return;
            }

            client.NoDelay = true;
            int id = Interlocked.Increment(ref nextPeer);
            Link link = new(client, id, inbox, Close);
            links.TryAdd(id, link);
            if (stop.IsCancellationRequested)
            {
                Close(id);
                return;
            }

            inbox.Enqueue(new TransportEvent(id, TransportEventKind.Connected));
            link.Start();
        }

        private sealed class Link : IDisposable
        {
            private readonly TcpClient client;
            private readonly NetworkStream stream;
            private readonly int id;
            private readonly ConcurrentQueue<TransportEvent> inbox;
            private readonly Action<int> close;
            private readonly ConcurrentQueue<byte[]> output = new();
            private readonly SemaphoreSlim signal = new(0);
            private readonly CancellationTokenSource lifetime = new();

            public Link(TcpClient client, int id, ConcurrentQueue<TransportEvent> inbox, Action<int> close)
            {
                this.client = client;
                this.id = id;
                this.inbox = inbox;
                this.close = close;
                stream = client.GetStream();
            }

            public void Start()
            {
                CancellationToken stop = lifetime.Token;
                _ = ReadLoop(stop);
                _ = WriteLoop(stop);
            }

            public bool Queue(string value)
            {
                if (lifetime.IsCancellationRequested || output.Count >= 48) return false;
                byte[] body = Encoding.UTF8.GetBytes(value);
                if (body.Length > MaxFrame) return false;
                output.Enqueue(body);
                signal.Release();
                return true;
            }

            public void Dispose()
            {
                lifetime.Cancel();
                client.Dispose();
            }

            private async Task ReadLoop(CancellationToken stop)
            {
                byte[] header = new byte[4];
                try
                {
                    while (!stop.IsCancellationRequested && await ReadAll(header, stop).ConfigureAwait(false))
                    {
                        int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
                        if (size <= 0 || size > MaxFrame || inbox.Count >= 384) break;
                        byte[] body = new byte[size];
                        if (!await ReadAll(body, stop).ConfigureAwait(false)) break;
                        inbox.Enqueue(new TransportEvent(id, TransportEventKind.Message, Encoding.UTF8.GetString(body)));
                    }
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                finally { close(id); }
            }

            private async Task<bool> ReadAll(byte[] buffer, CancellationToken stop)
            {
                int offset = 0;
                while (offset < buffer.Length)
                {
                    int count = await stream.ReadAsync(buffer, offset, buffer.Length - offset, stop).ConfigureAwait(false);
                    if (count == 0) return false;
                    offset += count;
                }
                return true;
            }

            private async Task WriteLoop(CancellationToken stop)
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        await signal.WaitAsync(stop).ConfigureAwait(false);
                        if (!output.TryDequeue(out byte[] body)) continue;
                        byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(body.Length));
                        await stream.WriteAsync(header, 0, header.Length, stop).ConfigureAwait(false);
                        await stream.WriteAsync(body, 0, body.Length, stop).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                finally { close(id); }
            }
        }
    }
}
