using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    [Serializable]
    public sealed class InventoryStack
    {
        public int item, count, cell, unsecured;
    }

    internal static class InventoryLayout
    {
        public const int Columns = 8, Rows = 5, BackpackSize = Columns * Rows;
        public const int StartingSlots = 4, HotbarSize = 8, ArmorSize = 4;
        public const int HotbarStart = BackpackSize, ArmorStart = HotbarStart + HotbarSize;

        public static Vector2Int Size(int item) => (Item)item switch
        {
            Item.Blade => new(1, 4), Item.Greatblade => new(2, 5), Item.Shield => new(2, 3),
            Item.Coat => new(3, 2), Item.Leggings => new(2, 3), Item.Helm or Item.Trap => new(2, 2),
            Item.Boots => new(2, 1), Item.Herb => new(1, 2), _ => Vector2Int.one
        };

        private static int Limit(int item) => Catalog.IsIngredient(item) ? 8 :
            Catalog.Get(item).Damage > 0 || item == (int)Item.Shield || Catalog.ArmorPosition(item) >= 0 ? 1 : 4;

        public static int BeltPrice(PlayerState player) => (player.quickSlots - StartingSlots + 1) * Money.Silver;

        private static RectInt Footprint(int item, int cell)
        {
            var size = Size(item);
            return new RectInt(cell % Columns, cell / Columns, size.x, size.y);
        }

        public static InventoryStack At(PlayerState player, int cell)
        {
            if (cell < 0) return null;
            if (cell >= BackpackSize) return player.inventory.Find(s => s.cell == cell);
            var point = new Vector2Int(cell % Columns, cell / Columns);
            return player.inventory.Find(s => s.cell >= 0 && s.cell < BackpackSize && Footprint(s.item, s.cell).Contains(point));
        }

        public static bool TwoHands(PlayerState player) =>
            player.Quick(0) is { } weapon && Catalog.Get(weapon.item).TwoHanded;

        private static bool Fits(PlayerState player, int item, int cell, InventoryStack moving = null)
        {
            if (cell < 0 || cell >= BackpackSize) return false;
            RectInt area = Footprint(item, cell);
            if (area.xMax > Columns || area.yMax > Rows) return false;
            foreach (var other in player.inventory)
                if (other != moving && other.cell >= 0 && other.cell < BackpackSize && area.Overlaps(Footprint(other.item, other.cell)))
                    return false;
            return true;
        }

        private static int FreeCell(PlayerState player, int item)
        {
            for (int i = 0; i < BackpackSize; i++) if (Fits(player, item, i)) return i;
            return -1;
        }

        // Atomic for multi-unit pickups. Failed placement leaves both inventory and loot source intact.
        public static bool Add(PlayerState player, int item, int count, bool unsecured = false)
        {
            if (!Catalog.Valid(item) || count <= 0) return false;
            var changes = new List<(InventoryStack stack, int added)>();
            int limit = Limit(item);
            foreach (var stack in player.inventory)
            {
                if (stack.item != item || stack.count >= limit) continue;
                int added = Math.Min(count, limit - stack.count);
                stack.count += added;
                if (unsecured) stack.unsecured += added;
                changes.Add((stack, added));
                count -= added;
                if (count == 0) return true;
            }
            while (count > 0)
            {
                int cell = FreeCell(player, item);
                if (cell < 0)
                {
                    foreach (var change in changes)
                    {
                        change.stack.count -= change.added;
                        if (unsecured) change.stack.unsecured -= change.added;
                        if (change.stack.count == 0) player.inventory.Remove(change.stack);
                    }
                    return false;
                }
                int added = Math.Min(count, limit);
                var stack = new InventoryStack { item = item, count = added, cell = cell, unsecured = unsecured ? added : 0 };
                player.inventory.Add(stack);
                changes.Add((stack, added));
                count -= added;
            }
            return true;
        }

        public static void Consume(PlayerState player, InventoryStack stack, int count)
        {
            stack.count -= count;
            stack.unsecured = Math.Max(0, stack.unsecured - count);
            if (stack.count == 0) player.inventory.Remove(stack);
        }

        public static void Consume(PlayerState player, int item, int count)
        {
            for (int i = player.inventory.Count - 1; i >= 0 && count > 0; i--)
            {
                var stack = player.inventory[i];
                if (stack.item != item) continue;
                int used = Math.Min(count, stack.count);
                Consume(player, stack, used);
                count -= used;
            }
        }

        public static void EndRaid(PlayerState player, bool extracted)
        {
            foreach (var stack in player.inventory)
            {
                if (!extracted) stack.count -= stack.unsecured;
                stack.unsecured = 0;
            }
            player.inventory.RemoveAll(s => s.count == 0);
        }

        public static string Move(PlayerState player, int from, int to)
        {
            var source = At(player, from);
            if (source == null || to < 0 || to >= ArmorStart + ArmorSize) return "error.stock";
            if (source.cell == to) return null;
            if (to < BackpackSize)
            {
                if (!Fits(player, source.item, to, source)) return "error.capacity";
                source.cell = to;
                return null;
            }
            if (to >= ArmorStart)
            {
                if (Catalog.ArmorPosition(source.item) != to - ArmorStart) return "error.armorSlot";
            }
            else
            {
                int slot = to - HotbarStart;
                if (slot >= player.quickSlots) return "error.beltLocked";
                bool weapon = Catalog.Get(source.item).Damage > 0;
                if (slot == 0 && !weapon || slot == 1 && (TwoHands(player) ||
                    source.item != (int)Item.Shield && (!weapon || Catalog.Get(source.item).TwoHanded)) ||
                    slot >= 2 && (weapon || source.item == (int)Item.Shield || Catalog.ArmorPosition(source.item) >= 0))
                    return "error.hands";
            }

            var displaced = At(player, to);
            var shield = to == HotbarStart && Catalog.Get(source.item).TwoHanded ? player.Quick(1) : null;
            int origin = source.cell;
            source.cell = to;
            int displacedCell = displaced?.cell ?? -1;
            if (displaced != null) displaced.cell = -1;
            if (shield != null) shield.cell = -1;
            if (displaced != null) displaced.cell = FreeCell(player, displaced.item);
            if (shield != null) shield.cell = FreeCell(player, shield.item);
            if (displaced?.cell == -1 || shield?.cell == -1)
            {
                source.cell = origin;
                if (displaced != null) displaced.cell = displacedCell;
                if (shield != null) shield.cell = HotbarStart + 1;
                return "error.capacity";
            }
            return null;
        }

        public static void GiveStarterKit(PlayerState player)
        {
            player.inventory.Add(new InventoryStack { item = (int)Item.Blade, count = 1, cell = HotbarStart });
            player.inventory.Add(new InventoryStack { item = (int)Item.Shield, count = 1, cell = HotbarStart + 1 });
            player.inventory.Add(new InventoryStack { item = (int)Item.Tonic, count = 2, cell = HotbarStart + 2 });
            Add(player, (int)Item.Trap, 2);
        }
    }
}
