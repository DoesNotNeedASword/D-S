using UnityEngine;

namespace DungeonsNShops
{
    public enum ShopTargetKind { None, Site, Crate, Payment }

    public readonly struct ShopTarget
    {
        public readonly ShopTargetKind Kind;
        public readonly Site Site;
        public readonly int Id, Index;

        public ShopTarget(ShopTargetKind kind, Site site, int id = 0, int index = 0)
        {
            Kind = kind;
            Site = site;
            Id = id;
            Index = index;
        }
    }

    public static class ShopTargets
    {
        public const int RackSlots = 24;
        private const float Range = 3.1f;
        private static readonly Site[] Stations =
        {
            Site.Shelf, Site.Warehouse, Site.Register, Site.Supplier, Site.Bank,
            Site.Bench, Site.Gate, Site.Bed, Site.Decor, Site.Sign, Site.Bin
        };

        public static readonly Vector3 CrateSize = new(0.52f, 0.4f, 0.73f);
        public static readonly Vector3 SignSize = new(1.15f, 0.62f, 0.17f);
        public static readonly Vector3 CoinSize = new(0.16f, 0.16f, 0.16f);

        public static Vector3 CratePosition(int slot) =>
            new(slot % 2 == 0 ? -8.22f : -6.03f, 1.16f + slot / 12 * 0.54f, 1.15f + slot / 2 % 6 * 1.02f);

        public static Vector3 CoinPosition(int index) =>
            new(-0.94f + index % 10 * 0.209f, 1.02f, 4.8f + index / 10 * 0.17f);

        public static Vector3 RackPosition(int side, float height) => new(side == 0 ? -8.22f : -6.03f, height, 3.7f);

        public static Bounds Station(Site site)
        {
            Vector3 position = Map.SitePosition(site);
            return site switch
            {
                Site.Shelf => new Bounds(position + Vector3.up * 0.61f, new Vector3(2.05f, 1.22f, 0.7f)),
                Site.Register => new Bounds(position + Vector3.up * 0.45f, new Vector3(2.2f, 0.9f, 0.65f)),
                Site.Bin => new Bounds(position + Vector3.up * 0.52f, new Vector3(0.84f, 1.04f, 0.84f)),
                Site.Sign => new Bounds(position + Vector3.up * 1.42f, SignSize),
                Site.Bench => new Bounds(position + Vector3.up * 0.55f, new Vector3(2, 1.1f, 0.9f)),
                _ => new Bounds(position + Vector3.up * 0.9f, new Vector3(1.3f, 1.8f, 1.15f))
            };
        }

        public static ShopTarget Find(WorldState world, Vector3 position, float yaw, float pitch, bool holdingCrate, float eye = 1.62f)
        {
            Ray ray = new(position + Vector3.up * eye, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
            float nearest = Range;
            ShopTarget target = default;

            void Consider(Bounds bounds, ShopTarget candidate)
            {
                if (!bounds.IntersectRay(ray, out float distance) || distance >= nearest) return;
                nearest = distance;
                target = candidate;
            }

            foreach (Crate crate in world.warehouse)
            {
                if (holdingCrate || crate.rackSlot < 0 || crate.rackSlot >= RackSlots) continue;
                Consider(new Bounds(CratePosition(crate.rackSlot), CrateSize),
                    new ShopTarget(ShopTargetKind.Crate, Site.Warehouse, crate.id));
            }

            if (!holdingCrate)
                foreach (Crate crate in world.dropped)
                {
                    var rotation = Quaternion.Euler(crate.angles);
                    Ray localRay = new(Quaternion.Inverse(rotation) * (ray.origin - crate.position),
                        Quaternion.Inverse(rotation) * ray.direction);
                    if (new Bounds(Vector3.zero, CrateSize).IntersectRay(localRay, out float distance) && distance < nearest)
                    {
                        nearest = distance;
                        target = new ShopTarget(ShopTargetKind.Crate, Site.None, crate.id);
                    }
                }

            Customer buyer = world.customer;
            if (buyer?.phase == CustomerPhase.Checkout && buyer.payment != null)
                for (int i = 0; i < buyer.payment.Length; i++)
                    if ((buyer.collected & (1 << i)) == 0)
                        Consider(new Bounds(CoinPosition(i), CoinSize), new ShopTarget(ShopTargetKind.Payment, Site.Register, buyer.id, i));

            foreach (Site site in Stations)
            {
                if (site == Site.Warehouse)
                {
                    // Aim at either physical rack. Leave the aisle clear for individual crates.
                    ShopTarget rack = new(ShopTargetKind.Site, site);
                    for (int side = 0; side < 2; side++)
                    {
                        Consider(new Bounds(RackPosition(side, 0.48f), new Vector3(0.66f, 0.96f, 6.1f)), rack);
                        Consider(new Bounds(RackPosition(side, 1.47f), new Vector3(0.66f, 0.10f, 6.1f)), rack);
                    }
                    continue;
                }

                Consider(Station(site), new ShopTarget(ShopTargetKind.Site, site));
            }

            return target;
        }
    }
}
