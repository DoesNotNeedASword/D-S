using System.Collections.Generic;

namespace DungeonsNShops
{
    public static class Money
    {
        public const int Copper = 1;
        public const int Silver = 100;
        public const int Gold = 10000;
        public const int MaxPrice = 999999;
        public const int CoinsPerStack = 10;

        public static int Denomination(int copper) => copper >= Gold ? Gold : copper >= Silver ? Silver : Copper;

        public static int[] Stacks(int amount)
        {
            List<int> stacks = new();
            foreach (int denomination in new[] { Gold, Silver, Copper })
            {
                int coins = amount / denomination;
                amount %= denomination;
                while (coins > 0)
                {
                    int count = System.Math.Min(coins, CoinsPerStack);
                    stacks.Add(count * denomination);
                    coins -= count;
                }
            }

            return stacks.ToArray();
        }
    }
}
