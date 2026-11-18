using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DungeonsNShops
{
    public sealed class PrototypeGame : MonoBehaviour
    {
        [SerializeField] private Balance balance = new();
        private readonly QuietInput quiet = new();
        private InputSettings settings;
        private IPlayerInput gameplay;
        private Localization language;
        private PrototypeUi ui;
        private Blockout view;
        private GameSession session;
        private ScreenMode screen;
        private Bind? rebinding;
        private bool rebindArmed;
        private string address, identity, menuError;
        private float sendIn, drawIn, toastTime;
        private int noticeVersion, observedDay;
        private string toast;
        private bool observedReady;
        private Vector2 aim;
        private int stockingCrate;
        private float stockHold;
        private const float StockHoldSeconds = 0.42f;
        private Zone observedZone;
        private AudioSource sound;
        private AudioClip positive, negative;

        private void Awake()
        {
            Application.runInBackground = true;
            language = new Localization();
            settings = new InputSettings();
            gameplay = new GameplayInput(settings);
            address = PlayerPrefs.GetString("prototype.address", "127.0.0.1");
            identity = PlayerPrefs.GetString("prototype.identity", "");
            if (!Guid.TryParseExact(identity, "N", out _))
            {
                identity = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("prototype.identity", identity);
                PlayerPrefs.Save();
            }

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            view = new Blockout(transform, Camera.main);
            view.Relabel(language);
            ui = new PrototypeUi(transform, language, balance, Command,
                ingredients => session?.Command(ActionKind.BeginCraft, ingredients: ingredients));
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            positive = MakeCue("Confirm", 610);
            negative = MakeCue("Reject", 225);
            Show(ScreenMode.Menu);
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.10f);
            session?.Tick(delta);

            if (session?.Failed == true)
            {
                menuError = session.Status;
                EndSession();
                Show(ScreenMode.Party);
            }
            else if (screen == ScreenMode.Connecting && session?.LocalPlayer != null)
                StartPlaying();

            Keyboard keyboard = Keyboard.current;
            if (!HandleRebind(keyboard) && keyboard?.escapeKey.wasPressedThisFrame == true) Escape();

            PlayerState player = session?.LocalPlayer;
            if (player == null) return;
            WorldState world = session.State;
            SyncTransition(player, world);

            IPlayerInput input = screen == ScreenMode.Play && !player.ready ? gameplay : quiet;
            MoveInput motion = input.Sample();
            if (input == gameplay)
                aim = new Vector2(motion.yaw, motion.pitch);
            else
            {
                motion.yaw = aim.x;
                motion.pitch = aim.y;
            }

            sendIn -= delta;
            if (sendIn <= 0 || motion.jump || input.Attack)
            {
                sendIn = 0.04f;
                session.Move(motion);
            }

            ShopTarget target = screen == ScreenMode.Play && player.zone == Zone.Shop && !player.ready
                ? ShopTargets.Find(world, player.position, aim.x, aim.y, player.HoldingCrate, player.EyeHeight) : default;
            if (screen == ScreenMode.Play)
            {
                if (player.night != NightChoice.Sleeping && keyboard?.fKey.wasPressedThisFrame == true && world.phase == DayPhase.Night)
                    Command(ActionKind.Ready);
                int slot = input.Slot;
                if (slot >= 0) Command(ActionKind.Select, slot);
                if (input.Pressed(Bind.Crate)) Command(ActionKind.SelectCrate);
                if (input.Attack && player.zone == Zone.Raid) Command(ActionKind.Strike);
                if (input.Pressed(Bind.Use))
                {
                    if (player.zone == Zone.Shop && (player.HoldingCrate || target.Kind == ShopTargetKind.Crate))
                    {
                        session.Move(motion);
                        Command(ActionKind.TakeCrate, player.HoldingCrate ? player.carriedCrate.id : target.Id);
                    }
                    else Command(ActionKind.Use);
                }
                if (input.Pressed(Bind.Inventory)) Show(ScreenMode.Inventory);
                else
                {
                    if (input.Pressed(Bind.Interact))
                    {
                        if (player.HoldingCrate && target.Site == Site.Shelf)
                        {
                            stockingCrate = player.carriedCrate.id;
                            stockHold = 0;
                        }
                        else
                        {
                            session.Move(motion);
                            Interact(world, player, target);
                        }
                    }

                    if (stockingCrate != 0)
                    {
                        if (!player.HoldingCrate || stockingCrate != player.carriedCrate.id || target.Site != Site.Shelf)
                            stockingCrate = 0;
                        else
                        {
                            stockHold += delta;
                            bool all = stockHold >= StockHoldSeconds;
                            if (all || !input.Held(Bind.Interact))
                            {
                                session.Move(motion);
                                Command(ActionKind.Stock, stockingCrate, all ? 0 : 1);
                                stockingCrate = 0;
                            }
                        }
                    }
                }
            }
            else if (screen == ScreenMode.Inventory)
            {
                int slot = gameplay.Slot;
                if (keyboard?[settings.Get(Bind.Inventory)].wasPressedThisFrame == true) Show(ScreenMode.Play);
                else if (slot >= 0) ui.AssignHovered(slot);
            }

            view.Render(world, player, aim, delta, screen == ScreenMode.Play);
            if (screen == ScreenMode.Craft && world.craft?.owner == player.id)
                view.CraftPointer((action, value) => Command(action, world.craft.id, value));
            if (player.noticeVersion != noticeVersion)
            {
                noticeVersion = player.noticeVersion;
                toast = player.notice;
                toastTime = 4.5f;
                if (!string.IsNullOrEmpty(toast))
                    sound.PlayOneShot(toast.StartsWith("error.", StringComparison.Ordinal) ? negative : positive, 0.22f);
            }

            toastTime -= delta;
            drawIn -= delta;
            if (drawIn > 0) return;
            drawIn = 0.08f;
            ui.DrawHud(world, player, target, settings, session.Warning ?? (toastTime > 0 ? toast : null));
            ui.DrawInteraction(world, player, () => Show(ScreenMode.Play));
        }

        private void FixedUpdate() => view?.FixedTick();

        private void Interact(WorldState world, PlayerState player, ShopTarget target)
        {
            if (player.zone == Zone.Raid)
            {
                int loot = GameWorld.NearbyLoot(player, world.raid);
                if (loot != -2) Command(ActionKind.Loot, loot);
                else if (Map.NearestSite(player) == Site.Exit) Command(ActionKind.Exit);
                return;
            }

            if (target.Kind == ShopTargetKind.Crate)
                Command(ActionKind.CarryCrate, target.Id);
            else if (target.Kind == ShopTargetKind.Payment)
                Command(ActionKind.Sell, target.Id, target.Index);
            else switch (target.Site)
            {
                case Site.Warehouse:
                    if (player.HoldingCrate) Command(ActionKind.PlaceCrate, player.carriedCrate.id);
                    break;
                case Site.Sign:
                    Command(world.phase == DayPhase.Preparation && player.ep >= balance.shiftEnergy ? ActionKind.Open : ActionKind.Close);
                    break;
                case Site.Gate: Command(ActionKind.Enter); break;
                case Site.Bed: Command(ActionKind.Sleep); break;
                case Site.Decor: Command(ActionKind.Decor); break;
                case Site.Shelf:
                case Site.Supplier:
                case Site.Bank:
                case Site.Bench:
                    Show(ScreenMode.Site, target.Site);
                    break;
            }
        }

        private void SyncTransition(PlayerState player, WorldState world)
        {
            if (player.zone != observedZone || world.day != observedDay || player.ready != observedReady)
            {
                observedZone = player.zone;
                observedDay = world.day;
                observedReady = player.ready;
                Orient(player);
                if (screen is ScreenMode.Site or ScreenMode.Inventory or ScreenMode.Craft)
                    Show(ScreenMode.Play);
            }

            bool crafting = world.craft?.id > 0 && world.craft.owner == player.id;
            if (crafting && screen != ScreenMode.Craft) Show(ScreenMode.Craft);
            if (!crafting && screen == ScreenMode.Craft) Show(ScreenMode.Play);
        }

        private void Escape()
        {
            switch (screen)
            {
                case ScreenMode.Play: Show(ScreenMode.Pause); break;
                case ScreenMode.Site:
                case ScreenMode.Inventory:
                case ScreenMode.Pause: Show(ScreenMode.Play); break;
                case ScreenMode.Craft:
                    if (session.State.craft != null) Command(ActionKind.CancelCraft, session.State.craft.id);
                    break;
                case ScreenMode.Settings: Show(session == null ? ScreenMode.Menu : ScreenMode.Pause); break;
                case ScreenMode.Party: Show(ScreenMode.Menu); break;
                case ScreenMode.Connecting:
                    EndSession();
                    Show(ScreenMode.Party);
                    break;
            }
        }

        private bool HandleRebind(Keyboard keyboard)
        {
            if (screen != ScreenMode.Settings || rebinding == null || keyboard == null) return false;
            if (!rebindArmed)
            {
                rebindArmed = !keyboard.anyKey.isPressed;
                return true;
            }

            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                if (key.keyCode != Key.Escape && !GameplayInput.Rebindable(key.keyCode)) continue;
                if (key.keyCode != Key.Escape) settings.Set(rebinding.Value, key.keyCode);
                rebinding = null;
                Show(ScreenMode.Settings);
                break;
            }

            return true;
        }

        private void Show(ScreenMode next, Site site = Site.None)
        {
            screen = next;
            stockingCrate = 0;
            drawIn = 0;
            if (next != ScreenMode.Settings)
            {
                PlayerPrefs.Save();
                rebinding = null;
            }

            bool locked = next == ScreenMode.Play;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (next != ScreenMode.Craft) view.ReleaseDrag();
            ui.Show(next, site);
            switch (next)
            {
                case ScreenMode.Menu:
                    ui.Menu(menuError, () => Host(false), () => { menuError = null; Show(ScreenMode.Party); },
                        () => Show(ScreenMode.Settings), Application.Quit);
                    break;
                case ScreenMode.Party:
                    ui.Party(address, menuError, () => Host(true), Join, () => Show(ScreenMode.Menu));
                    break;
                case ScreenMode.Connecting:
                    ui.Connecting(() => { EndSession(); Show(ScreenMode.Party); });
                    break;
                case ScreenMode.Pause:
                    ui.Pause(() => Show(ScreenMode.Play), () => Show(ScreenMode.Settings),
                        () => { EndSession(); Show(ScreenMode.Menu); });
                    break;
                case ScreenMode.Settings:
                    ui.Settings(settings, rebinding, () =>
                    {
                        language.Toggle();
                        view.Relabel(language);
                        Show(ScreenMode.Settings);
                    }, value =>
                    {
                        rebinding = value;
                        rebindArmed = false;
                        Show(ScreenMode.Settings);
                    }, () =>
                    {
                        rebinding = null;
                        settings.Reset();
                        Show(ScreenMode.Settings);
                    }, () => Show(session == null ? ScreenMode.Menu : ScreenMode.Pause));
                    break;
            }
        }

        private void Host(bool party)
        {
            menuError = null;
            try
            {
                session = GameSession.Host(balance, new WorldSave(party ? "party" : "solo"), party ? CreateTransport() : null);
                StartPlaying();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SocketException)
            {
                Debug.LogWarning(ex.Message);
                menuError = "network.startFailed";
                Show(party ? ScreenMode.Party : ScreenMode.Menu);
            }
        }

        private void Join(string value)
        {
            address = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value;
            PlayerPrefs.SetString("prototype.address", address);
            menuError = null;
            session = GameSession.Join(address, identity, CreateTransport());
            Show(ScreenMode.Connecting);
        }

        private static ISessionTransport CreateTransport() => new TcpTransport();

        private void StartPlaying()
        {
            PlayerState player = session.LocalPlayer;
            noticeVersion = player.noticeVersion;
            toast = null;
            toastTime = 0;
            observedDay = session.State.day;
            observedZone = player.zone;
            observedReady = player.ready;
            Orient(player);
            Show(ScreenMode.Play);
        }

        private void Orient(PlayerState player)
        {
            aim = new Vector2(player.yaw, player.pitch);
            gameplay.Orient(aim.x, aim.y);
        }

        internal void SetGameplayInput(IPlayerInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            (gameplay as IDisposable)?.Dispose();
            gameplay = input;
            gameplay.Orient(aim.x, aim.y);
        }

        private void Command(ActionKind kind, int value = 0, int extra = 0) => session?.Command(kind, value, extra);

        private void EndSession()
        {
            session?.Dispose();
            session = null;
            view.Hide();
        }

        private static AudioClip MakeCue(string name, float frequency)
        {
            const int rate = 22050;
            float[] samples = new float[2205];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Min(1, i / 80f) * (1 - i / (float)samples.Length);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            session?.Dispose();
            view?.Dispose();
            ui?.Dispose();
            (gameplay as IDisposable)?.Dispose();
            if (positive != null) Destroy(positive);
            if (negative != null) Destroy(negative);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
