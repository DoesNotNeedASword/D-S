using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed class CustomerFactory
    {
        public Customer Create(int id) => new()
        {
            id = id,
            archetype = Random.Range(0, 2),
            phase = CustomerPhase.Walking,
            remaining = 3
        };
    }

    public sealed partial class GameWorld
    {
        public const int OrderSize = 6;
        private readonly CustomerFactory customers = new();

        public static int ShelfPrice(WorldState state, ShelfStock stock) =>
            stock.marketPrice ? state.market[stock.item] : stock.manualPrice;

        public static int OrderCost(WorldState state, int item) =>
            Mathf.Max(1, Mathf.RoundToInt(state.market[item] * OrderSize * 0.8f));

        private void RollMarket()
        {
            for (int item = 0; item < Catalog.Count; item++)
            {
                int value = Catalog.Get(item).BaseValue;
                State.market[item] = value + Random.Range(-value / 10, value / 10 + 1);
            }
        }

        private void AddCrate(int item, int count, int unitCost, int quality = 0)
        {
            PutOnRack(new Crate { id = State.nextId++, item = item, count = count, unitCost = unitCost, quality = quality });
        }

        private void PutOnRack(Crate crate)
        {
            crate.rackSlot = -1;
            State.warehouse.Add(crate);
            FillRack();
        }

        private void FillRack()
        {
            int taken = 0;
            foreach (Crate crate in State.warehouse)
            {
                if (crate.rackSlot < 0 || crate.rackSlot >= ShopTargets.RackSlots ||
                    (taken & (1 << crate.rackSlot)) != 0)
                    crate.rackSlot = -1;
                else
                    taken |= 1 << crate.rackSlot;
            }

            int slot = 0;
            foreach (Crate crate in State.warehouse)
            {
                if (crate.rackSlot >= 0) continue;
                while (slot < ShopTargets.RackSlots && (taken & (1 << slot)) != 0) slot++;
                if (slot == ShopTargets.RackSlots) break;
                crate.rackSlot = slot++;
            }
        }

        private string Trade(PlayerState player, Controls control, GameAction action)
        {
            if (player.ready || player.night == NightChoice.Sleeping || State.craft?.owner == player.id)
                return "error.busy";

            if (action.kind == ActionKind.SelectCrate)
            {
                if (!(player.carriedCrate?.id > 0)) return "error.stock";
                player.crateActive = !player.crateActive;
                return null;
            }

            if (player.zone != Zone.Shop) return "error.reach";
            ShopTarget target = default;
            if (action.kind is ActionKind.CarryCrate or ActionKind.TakeCrate or ActionKind.PlaceCrate or
                ActionKind.Stock or ActionKind.Open or ActionKind.Close or ActionKind.Sell)
                target = ShopTargets.Find(State, player.position, control.input.yaw, control.input.pitch, player.HoldingCrate, player.EyeHeight);

            switch (action.kind)
            {
                case ActionKind.CarryCrate:
                    if (player.carriedCrate?.id > 0) return "error.holding";
                    if (target.Kind != ShopTargetKind.Crate || target.Id != action.value) return "error.reach";
                    Crate picked = State.warehouse.Find(c => c.id == action.value);
                    State.warehouse.Remove(picked);
                    picked.rackSlot = -1;
                    player.carriedCrate = picked;
                    player.crateActive = true;
                    FillRack();
                    break;
                case ActionKind.TakeCrate:
                    bool own = player.HoldingCrate && player.carriedCrate.id == action.value;
                    if (!own && (target.Kind != ShopTargetKind.Crate || target.Id != action.value)) return "error.reach";
                    Crate source = own ? player.carriedCrate : State.warehouse.Find(c => c.id == action.value);
                    if (source == null || source.count <= 0) return "error.stock";
                    if (player.bag[source.item] >= 20) return "error.capacity";
                    source.count--;
                    player.bag[source.item]++;
                    if (!own && source.count == 0)
                    {
                        State.warehouse.Remove(source);
                        FillRack();
                    }
                    break;
                case ActionKind.PlaceCrate:
                    if (!player.HoldingCrate || player.carriedCrate.id != action.value ||
                        target.Kind != ShopTargetKind.Site || target.Site != Site.Warehouse) return "error.reach";
                    if (player.carriedCrate.count > 0) PutOnRack(player.carriedCrate);
                    player.carriedCrate = null;
                    player.crateActive = false;
                    break;
                case ActionKind.Stock:
                    if (!player.HoldingCrate || player.carriedCrate.id != action.value ||
                        target.Kind != ShopTargetKind.Site || target.Site != Site.Shelf) return "error.reach";
                    Crate crate = player.carriedCrate;
                    if (crate.count <= 0) return "error.stock";
                    int size = Catalog.Get(crate.item).ShelfSize;
                    int room = (ShopShelf.Capacity - State.shelf.Used) / size;
                    int count = Mathf.Min(room, action.extra == 1 ? 1 : crate.count);
                    if (count <= 0) return "error.capacity";
                    ShelfStock line = State.shelf.stock.Find(s => s.item == crate.item);
                    if (line == null)
                    {
                        line = new ShelfStock { item = crate.item, manualPrice = State.market[crate.item] };
                        State.shelf.stock.Add(line);
                    }
                    line.count += count;
                    crate.count -= count;
                    break;
                case ActionKind.Retrieve:
                    if (!At(player, Site.Shelf)) return "error.reach";
                    ShelfStock returned = State.shelf.stock.Find(s => s.item == action.value);
                    int reserved = State.customer?.phase == CustomerPhase.Checkout
                        ? State.customer.cart.Find(s => s.item == action.value)?.count ?? 0 : 0;
                    if (returned == null || returned.count <= reserved) return "error.stock";
                    AddCrate(returned.item, returned.count - reserved, 0);
                    returned.count = reserved;
                    break;
                case ActionKind.Price:
                    if (!At(player, Site.Shelf)) return "error.reach";
                    ShelfStock priced = State.shelf.stock.Find(s => s.item == action.value);
                    if (priced == null || action.extra == 0 || action.extra < -Money.MaxPrice || action.extra > Money.MaxPrice)
                        return "error.price";
                    priced.manualPrice = Mathf.Abs(action.extra);
                    priced.marketPrice = action.extra < 0;
                    break;
                case ActionKind.Open:
                    if (target.Site != Site.Sign) return "error.reach";
                    if (State.phase != DayPhase.Preparation || player.ep < balance.shiftEnergy) return "error.shift";
                    State.phase = DayPhase.Trading;
                    State.nextCustomer = 1;
                    WorkShift(player);
                    break;
                case ActionKind.Sell:
                    return CollectPayment(player, target, action);
                case ActionKind.Close:
                    if (target.Site != Site.Sign) return "error.reach";
                    if (State.phase == DayPhase.Night) return "error.night";
                    if (State.customer?.phase == CustomerPhase.Checkout && State.customer.collected != 0)
                        return "error.payment";
                    State.phase = DayPhase.Night;
                    State.customer = null;
                    Notify(player, "notice.night");
                    return null;
                case ActionKind.Order:
                    if (!At(player, Site.Supplier)) return "error.reach";
                    if (!Catalog.Valid(action.value)) return "error.stock";
                    if (State.deliveries.Count >= 8) return "error.capacity";
                    int cost = OrderCost(State, action.value);
                    if (State.money < cost) return "error.money";
                    State.money -= cost;
                    State.deliveries.Add(new PendingDelivery
                    {
                        morning = State.day + 1,
                        paid = cost,
                        crate = new Crate { id = State.nextId++, item = action.value, count = OrderSize, unitCost = State.market[action.value] }
                    });
                    Notify(player, "notice.order");
                    break;
                case ActionKind.Loan:
                    if (!At(player, Site.Bank)) return "error.reach";
                    if (State.debt > 50) return "error.debt";
                    State.debt += 50;
                    State.money += 45;
                    break;
                case ActionKind.Repay:
                    if (!At(player, Site.Bank)) return "error.reach";
                    int payment = Mathf.Min(20, Mathf.Min(State.money, State.debt));
                    State.money -= payment;
                    State.debt -= payment;
                    break;
                case ActionKind.Decor:
                    if (!At(player, Site.Decor)) return "error.reach";
                    State.decorated = !State.decorated;
                    break;
                default:
                    return "error.reach";
            }

            if (State.phase == DayPhase.Night && player.night == NightChoice.Free)
                player.night = NightChoice.Working;
            return null;
        }

        private string CollectPayment(PlayerState player, ShopTarget target, GameAction action)
        {
            Customer customer = State.customer;
            if (target.Kind != ShopTargetKind.Payment || target.Id != action.value || target.Index != action.extra ||
                customer?.phase != CustomerPhase.Checkout) return "error.customer";
            if (customer.collected == 0 && !player.workedShift && player.ep < balance.shiftEnergy) return "error.shift";

            if (player.ep >= balance.shiftEnergy) WorkShift(player);
            customer.collected |= 1 << target.Index;
            if (customer.collected != (1 << customer.payment.Length) - 1) return null;

            foreach (PurchaseLine line in customer.cart)
                State.shelf.stock.Find(s => s.item == line.item).count -= line.count;
            State.money += customer.price;
            Leave(customer);
            Notify(player, "notice.sold");
            return null;
        }

        private void WorkShift(PlayerState player)
        {
            if (player.workedShift) return;
            player.ep -= balance.shiftEnergy;
            player.workedShift = true;
        }

        private void TickCustomer(float delta)
        {
            if (State.phase != DayPhase.Trading) return;
            if (State.customer == null)
            {
                State.nextCustomer -= delta;
                if (State.nextCustomer <= 0)
                {
                    State.customer = customers.Create(State.nextId++);
                    Dirty = true;
                }
                return;
            }

            Customer customer = State.customer;
            if (customer.phase == CustomerPhase.Checkout && customer.collected != 0) return;
            customer.remaining -= delta;
            if (customer.remaining > 0) return;
            Dirty = true;
            switch (customer.phase)
            {
                case CustomerPhase.Walking:
                    customer.phase = CustomerPhase.Choosing;
                    customer.remaining = 2;
                    break;
                case CustomerPhase.Choosing:
                    if (BuildCart(customer))
                    {
                        customer.phase = CustomerPhase.Checkout;
                        customer.remaining = 55;
                        customer.payment = Money.Stacks(customer.price);
                        break;
                    }
                    foreach (PlayerState player in State.players.Where(p => p.online && p.zone == Zone.Shop))
                        Notify(player, State.shelf.Used == 0 ? "notice.empty" : "notice.expensive");
                    Leave(customer);
                    break;
                case CustomerPhase.Checkout:
                    Leave(customer);
                    break;
                case CustomerPhase.Leaving:
                    State.customer = null;
                    State.nextCustomer = balance.customerInterval;
                    break;
            }
        }

        private bool BuildCart(Customer customer)
        {
            var stock = State.shelf.stock;
            int limit = 2 + customer.archetype;
            int offset = Random.Range(0, stock.Count);
            for (int i = 0; i < stock.Count && customer.cart.Count < limit; i++)
            {
                ShelfStock line = stock[(offset + i) % stock.Count];
                if (line.count <= 0) continue;
                int price = ShelfPrice(State, line);
                if (price > Money.MaxPrice - customer.price) continue;
                float ratio = price / (float)State.market[line.item];
                float chance = Mathf.Clamp01((customer.archetype == 0 ? 1.32f : 1.65f) -
                    balance.demandElasticity * Mathf.Max(0, ratio - 1));
                if (Random.value >= chance) continue;
                customer.cart.Add(new PurchaseLine { item = line.item, count = 1, unitPrice = price });
                customer.price += price;
            }

            // With one product available a customer can still buy a small batch.
            if (customer.cart.Count == 1)
            {
                PurchaseLine line = customer.cart[0];
                int available = stock.Find(s => s.item == line.item).count;
                int extra = Mathf.Min(available - 1, limit - 1, (Money.MaxPrice - customer.price) / line.unitPrice);
                line.count += extra;
                customer.price += extra * line.unitPrice;
            }

            return customer.cart.Count > 0;
        }

        private static void Leave(Customer customer)
        {
            customer.phase = CustomerPhase.Leaving;
            customer.remaining = 3;
        }
    }
}
