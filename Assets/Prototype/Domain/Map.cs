using UnityEngine;

namespace DungeonsNShops
{
    public static class Map
    {
        public static readonly Vector3 ShopSpawn = new(0, 0, -3);
        public static readonly Vector3 RaidSpawn = new(0, 0, 2);
        public static readonly Vector3 Cache = new(0, 0, 49);
        public static readonly Vector3 BossGate = new(0, 0, 58);

        public static Vector3 SitePosition(Site site) => site switch
        {
            Site.Shelf => new(-3, 0, 2),
            Site.Warehouse => new(-7, 0, 4),
            Site.Register => new(0, 0, 5),
            Site.Supplier => new(6, 0, 5),
            Site.Bank => new(8, 0, 0),
            Site.Bench => new(-7, 0, -4),
            Site.Gate => new(0, 0, 9),
            Site.Bed => new(-7, 0, -8),
            Site.Decor => new(6, 0, -8),
            Site.Sign => new(1.9f, 0, 4.65f),
            Site.Bin => new(-4.6f, 0, -0.8f),
            Site.Exit => new(0, 0, 0),
            _ => Vector3.zero
        };

        // x/z footprints are shared by authoritative movement and the blockout renderer.
        public static readonly Rect[] ShopObstacles =
        {
            new(-8.8f, 0.5f, 0.35f, 7), new(-5.6f, 0.5f, 0.35f, 7),
            new(-4, 1.7f, 2, 0.6f), new(-1.1f, 4.7f, 2.2f, 0.65f),
            new(-8, -4.35f, 2, 0.7f)
        };

        public static readonly Rect[] RaidObstacles =
        {
            new(-6, 13, 8, 1), new(-2, 23, 8, 1), new(-6, 34, 8, 1),
            new(-2, 44, 8, 1), new(-1.1f, 65, 2.2f, 3)
        };

        public static float Distance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        public static Site NearestSite(PlayerState player)
        {
            Site nearest = Site.None;
            float distance = 2.45f;
            int first = player.zone == Zone.Raid ? (int)Site.Exit : (int)Site.Shelf;
            int last = player.zone == Zone.Raid ? (int)Site.Exit : (int)Site.Decor;
            for (int i = first; i <= last; i++)
            {
                Site site = (Site)i;
                if (player.zone == Zone.Shop && site == Site.Exit)
                    continue;
                float candidate = Distance(player.position, SitePosition(site));
                if (candidate < distance)
                {
                    distance = candidate;
                    nearest = site;
                }
            }
            return nearest;
        }

        public static Vector3 Move(Zone zone, Vector3 from, Vector3 delta, bool bossUnlocked)
        {
            Vector3 x = from + new Vector3(delta.x, 0, 0);
            Vector3 z = (Clear(zone, x, bossUnlocked) ? x : from) + new Vector3(0, 0, delta.z);
            return Clear(zone, z, bossUnlocked) ? z : Clear(zone, x, bossUnlocked) ? x : from;
        }

        public static bool Visible(Vector3 from, Vector3 to, bool bossUnlocked)
        {
            float distance = Distance(from, to);
            for (float offset = 0.35f; offset < distance; offset += 0.35f)
                if (!Clear(Zone.Raid, Vector3.Lerp(from, to, offset / distance), bossUnlocked))
                    return false;
            return true;
        }

        private static bool Clear(Zone zone, Vector3 point, bool bossUnlocked)
        {
            float width = zone == Zone.Shop ? 9.55f : 5.6f;
            float min = zone == Zone.Shop ? -9.55f : -0.7f;
            float max = zone == Zone.Shop ? 9.55f : bossUnlocked ? 79.5f : 57.5f;
            if (Mathf.Abs(point.x) > width || point.z < min || point.z > max)
                return false;

            foreach (Rect box in zone == Zone.Shop ? ShopObstacles : RaidObstacles)
                if (point.x > box.xMin - 0.25f && point.x < box.xMax + 0.25f &&
                    point.z > box.yMin - 0.25f && point.z < box.yMax + 0.25f)
                    return false;
            return true;
        }
    }
}
