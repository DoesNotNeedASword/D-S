using System;

namespace DungeonsNShops
{
    internal static class InventoryLayout
    {
        public const int BackpackSize = 30;
        public const int HotbarSize = 10;
        public const int ArmorSize = 4;
        public const int HotbarStart = BackpackSize;
        public const int ArmorStart = HotbarStart + HotbarSize;

        public static int[] Empty(int count)
        {
            int[] slots = new int[count];
            Array.Fill(slots, -1);
            return slots;
        }

        public static void Migrate(PlayerState player, int coat)
        {
            Array.Resize(ref player.bag, Catalog.Count);
            Array.Resize(ref player.haul, Catalog.Count);
            player.backpack = Empty(BackpackSize);
            player.hotbar = Empty(HotbarSize);
            player.equipment = Empty(ArmorSize);
            for (int i = 0; i < HotbarSize; i++)
                if (player.bag[i] > 0) player.hotbar[i] = i;
            if (coat == (int)Item.Coat && player.bag[coat] > 0)
                player.equipment[(int)ArmorSlot.Torso] = coat;
            Sync(player);
        }

        public static void Sync(PlayerState player)
        {
            int seen = 0;
            for (int i = 0; i < BackpackSize; i++)
            {
                int item = player.backpack[i];
                if (!Owned(player, item) || (seen & (1 << item)) != 0)
                    player.backpack[i] = -1;
                else seen |= 1 << item;
            }
            for (int i = 0; i < Catalog.Count; i++)
                if (Owned(player, i) && (seen & (1 << i)) == 0)
                    player.backpack[Array.IndexOf(player.backpack, -1)] = i;
            for (int i = 0; i < HotbarSize; i++)
                if (!Owned(player, player.hotbar[i])) player.hotbar[i] = -1;
            for (int i = 0; i < ArmorSize; i++)
                if (!Owned(player, player.equipment[i]) || Catalog.ArmorPosition(player.equipment[i]) != i)
                    player.equipment[i] = -1;
        }

        private static bool Owned(PlayerState player, int item) => Catalog.Valid(item) && player.bag[item] > 0;

        public static string Move(PlayerState player, int from, int to)
        {
            if (from < 0 || to < 0 || from >= ArmorStart + ArmorSize || to >= ArmorStart + ArmorSize)
                return "error.stock";
            if (from == to) return null;

            int[] source = Cells(player, from, out int a);
            int[] destination = Cells(player, to, out int b);
            int item = source[a];
            if (!Owned(player, item)) return "error.stock";
            if (to >= ArmorStart)
            {
                if (Catalog.ArmorPosition(item) != b) return "error.armorSlot";
                destination[b] = item;
            }
            else if (to >= HotbarStart)
            {
                if (from >= ArmorStart) return "error.armorSlot";
                if (from >= HotbarStart) source[a] = destination[b];
                destination[b] = item;
            }
            else
            {
                if (from >= HotbarStart)
                {
                    source[a] = -1;
                    source = player.backpack;
                    a = Array.IndexOf(source, item);
                }
                if (a >= 0) (destination[b], source[a]) = (source[a], destination[b]);
            }

            if (player.Main >= 0 && Catalog.Get(player.Main).TwoHanded) player.offhand = -1;
            return null;
        }

        private static int[] Cells(PlayerState player, int cell, out int index)
        {
            index = cell;
            if (cell < HotbarStart) return player.backpack;
            if (cell < ArmorStart)
            {
                index -= HotbarStart;
                return player.hotbar;
            }
            index -= ArmorStart;
            return player.equipment;
        }
    }
}
