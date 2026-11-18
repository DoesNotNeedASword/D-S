using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    public enum Item { Tonic, Herb, Blade, Shield, Coat, Trap, Greatblade, Ore, Ember, Focus, Boots, Leggings, Helm, Ward, Brew }
    public enum ArmorSlot { Feet, Legs, Torso, Head }
    public enum DayPhase { Preparation, Trading, Night }
    public enum NightChoice { Free, Working, Raiding, Sleeping }
    public enum Zone { Shop, Raid }
    public enum Site { None, Shelf, Warehouse, Register, Supplier, Bank, Bench, Gate, Bed, Exit, Decor, Sign, Bin }
    public enum CustomerPhase { Walking, Choosing, Checkout, Leaving }
    public enum CraftStep { Mortar, Cauldron, Stir, Ingredients, Grind }
    public enum ReagentStage { Tray, Mortar, Ground, Pot }
    public enum EnemyPhase { Idle, Pursuit, Windup, Recovery, Defeated }
    public enum ActionKind
    {
        Stock, TakeCrate, Retrieve, Price, Open, Sell, Close, Order, Loan, Repay,
        BeginCraft, CraftZone, Stir, FinishCraft, CancelCraft, Enter, Exit, Sleep, Ready, Select,
        Offhand, Use, Strike, Loot, Decor, CarryCrate, PlaceCrate, SelectCrate, MoveInventory, GrindIngredient, AddIngredient, ExpandBelt, DropCrate, DiscardCrate, CoatWeapon
    }

    public readonly struct ItemDefinition
    {
        public readonly int BaseValue;
        public readonly int Damage;
        public readonly bool TwoHanded;
        public readonly int ShelfSize;

        public ItemDefinition(int value, int damage = 0, bool twoHanded = false, int shelfSize = 1)
        {
            BaseValue = value;
            Damage = damage;
            TwoHanded = twoHanded;
            ShelfSize = shelfSize;
        }
    }

    public static class Catalog
    {
        private static readonly ItemDefinition[] Items =
        {
            new(14), new(6), new(24, 24, shelfSize: 4), new(18, shelfSize: 3), new(25, shelfSize: 3),
            new(12, shelfSize: 2), new(42, 38, true, 6), new(10), new(36), new(24),
            new(16, shelfSize: 2), new(21, shelfSize: 3), new(19, shelfSize: 2), new(26), new(20)
        };

        public const int Count = 15;
        public static bool Valid(int item) => item >= 0 && item < Count;
        public static ItemDefinition Get(int item) => Items[item];
        public static bool IsIngredient(int item) => item is (int)Item.Herb or (int)Item.Ore or (int)Item.Ember;
        public static int ArmorPosition(int item) => (Item)item switch
        {
            Item.Boots => (int)ArmorSlot.Feet,
            Item.Leggings => (int)ArmorSlot.Legs,
            Item.Coat => (int)ArmorSlot.Torso,
            Item.Helm => (int)ArmorSlot.Head,
            _ => -1
        };
    }

    [Serializable]
    public sealed class Balance
    {
        [Range(0.1f, 4)] public float demandElasticity = 1.6f;
        [Min(0)] public int dailyTax = 4;
        [Min(0)] public int loanPayment = 8;
        [Range(1, 100)] public int shiftEnergy = 12;
        [Range(1, 100)] public int raidEnergy = 25;
        [Range(1, 100)] public int sleepEnergy = 60;
        [Min(1)] public float customerInterval = 9;
        [Min(1)] public float stirSeconds = 2;
        [Min(0)] public float gatherSeconds = 120;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public string id;
        public bool online;
        public Zone zone;
        public NightChoice night;
        public bool ready;
        public bool workedShift;
        public float hp = 100, sp = 100;
        public int ep = 100;
        public Vector3 position;
        public float yaw, pitch;
        public bool crouched, guarding;
        public float swing, ward;
        public int armorPower;
        public int selected;
        public int quickSlots = InventoryLayout.StartingSlots;
        public List<InventoryStack> inventory = new();
        public Crate carriedCrate;
        public bool crateActive;
        public long lastOperation;
        public string notice;
        public int noticeVersion;

        public bool HoldingCrate => carriedCrate?.id > 0;
        public InventoryStack Quick(int index) => inventory.Find(s => s.cell == InventoryLayout.HotbarStart + index);
        public int Count(int item)
        {
            int count = 0;
            foreach (var stack in inventory) if (stack.item == item) count += stack.count;
            return count;
        }
        public int Main => HoldingCrate ? -1 : Quick(selected)?.item ?? -1;
        public int Offhand => Quick(1)?.item ?? -1;
        public bool HasShield => !HoldingCrate && Offhand == (int)Item.Shield;
        public bool HasCoat => inventory.Exists(s => s.cell == InventoryLayout.ArmorStart + (int)ArmorSlot.Torso);
        public float EyeHeight => crouched ? 1.05f : 1.62f;
    }

    [Serializable]
    public sealed class Crate
    {
        public int id, item, count, unitCost, quality;
        public int rackSlot = -1;
        public PotionEffects formula;
        public Vector3 position, angles, velocity, spin;
    }

    [Serializable]
    public sealed class PendingDelivery
    {
        public Crate crate;
        public int morning, paid;
    }

    [Serializable]
    public sealed class ShopShelf
    {
        public const int Capacity = 12;
        public List<ShelfStock> stock = new();
        public int Used
        {
            get
            {
                int used = 0;
                foreach (ShelfStock line in stock) used += line.count * Catalog.Get(line.item).ShelfSize;
                return used;
            }
        }
    }

    [Serializable]
    public sealed class ShelfStock
    {
        public int item, count, manualPrice, batch;
        public PotionEffects formula;
        public int Key => batch > 0 ? Catalog.Count + batch : item;
        public bool marketPrice = true;
    }

    [Serializable]
    public sealed class PurchaseLine
    {
        public int item, count, unitPrice, batch;
        public int Key => batch > 0 ? Catalog.Count + batch : item;
    }

    [Serializable]
    public sealed class Customer
    {
        public int id, archetype, price, collected;
        public List<PurchaseLine> cart = new();
        public int[] payment;
        public CustomerPhase phase;
        public float remaining;
    }

    [Serializable]
    public sealed class CraftEvent
    {
        public CraftStep step;
        public int reagent;
        public float time;
    }

    [Serializable]
    public sealed class ReagentPiece
    {
        public int item, presses;
        public ReagentStage stage;
        public bool ground;
        public float nextPress;
    }

    [Serializable]
    public sealed class StationSession
    {
        public int id;
        public string owner;
        public CraftStep step = CraftStep.Ingredients;
        public float elapsed, stir;
        public float lastStirSample, boilStart;
        public int[] ingredients;
        public List<ReagentPiece> pieces = new();
        public System.Collections.Generic.List<CraftEvent> journal = new();
    }

    [Serializable]
    public sealed class Enemy
    {
        public Vector3 position;
        public float hp, maxHp, remaining, stun;
        public float yaw, swing, ward;
        public int armorPower;
        public int kind;
        public EnemyPhase phase;
        public string target;
        public bool looted;
    }

    [Serializable]
    public sealed class RaidState
    {
        public int id;
        public Enemy[] enemies;
        public bool cacheTaken, bossUnlocked;
        public int veinsTaken;
        public float gatherRemaining;
        public System.Collections.Generic.List<Vector3> traps = new();
    }

    [Serializable]
    public sealed class WorldState
    {
        public const int CurrentVersion = 6;
        public int version = CurrentVersion;
        public int day = 1, money = 120, debt = 50, arrears, lastTax, lastPayment, nextId = 1;
        public DayPhase phase;
        public bool decorated, bossDefeated;
        public int[] market = new int[Catalog.Count];
        public ShopShelf shelf = new();
        public List<Crate> dropped = new();
        public System.Collections.Generic.List<Crate> warehouse = new();
        public System.Collections.Generic.List<PendingDelivery> deliveries = new();
        public System.Collections.Generic.List<PlayerState> players = new();
        public Customer customer;
        public StationSession craft;
        public CraftEvent[] lastCraft;
        public int craftQuality;
        public RaidState raid;
        public float nextCustomer;
    }

    [Serializable]
    public sealed class GameAction
    {
        public ActionKind kind;
        public int value, extra;
        public int[] ingredients;
        public long operation;
    }

    [Serializable]
    public sealed class MoveInput
    {
        public Vector2 axes;
        public float yaw, pitch;
        public bool sprint, jump, block, crouch;
    }
}
