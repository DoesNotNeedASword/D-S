using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    // All commands and simulation steps execute on one authority thread.
    public sealed partial class GameWorld
    {
        private sealed class Controls
        {
            public MoveInput input = new();
            public float expires, verticalSpeed, regenDelay, strikeDelay;
            public bool jumpQueued, blocking;
        }

        private readonly Dictionary<string, Controls> controls = new();
        private readonly Balance balance;
        public WorldState State { get; }
        public bool Dirty { get; private set; }

        public GameWorld(Balance balance, WorldState saved = null)
        {
            this.balance = balance;
            State = saved ?? new WorldState();
            if (saved == null)
            {
                RollMarket();
                AddCrate((int)Item.Tonic, 8, 0);
                AddCrate((int)Item.Herb, 5, 0);
                AddCrate((int)Item.Ore, 2, 0);
            }

            foreach (PlayerState player in State.players)
            {
                player.online = false;
                player.ready = false;
            }

            State.craft = null;
            FillRack();
            if (State.customer?.id <= 0) State.customer = null;
            if (State.raid?.id <= 0) State.raid = null;
        }

        public bool Connect(string id, bool owner)
        {
            if (string.IsNullOrEmpty(id) || controls.ContainsKey(id) || controls.Count >= 2)
                return false;

            PlayerState player = State.players.Find(p => p.id == id);
            if (player == null)
            {
                if (State.players.Count >= 2)
                    return false;

                player = new PlayerState { id = id, position = Map.ShopSpawn + (owner ? Vector3.zero : Vector3.right) };
                InventoryLayout.GiveStarterKit(player);
                State.players.Add(player);
            }

            player.online = true;
            player.ready = false;
            controls.Add(id, new Controls());
            Dirty = true;
            return true;
        }

        public void Disconnect(string id)
        {
            if (!controls.Remove(id))
                return;

            PlayerState player = State.players.Find(p => p.id == id);
            if (player == null)
                return;

            player.online = false;
            player.ready = false;
            if (State.craft?.owner == id)
                State.craft = null;

            // A disconnected explorer can reconnect to the same raid until dawn.
            Dirty = true;
            if (State.phase == DayPhase.Night)
                TryDawn();
        }

        public void ReceiveMove(string id, MoveInput input)
        {
            if (input == null || !controls.TryGetValue(id, out Controls control) ||
                !float.IsFinite(input.axes.x) || !float.IsFinite(input.axes.y) ||
                !float.IsFinite(input.yaw) || !float.IsFinite(input.pitch))
                return;

            control.input = input;
            control.expires = 0.45f;
            control.jumpQueued |= input.jump;
        }

        public void Execute(string id, GameAction action)
        {
            if (action == null || !controls.TryGetValue(id, out Controls control))
                return;

            PlayerState player = State.players.Find(p => p.id == id);
            if (action.operation <= player.lastOperation)
                return;

            player.lastOperation = action.operation;
            Dirty = true;
            string error = action.kind switch
            {
                ActionKind.Select or ActionKind.Offhand or ActionKind.Use or ActionKind.MoveInventory => Equipment(player, action),
                ActionKind.Enter or ActionKind.Exit or ActionKind.Strike or ActionKind.Loot => Expedition(player, control, action),
                ActionKind.BeginCraft or ActionKind.CraftZone or ActionKind.GrindIngredient or ActionKind.AddIngredient or
                    ActionKind.Stir or ActionKind.FinishCraft or ActionKind.CancelCraft => Craft(player, action),
                ActionKind.Sleep or ActionKind.Ready => Schedule(player, action.kind),
                _ => Trade(player, control, action)
            };
            if (error != null)
                Notify(player, error);
        }

        public void Tick(float delta)
        {
            foreach (PlayerState player in State.players)
                if (controls.TryGetValue(player.id, out Controls control))
                    Move(player, control, delta);

            TickCustomer(delta);
            TickCraft(delta);
            TickRaid(delta);
        }

        public void MarkSaved() => Dirty = false;

        private static void Notify(PlayerState player, string key)
        {
            player.notice = key;
            player.noticeVersion++;
        }

        private void Move(PlayerState player, Controls control, float delta)
        {
            MoveInput input = control.input;
            control.expires -= delta;
            control.regenDelay -= delta;
            control.strikeDelay -= delta;
            player.swing = Mathf.Max(0, player.swing - delta);
            player.ward = Mathf.Max(0, player.ward - delta);
            bool live = control.expires > 0 && !player.ready && player.night != NightChoice.Sleeping;
            bool canWalk = live && State.craft?.owner != player.id;
            Vector2 axes = canWalk ? Vector2.ClampMagnitude(input.axes, 1) : Vector2.zero;
            player.crouched = canWalk && input.crouch && player.position.y <= 0.001f;
            bool sprint = canWalk && !player.crouched && input.sprint && axes.sqrMagnitude > 0 && player.sp >= 1;
            control.blocking = live && input.block && player.HasShield && player.sp >= 1;
            player.guarding = control.blocking;
            if (sprint) Spend(player, control, 12 * delta);
            if (control.blocking) Spend(player, control, 6 * delta);
            if (canWalk && !player.crouched && control.jumpQueued && player.position.y <= 0.001f && player.sp >= 10)
            {
                Spend(player, control, 10);
                control.verticalSpeed = 5;
            }

            control.jumpQueued = false;
            if (live)
            {
                player.yaw = Mathf.Repeat(input.yaw, 360);
                player.pitch = Mathf.Clamp(input.pitch, -85, 85);
            }

            float speed = player.crouched ? 1.65f : control.blocking ? 2.3f : sprint ? 6.3f : 4.1f;
            Vector3 direction = Quaternion.Euler(0, player.yaw, 0) * new Vector3(axes.x, 0, axes.y);
            Vector3 pos = Map.Move(player.zone, player.position, direction * (speed * delta), State.raid?.bossUnlocked == true);
            control.verticalSpeed -= 15 * delta;
            pos.y = Mathf.Max(0, pos.y + control.verticalSpeed * delta);
            if (pos.y == 0) control.verticalSpeed = 0;
            if (pos != player.position || player.zone == Zone.Raid) Dirty = true;
            player.position = pos;
            if (control.regenDelay <= 0)
                player.sp = Mathf.Min(100, player.sp + 24 * delta);
        }

        private static void Spend(PlayerState player, Controls control, float amount)
        {
            player.sp = Mathf.Max(0, player.sp - amount);
            control.regenDelay = 0.65f;
        }

        private static bool At(PlayerState player, Site site)
        {
            Zone zone = site == Site.Exit ? Zone.Raid : Zone.Shop;
            return player.zone == zone && Map.Distance(player.position, Map.SitePosition(site)) <= 3.1f;
        }

        private string Schedule(PlayerState player, ActionKind kind)
        {
            if (State.phase != DayPhase.Night || player.zone != Zone.Shop || State.craft?.owner == player.id)
                return "error.night";
            if (kind == ActionKind.Sleep)
            {
                if (!At(player, Site.Bed)) return "error.reach";
                if (player.night != NightChoice.Free) return "error.sleep";
                player.night = NightChoice.Sleeping;
                player.ready = true;
            }
            else
            {
                if (player.night == NightChoice.Sleeping) return "error.sleep";
                player.ready = !player.ready;
            }

            TryDawn();
            return null;
        }

        private void TryDawn()
        {
            if (controls.Count > 0 && !State.players.Any(p => p.online && (p.zone == Zone.Raid || !p.ready)))
                Dawn();
        }

        private void Dawn()
        {
            State.day++;
            State.phase = DayPhase.Preparation;
            State.raid = null;
            State.customer = null;
            State.craft = null;
            RollMarket();

            for (int i = 0; i < State.deliveries.Count;)
            {
                PendingDelivery delivery = State.deliveries[i];
                if (delivery.morning > State.day)
                {
                    i++;
                    continue;
                }

                PutOnRack(delivery.crate);
                State.deliveries.RemoveAt(i);
            }

            State.lastTax = Mathf.Min(State.money, State.arrears + balance.dailyTax);
            State.arrears += balance.dailyTax - State.lastTax;
            State.money -= State.lastTax;
            State.lastPayment = Mathf.Min(State.money, Mathf.Min(State.debt, balance.loanPayment));
            State.money -= State.lastPayment;
            State.debt -= State.lastPayment;
            foreach (PlayerState player in State.players)
            {
                if (player.night == NightChoice.Sleeping)
                {
                    player.ep = Mathf.Min(100, player.ep + balance.sleepEnergy);
                    player.hp = 100;
                }

                if (player.zone == Zone.Raid)
                    ReturnToShop(player, false);
                player.night = NightChoice.Free;
                player.ready = false;
                player.workedShift = false;
                Notify(player, "notice.dawn");
            }
        }
    }
}
