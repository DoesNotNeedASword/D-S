using System;
using System.IO;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed class WorldSave
    {
        [Serializable] private sealed class LegacySave
        {
            public LegacyShelf shelf = null;
            public LegacyPlayer[] players = null;
        }
        [Serializable] private sealed class LegacyPlayer
        {
            public int armor = -1, offhand = -1, selected;
            public int[] bag, haul, hotbar, equipment;
        }
        [Serializable] private sealed class LegacyShelf
        {
            public int item = -1, count = 0, manualPrice = 14;
            public bool marketPrice = true;
        }

        private readonly string path;

        public WorldSave(string slot, string directory = null)
        {
            directory ??= Path.Combine(Application.persistentDataPath, "Prototype");
            path = Path.Combine(directory, slot + ".json");
        }

        public WorldState Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            return Read(path) ?? Read(path + ".bak") ?? throw new IOException("No readable prototype save.");
        }

        public void Write(WorldState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(state));
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
        }

        private static WorldState Read(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                return Decode(File.ReadAllText(file));
            }
            catch (Exception ex) when (ex is IOException or ArgumentException) { return null; }
        }

        internal static WorldState Decode(string json)
        {
            WorldState state = JsonUtility.FromJson<WorldState>(json);
            if (state == null || state.day < 1 || state.shelf == null ||
                state.market == null || state.players == null || state.warehouse == null)
                return null;
            if (state.version == WorldState.CurrentVersion)
                return state.market.Length == Catalog.Count ? state : null;
            if (state.version == 5 && state.market.Length == 14)
            {
                Array.Resize(ref state.market, Catalog.Count);
                state.market[(int)Item.Brew] = Catalog.Get((int)Item.Brew).BaseValue;
                foreach (var player in state.players) player.armorPower = player.ward > 0 ? 40 : 0;
                state.craft = null;
                state.version = WorldState.CurrentVersion;
                return state;
            }
            if (state.version is < 1 or > 4 || state.market.Length != (state.version == 4 ? 14 : state.version == 3 ? 13 : 10)) return null;
            LegacySave legacy = JsonUtility.FromJson<LegacySave>(json);
            if (state.version == 1) UpgradeShelf(state, legacy?.shelf);

            int previousCount = state.market.Length;
            Array.Resize(ref state.market, Catalog.Count);
            for (int i = previousCount; i < Catalog.Count; i++) state.market[i] = Catalog.Get(i).BaseValue;
            for (int i = 0; i < state.players.Count; i++)
            {
                PlayerState player = state.players[i];
                var old = legacy.players[i];
                player.inventory = new();
                player.quickSlots = InventoryLayout.StartingSlots;
                player.selected = 0;
                player.armorPower = player.ward > 0 ? 40 : 0;
                var bag = old.bag ?? Array.Empty<int>();
                void Equip(int item, int cell)
                {
                    if (item < 0 || item >= bag.Length || bag[item] <= 0 || InventoryLayout.At(player, cell) != null) return;
                    player.inventory.Add(new InventoryStack { item = item, count = 1, cell = cell });
                    bag[item]--;
                }
                int wielded = old.hotbar != null && old.selected >= 0 && old.selected < old.hotbar.Length
                    ? old.hotbar[old.selected] : old.selected;
                int weapon = Catalog.Valid(wielded) && Catalog.Get(wielded).Damage > 0 ? wielded : (int)Item.Blade;
                Equip(weapon, InventoryLayout.HotbarStart);
                if (old.offhand == (int)Item.Shield && !InventoryLayout.TwoHands(player))
                    Equip(old.offhand, InventoryLayout.HotbarStart + 1);
                for (int slot = 0; slot < InventoryLayout.ArmorSize; slot++)
                {
                    int armor = old.equipment != null && slot < old.equipment.Length ? old.equipment[slot] : old.armor;
                    if (Catalog.ArmorPosition(armor) == slot) Equip(armor, InventoryLayout.ArmorStart + slot);
                }
                for (int item = 0; item < bag.Length; item++)
                {
                    int pending = old.haul != null && item < old.haul.Length ? old.haul[item] : 0;
                    int total = bag[item] + pending;
                    int packed = 0;
                    while (packed < total && InventoryLayout.Add(player, item, 1, packed >= bag[item])) packed++;
                    if (packed < total)
                        state.warehouse.Add(new Crate { id = state.nextId++, item = item, count = total - packed });
                }
                player.crateActive = player.carriedCrate?.id > 0;
            }
            state.craft = null;
            state.version = WorldState.CurrentVersion;
            return state;
        }

        private static void UpgradeShelf(WorldState state, LegacyShelf old)
        {
            state.shelf = new ShopShelf();
            if (old != null && Catalog.Valid(old.item))
            {
                int fits = Mathf.Min(old.count, ShopShelf.Capacity / Catalog.Get(old.item).ShelfSize);
                state.shelf.stock.Add(new ShelfStock
                {
                    item = old.item, count = fits, manualPrice = Mathf.Max(1, old.manualPrice), marketPrice = old.marketPrice
                });
                if (old.count > fits)
                    state.warehouse.Add(new Crate { id = state.nextId++, item = old.item, count = old.count - fits });
            }
            foreach (Crate crate in state.warehouse) crate.rackSlot = -1;
            // Version 1 quotes did not reserve stock or collect any payment.
            state.customer = null;
            state.craft = null;
        }
    }
}
