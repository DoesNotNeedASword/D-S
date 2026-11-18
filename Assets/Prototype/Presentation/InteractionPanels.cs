using System;
using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed partial class PrototypeUi
    {
        public void DrawInteraction(WorldState world, PlayerState player, Action close)
        {
            int stamp = mode switch
            {
                ScreenMode.Inventory => 0,
                ScreenMode.Craft => world.craft == null ? -1 : (int)world.craft.step,
                ScreenMode.Site => SiteStamp(world, player),
                _ => int.MinValue
            };
            if (stamp == int.MinValue) return;
            if (contentStamp != stamp)
            {
                contentStamp = stamp;
                float scrollPosition = scroll != null ? scroll.verticalNormalizedPosition : 1;
                scroll = null;
                UiFactory.Clear(panel);
                if (mode == ScreenMode.Inventory) Inventory(player, close);
                if (mode == ScreenMode.Site) SitePanel(world, player, close);
                if (mode == ScreenMode.Craft && world.craft != null) CraftPanel(world.craft);
                if (scroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scroll.verticalNormalizedPosition = scrollPosition;
                }
            }

            if (mode == ScreenMode.Inventory) RefreshInventory(player);
            if (mode == ScreenMode.Craft && world.craft != null && progress != null)
            {
                StationSession brew = world.craft;
                ReagentPiece mortar = brew.pieces.Find(piece => piece.stage is ReagentStage.Mortar or ReagentStage.Ground);
                int added = brew.pieces.Count(piece => piece.stage == ReagentStage.Pot);
                progress.text = brew.step == CraftStep.Stir
                    ? T("craft.Stir", brew.stir, balance.stirSeconds, balance.stirSeconds + 2)
                    : mortar?.stage == ReagentStage.Mortar
                        ? T("craft.grind", ItemName(mortar.item), mortar.presses, GameWorld.PressesToGrind, added, brew.pieces.Count)
                        : mortar?.stage == ReagentStage.Ground
                            ? T("craft.ground", ItemName(mortar.item), added, brew.pieces.Count)
                            : T("craft.Ingredients", added, brew.pieces.Count);
                if (finish != null) finish.interactable = brew.stir >= 0.1f;
            }
        }

        private static int InventoryStamp(PlayerState player)
        {
            int stamp = HashCode.Combine(player.selected, player.offhand, player.haul.Sum(), player.crateActive);
            foreach (int count in player.bag) stamp = HashCode.Combine(stamp, count);
            return stamp;
        }

        private int SiteStamp(WorldState world, PlayerState player)
        {
            if (site == Site.Shelf)
            {
                int stamp = world.day;
                foreach (ShelfStock line in world.shelf.stock)
                    stamp = HashCode.Combine(stamp, line.item, line.count, line.manualPrice, line.marketPrice);
                return stamp;
            }

            return site switch
            {
                Site.Supplier => HashCode.Combine(world.day, world.deliveries.Count),
                Site.Bench => HashCode.Combine(world.craft?.id ?? 0, InventoryStamp(player)),
                Site.Bank => HashCode.Combine(world.money, world.debt),
                _ => 0
            };
        }

        private void SitePanel(WorldState world, PlayerState player, Action close)
        {
            Heading("site." + site);
            switch (site)
            {
                case Site.Shelf: Prices(world); break;
                case Site.Supplier: Supplier(world); break;
                case Site.Bank:
                    ui.Label(panel, T("register.report", Currency(world.money), Currency(world.debt), Currency(balance.dailyTax),
                        Currency(balance.loanPayment), Currency(world.arrears)), 58);
                    ui.Label(panel, T("register.yesterday", Currency(world.lastTax), Currency(world.lastPayment)), 40);
                    ui.Button(panel, T("bank.loan", Currency(45), Currency(50)), () => command(ActionKind.Loan, 0, 0));
                    ui.Button(panel, T("bank.repay", Currency(20)), () => command(ActionKind.Repay, 0, 0));
                    break;
                case Site.Bench:
                    if (world.craft?.id > 0) ui.Label(panel, T("bench.busy"));
                    else ChooseIngredients(player);
                    break;
            }

            Button(panel, "panel.close", close);
        }

        private void Prices(WorldState world)
        {
            ui.Label(panel, T("hud.shelfPrices", world.shelf.Used, ShopShelf.Capacity), 36);
            RectTransform list = ui.Scroll(panel, 424, out scroll);
            if (world.shelf.stock.Count == 0) ui.Label(list, T("panel.empty"));
            foreach (ShelfStock line in world.shelf.stock)
            {
                int item = line.item;
                ui.Label(list, T("price.details", ItemName(item), line.count, Catalog.Get(item).ShelfSize,
                    Currency(world.market[item]), Currency(GameWorld.ShelfPrice(world, line))) +
                    " · " + T(line.marketPrice ? "price.auto" : "price.manual"), 38);
                RectTransform denominations = ui.Row(list, 36);
                ui.Label(denominations, T("money.goldName"));
                UnityEngine.UI.InputField gold = ui.Field(denominations, (line.manualPrice / Money.Gold).ToString(), "", true);
                ui.Label(denominations, T("money.silverName"));
                UnityEngine.UI.InputField silver = ui.Field(denominations, (line.manualPrice / Money.Silver % 100).ToString(), "", true);
                ui.Label(denominations, T("money.copperName"));
                UnityEngine.UI.InputField copper = ui.Field(denominations, (line.manualPrice % 100).ToString(), "", true);

                RectTransform row = ui.Row(list, 36);
                Button(row, "price.apply", () => command(ActionKind.Price, item, ParsePrice(gold.text, silver.text, copper.text)));
                Act(row, "price.auto", ActionKind.Price, item, -line.manualPrice);
                Act(row, "price.retrieve", ActionKind.Retrieve, item);
            }
        }

        private static int ParsePrice(string gold, string silver, string copper)
        {
            if (!int.TryParse(gold, out int g) || !int.TryParse(silver, out int s) || !int.TryParse(copper, out int c) ||
                g < 0 || g > 99 || s < 0 || s > 99 || c < 0 || c > 99) return 0;
            return g * Money.Gold + s * Money.Silver + c;
        }

        private void ChooseIngredients(PlayerState player)
        {
            ui.Label(panel, T("bench.hint"), 64);
            UnityEngine.UI.Text[] quantities = new UnityEngine.UI.Text[Catalog.Count];
            UnityEngine.UI.Button begin = null;
            for (int item = 0; item < Catalog.Count; item++)
            {
                if (!Catalog.IsIngredient(item)) continue;
                int selectedItem = item;
                int available = player.bag[item];
                ingredients[item] = Mathf.Min(ingredients[item], available);
                ui.Label(panel, T("bench.effects", ItemName(item), ItemName((int)GameWorld.ReagentEffect((Item)item, false)),
                    ItemName((int)GameWorld.ReagentEffect((Item)item, true))), 28, 16);
                RectTransform row = ui.Row(panel, 40);
                quantities[item] = ui.Label(row, "");
                ui.Button(row, "−", () =>
                {
                    if (ingredients[selectedItem] > 0) ingredients[selectedItem]--;
                    Refresh();
                }).interactable = available > 0;
                ui.Button(row, "+", () =>
                {
                    if (ingredients[selectedItem] < available && ingredients.Sum() < GameWorld.MaxReagents)
                        ingredients[selectedItem]++;
                    Refresh();
                }).interactable = available > 0;
            }

            begin = Button(panel, "bench.begin", () => beginBrew((int[])ingredients.Clone()));
            Refresh();

            void Refresh()
            {
                for (int item = 0; item < quantities.Length; item++)
                    if (quantities[item] != null)
                        quantities[item].text = T("bench.ingredient", ItemName(item), ingredients[item], player.bag[item]);
                begin.interactable = ingredients.Sum() >= 2;
            }
        }

        private void Supplier(WorldState world)
        {
            ui.Label(panel, T("supplier.hint"), 52);
            RectTransform list = ui.Scroll(panel, 388, out scroll);
            foreach (PendingDelivery delivery in world.deliveries)
                ui.Label(list, T("warehouse.wait", ItemName(delivery.crate.item), delivery.crate.count, Currency(delivery.paid)));
            for (int item = 0; item < Catalog.Count; item++)
            {
                int selection = item;
                RectTransform row = ui.Row(list, 37);
                ui.Label(row, ItemName(item));
                ui.Button(row, T("supplier.order", Currency(GameWorld.OrderCost(world, item))), () => command(ActionKind.Order, selection, 0));
            }
        }

        private void CraftPanel(StationSession brew)
        {
            finish = null;
            progress = ui.Label(panel, "", 52);
            if (brew.step == CraftStep.Stir)
            {
                finish = Button(panel, "craft.finish", () => command(ActionKind.FinishCraft, brew.id, 0));
            }
            Act(panel, "craft.cancel", ActionKind.CancelCraft, brew.id);
        }
    }
}
