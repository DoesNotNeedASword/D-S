using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DungeonsNShops
{
    internal enum PacketKind { Hello, Command, Movement, Snapshot, Rejected }

    [Serializable]
    internal sealed class SessionPacket
    {
        public int version = WorldState.CurrentVersion;
        public PacketKind kind;
        public string player;
        public GameAction action;
        public MoveInput movement;
        public WorldState state;
    }

    internal sealed class GameSession : IDisposable
    {
        public const int Port = 28731;
        private const float TickStep = 1f / 30f;
        private const string Owner = "owner";
        private readonly ISessionTransport transport;
        private readonly GameWorld authority;
        private readonly WorldSave save;
        private readonly Dictionary<int, string> guests = new();
        private readonly Dictionary<int, float> peerTimers = new();
        private float snapshotIn, saveIn, simulationTime, connectTimeout = 12;
        private int server;
        private long operation;
        private bool disposed;
        public string LocalId { get; }
        public WorldState State { get; private set; }
        public string Status { get; private set; }
        public string Warning { get; private set; }
        public bool Failed { get; private set; }

        private GameSession(string id, ISessionTransport transport, GameWorld world, WorldSave save)
        {
            this.transport = transport;
            authority = world;
            this.save = save;
            LocalId = id;
            State = world?.State;
            if (world != null)
            {
                world.Connect(id, true);
                operation = LocalPlayer.lastOperation;
            }
        }

        public PlayerState LocalPlayer => State?.players.Find(p => p.id == LocalId);

        public static GameSession Host(Balance balance, WorldSave save, ISessionTransport transport = null)
        {
            try
            {
                GameWorld world = new(balance, save.Load());
                transport?.Listen(Port);
                return new GameSession(Owner, transport, world, save);
            }
            catch
            {
                transport?.Dispose();
                throw;
            }
        }

        public static GameSession Join(string address, string identity, ISessionTransport transport)
        {
            try
            {
                transport.Connect(address, Port);
                return new GameSession(identity, transport, null, null) { Status = "network.connecting" };
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        public void Move(MoveInput input)
        {
            if (authority != null)
                authority.ReceiveMove(LocalId, input);
            else if (LocalPlayer != null)
                Send(server, new SessionPacket { kind = PacketKind.Movement, movement = input });
        }

        public void Command(ActionKind kind, int value = 0, int extra = 0, int[] ingredients = null)
        {
            if (LocalPlayer == null) return;
            GameAction action = new() { kind = kind, value = value, extra = extra, ingredients = ingredients, operation = ++operation };
            if (authority != null)
                authority.Execute(LocalId, action);
            else
                Send(server, new SessionPacket { kind = PacketKind.Command, action = action });
        }

        public void Tick(float delta)
        {
            if (transport != null)
                for (int i = 0; i < 192 && transport.Poll(out TransportEvent message); i++)
                    Handle(message);

            if (authority == null)
            {
                connectTimeout -= delta;
                if (!Failed && connectTimeout <= 0)
                    Fail(State == null ? "network.timeout" : "network.disconnected");
                return;
            }

            // Closing a stalled peer releases craft locks and keeps dawn available.
            if (peerTimers.Count > 0)
            {
                foreach (int peer in new List<int>(peerTimers.Keys))
                {
                    peerTimers[peer] -= delta;
                    if (peerTimers[peer] <= 0)
                    {
                        peerTimers.Remove(peer);
                        transport.Close(peer);
                    }
                }
            }

            simulationTime += delta;
            while (simulationTime >= TickStep)
            {
                authority.Tick(TickStep);
                simulationTime -= TickStep;
            }
            snapshotIn -= delta;
            if (guests.Count > 0 && snapshotIn <= 0)
            {
                snapshotIn = 0.08f;
                foreach (int peer in guests.Keys)
                    Snapshot(peer);
            }

            saveIn -= delta;
            if (authority.Dirty && saveIn <= 0)
                Persist();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            transport?.Dispose();
            if (authority != null && authority.Dirty)
                Persist();
        }

        private void Persist()
        {
            saveIn = 3;
            try
            {
                save.Write(State);
                authority.MarkSaved();
                Warning = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Warning = "network.saveFailed";
                Debug.LogWarning(ex.Message);
            }
        }

        private void Handle(TransportEvent message)
        {
            switch (message.Kind)
            {
                case TransportEventKind.Connected:
                    if (authority == null)
                    {
                        server = message.Peer;
                        Send(server, new SessionPacket { kind = PacketKind.Hello, player = LocalId });
                    }
                    else peerTimers[message.Peer] = 10;
                    return;
                case TransportEventKind.Closed:
                    if (authority == null)
                    {
                        if (!Failed) Fail("network.disconnected");
                    }
                    else
                    {
                        peerTimers.Remove(message.Peer);
                        if (guests.Remove(message.Peer, out string id)) authority.Disconnect(id);
                    }
                    return;
                case TransportEventKind.Failed:
                    if (authority == null) Fail("network.timeout");
                    else Warning = "network.listenFailed";
                    return;
            }

            SessionPacket packet;
            try { packet = JsonUtility.FromJson<SessionPacket>(message.Message); }
            catch (ArgumentException)
            {
                transport.Close(message.Peer);
                return;
            }

            if (packet == null || packet.version != WorldState.CurrentVersion) { transport.Close(message.Peer); return; }
            if (authority == null)
            {
                if (packet.kind == PacketKind.Rejected) Fail("network.full");
                if (packet.kind != PacketKind.Snapshot || packet.state == null) return;
                State = packet.state;
                if (LocalPlayer == null) { Fail("network.disconnected"); return; }
                operation = Math.Max(operation, LocalPlayer.lastOperation);
                connectTimeout = 12;
                Status = null;
                return;
            }

            if (!guests.TryGetValue(message.Peer, out string player))
            {
                if (packet.kind != PacketKind.Hello || !Guid.TryParseExact(packet.player, "N", out _) ||
                    !authority.Connect(packet.player, false))
                {
                    Send(message.Peer, new SessionPacket { kind = PacketKind.Rejected });
                    peerTimers[message.Peer] = 1;
                    return;
                }

                guests.Add(message.Peer, packet.player);
                peerTimers[message.Peer] = 12;
                Snapshot(message.Peer);
                return;
            }

            peerTimers[message.Peer] = 12;
            if (packet.kind == PacketKind.Command) authority.Execute(player, packet.action);
            if (packet.kind == PacketKind.Movement) authority.ReceiveMove(player, packet.movement);
        }

        private void Snapshot(int peer) => Send(peer, new SessionPacket { kind = PacketKind.Snapshot, state = State });
        private void Send(int peer, SessionPacket packet) => transport.Send(peer, JsonUtility.ToJson(packet));

        private void Fail(string key)
        {
            Failed = true;
            Status = key;
        }
    }
}
