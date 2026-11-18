using System;
using System.Collections.Generic;

namespace DungeonsNShops
{
    // Signed axes allow opposite effects to cancel before the timing multiplier is applied.
    [Serializable]
    public struct PotionEffects : IEquatable<PotionEffects>
    {
        public int health, energy, armor;
        public bool Empty => health == 0 && energy == 0 && armor == 0;
        public bool Equals(PotionEffects other) => health == other.health && energy == other.energy && armor == other.armor;

        public static PotionEffects Ingredient(Item item, bool ground) => item switch
        {
            Item.Herb => ground ? new() { health = 15 } : new() { energy = 10 },
            Item.Ore => ground ? new() { armor = 12 } : new() { health = -12 },
            Item.Ember => ground ? new() { energy = -10 } : new() { armor = -8 },
            _ => default
        };

        public static PotionEffects Mix(IEnumerable<ReagentPiece> pieces, int percent)
        {
            PotionEffects result = default;
            foreach (var piece in pieces)
            {
                var effect = Ingredient((Item)piece.item, piece.ground);
                result.health += effect.health;
                result.energy += effect.energy;
                result.armor += effect.armor;
            }
            int Scale(int value) => Math.Sign(value) * ((Math.Abs(value) * percent + 50) / 100);
            result.health = Scale(result.health);
            result.energy = Scale(result.energy);
            result.armor = Scale(result.armor);
            return result;
        }

        public static PotionEffects Bottle(int item, PotionEffects blend) => (Item)item switch
        {
            Item.Tonic => new() { health = 40 },
            Item.Focus => new() { energy = 20 },
            Item.Ward => new() { armor = 40 },
            Item.Brew => blend,
            _ => default
        };

        public static bool IsBottle(int item) => (Item)item is Item.Tonic or Item.Focus or Item.Ward or Item.Brew;
    }
}
