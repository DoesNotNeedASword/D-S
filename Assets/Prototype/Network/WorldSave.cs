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
        [Serializable] private sealed class LegacyPlayer { public int armor = -1; }
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
            if (state.version is < 1 or > 3 || state.market.Length != (state.version == 3 ? 13 : 10)) return null;
            LegacySave legacy = state.version < 3 ? JsonUtility.FromJson<LegacySave>(json) : null;
            if (state.version == 1) UpgradeShelf(state, legacy?.shelf);

            int previousCount = state.market.Length;
            Array.Resize(ref state.market, Catalog.Count);
            for (int i = previousCount; i < Catalog.Count; i++) state.market[i] = Catalog.Get(i).BaseValue;
            for (int i = 0; i < state.players.Count; i++)
            {
                PlayerState player = state.players[i];
                if (state.version < 3)
                    InventoryLayout.Migrate(player, legacy?.players != null && i < legacy.players.Length ? legacy.players[i].armor : -1);
                else
                {
                    Array.Resize(ref player.bag, Catalog.Count);
                    Array.Resize(ref player.haul, Catalog.Count);
                }
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
