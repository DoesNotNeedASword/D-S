using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    public sealed partial class GameWorld
    {
        private static readonly Vector3[] OreVeins =
        {
            new(-4.3f, 0, 46.5f), new(3.8f, 0, 48), new(-3.6f, 0, 52),
            new(3.7f, 0, 54), new(0, 0, 55)
        };

        public static Vector3 VeinPosition(int index) => OreVeins[index];
        public static int VeinCount => OreVeins.Length;

        private string Equipment(PlayerState player, GameAction action)
        {
            if (player.ready || player.night == NightChoice.Sleeping || State.craft?.owner == player.id)
                return "error.busy";

            switch (action.kind)
            {
                case ActionKind.Select:
                    if (action.value < 0 || action.value >= player.quickSlots) return "error.beltLocked";
                    if (action.value >= 2 && player.Quick(action.value) is { } quick &&
                        (PotionEffects.IsBottle(quick.item) || quick.item == (int)Item.Trap))
                        return UseItem(player, quick);
                    player.selected = action.value;
                    return null;
                case ActionKind.MoveInventory:
                    return InventoryLayout.Move(player, action.value, action.extra);
                case ActionKind.Use:
                    return UseItem(player, player.Quick(player.selected));
                case ActionKind.CoatWeapon:
                    var bottle = InventoryLayout.At(player, action.value);
                    var weapon = player.Quick(action.extra == 1 ? 1 : 0);
                    if (bottle == null || !PotionEffects.IsBottle(bottle.item) || weapon == null ||
                        Catalog.Get(weapon.item).Damage == 0) return "error.use";
                    var coating = PotionEffects.Bottle(bottle.item, bottle.formula);
                    if (coating.Empty || !weapon.coating.Empty) return "error.coating";
                    weapon.coating = coating;
                    InventoryLayout.Consume(player, bottle, 1);
                    return null;
                default: return "error.use";
            }
        }

        private string UseItem(PlayerState player, InventoryStack stack)
        {
            if (stack == null) return "error.use";
            if (stack.item == (int)Item.Trap)
            {
                if (player.zone != Zone.Raid || State.raid.traps.Count >= 12) return "error.trap";
                State.raid.traps.Add(player.position);
            }
            else
            {
                if (!PotionEffects.IsBottle(stack.item)) return "error.use";
                var effect = PotionEffects.Bottle(stack.item, stack.formula);
                if (effect.health > 0 && player.hp >= 100 && effect.energy == 0 && effect.armor == 0) return "error.fullHp";
                if (effect.energy > 0 && player.ep >= 100 && effect.health == 0 && effect.armor == 0) return "error.fullEp";
                player.hp = Mathf.Clamp(player.hp + effect.health, 0, 100);
                player.ep = Mathf.Clamp(player.ep + effect.energy, 0, 100);
                if (effect.armor != 0) { player.armorPower = effect.armor; player.ward = 18; }
            }
            InventoryLayout.Consume(player, stack, 1);
            if (player.hp <= 0) Defeat(player);
            return null;
        }

        private string Expedition(PlayerState player, Controls control, GameAction action)
        {
            if (player.ready || player.night == NightChoice.Sleeping || State.craft?.owner == player.id)
                return "error.busy";

            if (action.kind == ActionKind.Enter)
            {
                if (!At(player, Site.Gate)) return "error.reach";
                if (State.phase != DayPhase.Night) return "error.night";
                if (player.night != NightChoice.Raiding)
                {
                    if (player.ep < balance.raidEnergy) return "error.energy";
                    player.ep -= balance.raidEnergy;
                }

                State.raid ??= CreateRaid();
                player.night = NightChoice.Raiding;
                player.zone = Zone.Raid;
                player.crateActive = false;
                player.position = Map.RaidSpawn;
                player.yaw = player.pitch = 0;
                control.verticalSpeed = 0;
                control.expires = 0;
                Notify(player, "notice.raid");
                return null;
            }

            if (player.zone != Zone.Raid || State.raid == null) return "error.reach";
            switch (action.kind)
            {
                case ActionKind.Exit:
                    if (!At(player, Site.Exit)) return "error.reach";
                    ReturnToShop(player, true);
                    control.expires = 0;
                    control.verticalSpeed = 0;
                    Notify(player, "notice.return");
                    return null;
                case ActionKind.Strike:
                    Strike(player, control);
                    return null;
                case ActionKind.Loot:
                    return Loot(player, action.value);
                default:
                    return "error.reach";
            }
        }

        private RaidState CreateRaid()
        {
            return new RaidState
            {
                id = State.nextId++,
                gatherRemaining = balance.gatherSeconds,
                enemies = new[]
                {
                    Spawn(-3, 9, 0), Spawn(3.8f, 17, 1), Spawn(3, 21, 0),
                    Spawn(-3.8f, 29, 1), Spawn(-3.4f, 32, 0), Spawn(3.8f, 39, 0),
                    Spawn(-3.7f, 46, 1), Spawn(0, 72, 2)
                }
            };
        }

        private static Enemy Spawn(float x, float z, int kind)
        {
            float hp = kind == 2 ? 175 : kind == 1 ? 48 : 64;
            return new Enemy { position = new Vector3(x, 0, z), hp = hp, maxHp = hp, kind = kind, yaw = 180 };
        }

        private void ReturnToShop(PlayerState player, bool extract)
        {
            InventoryLayout.EndRaid(player, extract);
            player.zone = Zone.Shop;
            player.position = new Vector3(0, 0, 7.5f);
            player.yaw = 180;
            player.pitch = 0;
        }

        private void Strike(PlayerState player, Controls control)
        {
            if (control.strikeDelay > 0 || control.blocking || player.sp < 12) return;
            Spend(player, control, 12);
            control.strikeDelay = 0.64f;
            player.swing = 0.36f;
            RaidState raid = State.raid;
            Vector3 aim = Quaternion.Euler(player.pitch, player.yaw, 0) * Vector3.forward;
            Vector3 origin = player.position + Vector3.up * player.EyeHeight;
            Enemy nearest = null;
            float distance = 2.85f;
            foreach (Enemy enemy in raid.enemies)
            {
                if (enemy.phase == EnemyPhase.Defeated || (enemy.kind == 2 && !raid.bossUnlocked)) continue;
                Vector3 offset = enemy.position + Vector3.up - origin;
                if (offset.magnitude >= distance || Vector3.Dot(aim, offset.normalized) < 0.73f ||
                    !Map.Visible(player.position, enemy.position, raid.bossUnlocked)) continue;
                distance = offset.magnitude;
                nearest = enemy;
            }

            if (nearest == null) return;
            int damage = player.Main < 0 ? 7 : Mathf.Max(7, Catalog.Get(player.Main).Damage);
            var weapon = player.Quick(player.selected);
            var coating = weapon?.coating ?? default;
            if (weapon != null) weapon.coating = default;
            float factor = nearest.ward > 0 ? 1 - Mathf.Clamp(nearest.armorPower, -80, 80) / 100f : 1;
            nearest.hp = Mathf.Min(nearest.maxHp, nearest.hp + Mathf.Max(0, coating.health));
            Hurt(nearest, damage * factor + Mathf.Max(0, -coating.health));
            if (nearest.phase != EnemyPhase.Defeated)
            {
                if (coating.armor != 0) { nearest.armorPower = coating.armor; nearest.ward = 18; }
                if (coating.energy < 0) nearest.stun += -coating.energy * 0.08f;
                if (coating.energy > 0) nearest.remaining = Mathf.Max(0, nearest.remaining - coating.energy * 0.08f);
            }
        }

        private string Loot(PlayerState player, int index)
        {
            RaidState raid = State.raid;
            int item;
            int count;
            if (index == -1)
            {
                if (raid.cacheTaken || Map.Distance(player.position, Map.Cache) > 2.8f) return "error.loot";
                item = (int)Item.Coat;
                count = 1;
            }
            else if (index >= raid.enemies.Length && index < raid.enemies.Length + VeinCount)
            {
                int vein = index - raid.enemies.Length;
                if ((raid.veinsTaken & (1 << vein)) != 0 ||
                    Map.Distance(player.position, VeinPosition(vein)) > 2.8f) return "error.loot";
                item = (int)Item.Ore;
                count = 1;
            }
            else
            {
                if (index < 0 || index >= raid.enemies.Length) return "error.loot";
                Enemy enemy = raid.enemies[index];
                if (enemy.phase != EnemyPhase.Defeated || enemy.looted ||
                    Map.Distance(player.position, enemy.position) > 2.8f) return "error.loot";
                item = enemy.kind == 2 ? (int)Item.Ember : enemy.kind == 1 ? (int)Item.Herb : (int)Item.Ore;
                count = enemy.kind == 2 ? 2 : 1;
            }

            if (!InventoryLayout.Add(player, item, count, unsecured: true)) return "error.capacity";
            if (index == -1) raid.cacheTaken = true;
            else if (index >= raid.enemies.Length)
                raid.veinsTaken |= 1 << (index - raid.enemies.Length);
            else
                raid.enemies[index].looted = true;
            Notify(player, "notice.loot");
            return null;
        }

        public static int NearbyLoot(PlayerState player, RaidState raid)
        {
            if (player.zone != Zone.Raid || raid == null) return -2;
            int nearest = -2;
            float radius = 2.6f;
            if (!raid.cacheTaken && Map.Distance(player.position, Map.Cache) < radius)
            {
                nearest = -1;
                radius = Map.Distance(player.position, Map.Cache);
            }

            for (int i = 0; i < raid.enemies.Length; i++)
            {
                Enemy enemy = raid.enemies[i];
                float distance = Map.Distance(player.position, enemy.position);
                if (enemy.phase != EnemyPhase.Defeated || enemy.looted || distance >= radius) continue;
                nearest = i;
                radius = distance;
            }

            for (int i = 0; i < VeinCount; i++)
            {
                float distance = Map.Distance(player.position, VeinPosition(i));
                if ((raid.veinsTaken & (1 << i)) != 0 || distance >= radius) continue;
                nearest = raid.enemies.Length + i;
                radius = distance;
            }

            return nearest;
        }

        private void TickRaid(float delta)
        {
            RaidState raid = State.raid;
            if (raid == null || !State.players.Any(p => p.online && p.zone == Zone.Raid)) return;

            if (!raid.bossUnlocked && raid.enemies.Take(raid.enemies.Length - 1).All(e => e.phase == EnemyPhase.Defeated))
            {
                raid.gatherRemaining = Mathf.Max(0, raid.gatherRemaining - delta);
                if (raid.gatherRemaining <= 0 || raid.veinsTaken == (1 << VeinCount) - 1)
                    raid.bossUnlocked = true;
            }

            foreach (Enemy enemy in raid.enemies)
            {
                if (enemy.phase == EnemyPhase.Defeated || (enemy.kind == 2 && !raid.bossUnlocked)) continue;
                TickEnemy(raid, enemy, delta);
            }
        }

        private void TickEnemy(RaidState raid, Enemy enemy, float delta)
        {
            enemy.ward = Mathf.Max(0, enemy.ward - delta);
            enemy.swing = Mathf.Max(0, enemy.swing - delta);
            for (int i = raid.traps.Count - 1; i >= 0; i--)
            {
                if (Map.Distance(enemy.position, raid.traps[i]) >= 1.25f) continue;
                raid.traps.RemoveAt(i);
                Hurt(enemy, enemy.kind == 2 ? 52 : 42);
                enemy.stun = 2.4f;
                if (enemy.phase != EnemyPhase.Defeated) enemy.phase = EnemyPhase.Recovery;
                break;
            }

            if (enemy.phase == EnemyPhase.Defeated) return;
            enemy.stun -= delta;
            if (enemy.stun > 0) return;
            enemy.remaining -= delta;
            if (enemy.phase == EnemyPhase.Recovery && enemy.remaining > 0) return;
            if (enemy.phase == EnemyPhase.Windup)
            {
                if (enemy.remaining > 0) return;
                PlayerState victim = State.players.Find(p => p.id == enemy.target);
                if (victim != null && victim.online && victim.zone == Zone.Raid &&
                    Map.Distance(victim.position, enemy.position) <= AttackRange(enemy) + 0.4f &&
                    Map.Visible(enemy.position, victim.position, raid.bossUnlocked))
                    DamagePlayer(enemy, victim);
                enemy.phase = EnemyPhase.Recovery;
                enemy.swing = 0.36f;
                enemy.remaining = enemy.kind == 2 ? 1.25f : 1.7f;
                return;
            }

            PlayerState target = null;
            float nearest = enemy.kind == 2 ? 14 : 10;
            foreach (PlayerState player in State.players)
            {
                if (!player.online || player.zone != Zone.Raid) continue;
                float distance = Map.Distance(enemy.position, player.position);
                if (distance >= nearest || !CanDetect(enemy, player, distance) ||
                    !Map.Visible(enemy.position, player.position, raid.bossUnlocked)) continue;
                target = player;
                nearest = distance;
            }

            if (target == null)
            {
                enemy.phase = EnemyPhase.Idle;
                enemy.target = null;
                return;
            }

            Vector3 direction = target.position - enemy.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
                enemy.yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            enemy.target = target.id;
            if (nearest <= AttackRange(enemy))
            {
                enemy.phase = EnemyPhase.Windup;
                enemy.target = target.id;
                enemy.remaining = enemy.kind == 2 ? 1.45f : 0.95f;
                return;
            }

            enemy.phase = EnemyPhase.Pursuit;
            enemy.position = Map.Move(Zone.Raid, enemy.position, direction.normalized * (2.25f * delta), raid.bossUnlocked);
        }

        internal static bool CanDetect(Enemy enemy, PlayerState player, float distance)
        {
            if (!player.crouched || enemy.target == player.id || distance <= 2.4f) return true;
            if (distance > (enemy.kind == 2 ? 6f : 4.5f)) return false;
            Vector3 direction = player.position - enemy.position;
            direction.y = 0;
            Vector3 forward = Quaternion.Euler(0, enemy.yaw, 0) * Vector3.forward;
            return Vector3.Dot(forward, direction.normalized) >= 0.48f;
        }

        private static float AttackRange(Enemy enemy) => enemy.kind switch { 2 => 4.6f, 1 => 8, _ => 1.95f };

        private void Hurt(Enemy enemy, float damage)
        {
            enemy.hp = Mathf.Max(0, enemy.hp - damage);
            if (enemy.hp > 0) return;
            enemy.phase = EnemyPhase.Defeated;
            if (enemy.kind == 2) State.bossDefeated = true;
        }

        private void DamagePlayer(Enemy enemy, PlayerState player)
        {
            Controls control = controls[player.id];
            Vector3 forward = Quaternion.Euler(0, player.yaw, 0) * Vector3.forward;
            float damage = enemy.kind == 2 ? (player.HasCoat ? 11 : 35) : enemy.kind == 1 ? 13 : 16;
            if (player.ward > 0) damage *= 1 - Mathf.Clamp(player.armorPower, -80, 80) / 100f;
            foreach (var stack in player.inventory)
                if (stack.cell >= InventoryLayout.ArmorStart && stack.cell != InventoryLayout.ArmorStart + (int)ArmorSlot.Torso) damage *= 0.92f;
            if (control.blocking && control.input.block &&
                Vector3.Dot(forward, (enemy.position - player.position).normalized) > 0.15f)
            {
                damage *= 0.25f;
                Spend(player, control, 9);
            }

            player.hp = Mathf.Max(0, player.hp - damage);
            if (player.hp > 0) return;
            Defeat(player);
        }

        private void Defeat(PlayerState player)
        {
            ReturnToShop(player, false);
            controls[player.id].expires = 0;
            player.hp = 65;
            player.ep = Mathf.Max(0, player.ep - 5);
            Notify(player, "notice.defeated");
        }
    }
}
