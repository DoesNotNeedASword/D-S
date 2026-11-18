using System;
using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    internal enum ScreenMode { Menu, Party, Settings, Connecting, Play, Pause, Site, Inventory, Craft }

    internal sealed partial class PrototypeUi
    {
        private readonly UiFactory ui = new();
        private readonly Localization language;
        private readonly RectTransform overlay, panel, hud, hotbar, hudHeader, toast;
        private readonly UnityEngine.UI.Text info, hands, prompt, notice, help, raidHint, crosshair;
        private readonly UnityEngine.UI.Text[] slots = new UnityEngine.UI.Text[InventoryLayout.HotbarSize];
        private readonly UnityEngine.UI.Image[] slotColors = new UnityEngine.UI.Image[InventoryLayout.HotbarSize];
        private readonly UnityEngine.UI.Text[] meters = new UnityEngine.UI.Text[3];
        private readonly RectTransform[] fills = new RectTransform[3];
        private readonly RectTransform tooltip;
        private readonly UnityEngine.UI.Text tooltipText;
        private readonly Balance balance;
        private ScreenMode mode;
        private Site site;
        private int contentStamp = int.MinValue;
        private UnityEngine.UI.Text progress;
        private UnityEngine.UI.Button finish;
        private UnityEngine.UI.ScrollRect scroll;
        private readonly Action<ActionKind, int, int> command;
        private readonly Action<int[]> beginBrew;
        private readonly int[] ingredients = new int[Catalog.Count];

        public PrototypeUi(Transform parent, Localization language, Balance balance, Action<ActionKind, int, int> command,
            Action<int[]> beginBrew)
        {
            this.language = language;
            this.balance = balance;
            this.command = command;
            this.beginBrew = beginBrew;
            GameObject canvasObject = new("Prototype UI", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = 1;

            hud = ui.Rect(canvas.transform, "HUD");
            UiFactory.Stretch(hud, Vector2.zero, Vector2.zero);
            hudHeader = HudBox("Vitals", new Vector2(0, 1), new Vector2(22, -181), new Vector2(316, -18));
            info = HudText(hudHeader, "", new Vector2(12, 104), new Vector2(282, 156), 18);
            MakeMeter(0, 75, new Color(0.70f, 0.23f, 0.30f));
            MakeMeter(1, 47, new Color(0.22f, 0.60f, 0.47f));
            MakeMeter(2, 19, new Color(0.26f, 0.46f, 0.82f));
            crosshair = HudText(hud, "+", new Vector2(-12, -14), new Vector2(12, 14), 24, new Vector2(0.5f, 0.5f));
            hotbar = HudBox("Hotbar", new Vector2(0.5f, 0), new Vector2(-410, 25), new Vector2(410, 83));
            RectTransform row = ui.Row(hotbar, 54);
            UiFactory.Stretch(row, new Vector2(4, 4), new Vector2(-4, -4));
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform tile = ui.Box(row, "Slot " + i, UiFactory.Accent);
                slotColors[i] = tile.GetComponent<UnityEngine.UI.Image>();
                slots[i] = ui.Text(tile, "", 14, TextAnchor.MiddleCenter);
                UiFactory.Stretch(slots[i].rectTransform, new Vector2(2, 0), new Vector2(-2, 0));
            }

            hands = HudText(hud, "", new Vector2(-400, 86), new Vector2(400, 115), 18, new Vector2(0.5f, 0));
            help = HudText(hud, "", new Vector2(-480, 0), new Vector2(480, 24), 15, new Vector2(0.5f, 0));
            prompt = HudText(hud, "", new Vector2(-470, -130), new Vector2(470, -77), 22, new Vector2(0.5f, 0.5f));
            raidHint = HudText(hud, "", new Vector2(-485, -73), new Vector2(485, -22), 18, new Vector2(0.5f, 1));
            overlay = ui.Box(canvas.transform, "Overlay", UiFactory.Background);
            UiFactory.Stretch(overlay, Vector2.zero, Vector2.zero);
            panel = ui.Box(canvas.transform, "Screen", UiFactory.Panel);
            UiFactory.Column(panel, 8, 22);
            toast = ui.Rect(canvas.transform, "Toast");
            UiFactory.Anchor(toast, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-610, 124), new Vector2(610, 166));
            notice = ui.Text(toast, "", 19, TextAnchor.MiddleCenter);
            UiFactory.Stretch(notice.rectTransform, Vector2.zero, Vector2.zero);
            AddOutline(notice);
            tooltip = ui.Box(canvas.transform, "Item information", new Color(0.085f, 0.06f, 0.04f, 0.89f));
            tooltip.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            tooltip.pivot = new Vector2(0, 1);
            tooltip.sizeDelta = new Vector2(305, 100);
            tooltipText = ui.Text(tooltip, "", 16, TextAnchor.MiddleLeft);
            UiFactory.Stretch(tooltipText.rectTransform, new Vector2(12, 5), new Vector2(-10, -5));
            tooltip.gameObject.SetActive(false);
        }

        private string T(string key, params object[] values) => language.Text(key, values);
        private string ItemName(int item) => language.ItemName(item);

        private string Currency(int copper)
        {
            int gold = copper / Money.Gold;
            int silver = copper / Money.Silver % 100;
            int change = copper % 100;
            return ((gold > 0 ? T("money.gold", gold) + " " : "") +
                (silver > 0 ? T("money.silver", silver) + " " : "") +
                (change > 0 || copper == 0 ? T("money.copper", change) : "")).TrimEnd();
        }

        public void Show(ScreenMode next, Site selectedSite = Site.None)
        {
            mode = next;
            hoveredCell = null;
            tooltip.gameObject.SetActive(false);
            ClearGhost();
            site = selectedSite;
            contentStamp = int.MinValue;
            progress = null;
            Array.Clear(ingredients, 0, ingredients.Length);
            finish = null;
            scroll = null;
            bool gameplay = next is ScreenMode.Play or ScreenMode.Inventory or ScreenMode.Site or ScreenMode.Craft;
            hud.gameObject.SetActive(gameplay && next != ScreenMode.Craft);
            hotbar.gameObject.SetActive(next != ScreenMode.Inventory);
            hands.gameObject.SetActive(next != ScreenMode.Inventory);
            bool inventoryOpen = next == ScreenMode.Inventory;
            Vector2 noticeAnchor = new(0.5f, inventoryOpen ? 1 : 0);
            UiFactory.Anchor(toast, noticeAnchor, noticeAnchor, new Vector2(-610, inventoryOpen ? -106 : 124),
                new Vector2(610, inventoryOpen ? -64 : 166));
            overlay.gameObject.SetActive(!gameplay);
            panel.gameObject.SetActive(next != ScreenMode.Play);
            notice.text = "";
            UiFactory.Clear(panel);
            bool craft = next == ScreenMode.Craft;
            float height = next == ScreenMode.Settings ? 360 : 321;
            Vector2 anchor = new(0.5f, craft ? 0 : 0.5f);
            UiFactory.Anchor(panel, anchor, anchor, craft ? new Vector2(-400, 14) : new Vector2(-400, -height),
                craft ? new Vector2(400, 222) : new Vector2(400, height));
        }

        public void DrawHud(WorldState world, PlayerState player, ShopTarget focus, InputSettings bindings, string toast)
        {
            if (world == null || player == null) return;
            notice.text = T(toast);
            if (!hud.gameObject.activeSelf) return;
            bool inRaid = player.zone == Zone.Raid;
            info.text = inRaid ? T("hud.raid", world.day, world.raid.id, player.inventory.Where(s => s.cell < InventoryLayout.BackpackSize).Sum(s => InventoryLayout.Size(s.item).x * InventoryLayout.Size(s.item).y), InventoryLayout.BackpackSize) :
                T("hud.shop", world.day, T("phase." + world.phase), Currency(world.money), Currency(world.debt));
            UpdateMeter(0, "HP", player.hp);
            UpdateMeter(1, "SP", player.sp);
            UpdateMeter(2, "EP", player.ep);
            int main = player.Main;
            hands.text = player.carriedCrate?.id > 0
                ? T(player.HoldingCrate ? "hud.crateActive" : "hud.crateStored",
                    bindings.Display(Bind.Crate), ItemName(player.carriedCrate.item), player.carriedCrate.count)
                : T("hud.main", ItemName(main),
                    main >= 0 && Catalog.Get(main).TwoHanded ? T("hud.both") : ItemName(player.Offhand));
            help.text = inRaid ? T("hud.controls", bindings.Display(Bind.Use), bindings.Display(Bind.Inventory), bindings.Display(Bind.Crouch))
                : T("hud.shopControls", bindings.Display(Bind.Interact), bindings.Display(Bind.Use),
                    bindings.Display(Bind.Crate), bindings.Display(Bind.Inventory));
            for (int i = 0; i < slots.Length; i++)
            {
                slotColors[i].gameObject.SetActive(i < player.quickSlots);
                if (i >= player.quickSlots) continue;
                var stack = player.Quick(i);
                int item = stack?.item ?? -1;
                string name = item >= 0 ? ItemName(item) + " ×" + stack.count : i == 1 && InventoryLayout.TwoHands(player) ? T("hud.both") : T("hud.empty");
                slots[i].text = ((i + 1) % 10) + "\n" + name;
                slotColors[i].color = i == player.selected && !player.HoldingCrate
                    ? new Color(0.18f, 0.43f, 0.38f) : UiFactory.Panel;
            }

            crosshair.enabled = mode == ScreenMode.Play;
            string target = "";
            if (mode == ScreenMode.Play)
            {
                if (player.night == NightChoice.Sleeping) target = T("hud.sleep");
                else if (player.ready) target = T("hud.ready");
                else
                {
                    if (inRaid)
                    {
                        if (GameWorld.NearbyLoot(player, world.raid) != -2) target = T("hud.loot");
                        else if (Map.NearestSite(player) == Site.Exit) target = T("site.Exit");
                        if (target.Length > 0) target = T("hud.interact", bindings.Display(Bind.Interact), target);
                    }
                    else target = ShopPrompt(world, player, focus, bindings);
                    if (!inRaid && world.phase == DayPhase.Night)
                        target += (target.Length > 0 ? "\n" : "") + T("hud.dawn");
                }
            }

            if (mode == ScreenMode.Play)
            {
                var stock = focus.Site == Site.Shelf && focus.Index == 1 ? world.shelf.stock.Find(s => s.Key == focus.Id) : null;
                if (stock != null) ShowItemInfo(stock.item, stock.formula, stock.count, default,
                    new Vector2(Screen.width * 0.52f, Screen.height * 0.54f), GameWorld.ShelfPrice(world, stock));
                else tooltip.gameObject.SetActive(false);
            }
            prompt.text = target;
            raidHint.text = "";
            if (inRaid && mode == ScreenMode.Play)
            {
                RaidState raid = world.raid;
                if (raid.enemies.Any(e => e.phase == EnemyPhase.Windup && e.target == player.id))
                    raidHint.text = T("hud.threat");
                else if (player.position.z >= 45 && !raid.bossUnlocked)
                    raidHint.text = raid.enemies.Take(raid.enemies.Length - 1).All(e => e.phase == EnemyPhase.Defeated)
                        ? T("hud.gather", Mathf.CeilToInt(raid.gatherRemaining)) : T("hud.clear");
                else if (player.position.z >= 48 && raid.enemies[^1].phase != EnemyPhase.Defeated)
                    raidHint.text = T("hud.boss");
                else if (player.crouched)
                    raidHint.text = T("hud.crouched");
            }
        }

        private string ShopPrompt(WorldState world, PlayerState player, ShopTarget focus, InputSettings bindings)
        {
            string interact = bindings.Display(Bind.Interact);
            if (focus.Kind == ShopTargetKind.Crate)
            {
                Crate crate = world.warehouse.Find(box => box.id == focus.Id) ?? world.dropped.Find(box => box.id == focus.Id);
                if (crate == null) return "";
                string hint = T("hud.crateFocus", ItemName(crate.item), crate.count, interact, bindings.Display(Bind.Use));
                return crate.quality > 0 ? hint + " " + T("warehouse.grade", crate.quality) : hint;
            }

            if (focus.Kind == ShopTargetKind.Payment)
            {
                Customer buyer = world.customer;
                int remaining = buyer.payment.Where((_, i) => (buyer.collected & (1 << i)) == 0).Sum();
                return T("hud.interact", interact, T("hud.collect", Currency(buyer.payment[focus.Index]), Currency(remaining)));
            }

            if (player.HoldingCrate && focus.Site is not (Site.Shelf or Site.Warehouse or Site.Bin))
                return T("hud.throw", bindings.Display(Bind.Crate));
            switch (focus.Site)
            {
                case Site.Bin: return player.HoldingCrate ? T("hud.discard", interact) : T("site.Bin");
                case Site.None: return "";
                case Site.Shelf:
                    return player.HoldingCrate
                        ? T("hud.stock", interact, world.shelf.Used, ShopShelf.Capacity,
                            Catalog.Get(player.carriedCrate.item).ShelfSize)
                        : T("hud.interact", interact, T("hud.shelfPrices", world.shelf.Used, ShopShelf.Capacity));
                case Site.Register: return T("hud.register", Currency(world.money));
                case Site.Warehouse:
                    return player.HoldingCrate ? T("hud.interact", interact, T("hud.placeCrate")) : T("hud.aimCrate");
                case Site.Sign:
                    if (world.phase == DayPhase.Night) return T("sign.closed");
                    return T("hud.interact", interact, world.phase == DayPhase.Preparation && player.ep >= balance.shiftEnergy
                        ? T("register.open", balance.shiftEnergy) : T("register.close"));
                case Site.Gate: return T("hud.interact", interact, T("hud.enter", balance.raidEnergy));
                case Site.Bed: return T("hud.interact", interact, T("bed.sleep"));
                default: return T("hud.interact", interact, T("site." + focus.Site));
            }
        }

        public void Menu(string error, Action solo, Action party, Action settings, Action quit)
        {
            Heading("title");
            ui.Label(panel, T("subtitle"), 59, 20);
            Button(panel, "menu.solo", solo, 55);
            Button(panel, "menu.party", party, 55);
            Button(panel, "menu.settings", settings, 46);
            Button(panel, "menu.exit", quit, 46);
            ui.Label(panel, T(error), 50);
        }

        public void Party(string address, string error, Action host, Action<string> join, Action back)
        {
            Heading("menu.party");
            Button(panel, "menu.host", host, 52);
            UnityEngine.UI.InputField field = ui.Field(panel, address, T("menu.address"), false);
            Button(panel, "menu.join", () => join(field.text.Trim()), 52);
            Button(panel, "menu.back", back);
            ui.Label(panel, T(error), 50);
        }

        public void Pause(Action resume, Action settings, Action leave)
        {
            Heading("menu.pause");
            Button(panel, "menu.resume", resume);
            Button(panel, "menu.settings", settings);
            Button(panel, "menu.leave", leave);
        }

        public void Connecting(Action cancel)
        {
            Heading("menu.party");
            ui.Label(panel, T("network.connecting"), 60);
            Button(panel, "menu.back", cancel);
        }

        public void Settings(InputSettings settings, Bind? rebinding, Action locale, Action<Bind> bind, Action reset, Action back)
        {
            Heading("menu.settings");
            UnityEngine.UI.Text volume = ui.Label(panel, T("settings.sound", Mathf.RoundToInt(settings.Volume * 100)));
            ui.Slider(panel, 0, 1, settings.Volume, value =>
            {
                settings.Sound(value);
                volume.text = T("settings.sound", Mathf.RoundToInt(value * 100));
            });
            UnityEngine.UI.Text mouse = ui.Label(panel, T("settings.mouse", settings.Sensitivity));
            ui.Slider(panel, 0.2f, 3, settings.Sensitivity, value =>
            {
                settings.Mouse(value);
                mouse.text = T("settings.mouse", value);
            });
            Button(panel, "settings.locale", locale);
            foreach (Bind value in Enum.GetValues(typeof(Bind)))
            {
                string binding = T("bind." + value.ToString().ToLowerInvariant());
                ui.Button(panel, rebinding == value ? T("settings.press") :
                    T("settings.bind", binding, settings.Display(value)), () => bind(value));
            }

            Button(panel, "settings.reset", reset);
            Button(panel, "menu.back", back);
        }

        private void Heading(string key)
        {
            UnityEngine.UI.Text heading = ui.Label(panel, T(key), 62, 34);
            heading.fontStyle = FontStyle.Bold;
            heading.color = new Color(1, 0.79f, 0.47f);
        }

        public void Dispose() => ui.Dispose();
        private UnityEngine.UI.Button Button(Transform parent, string key, Action invoke, int height = 38) =>
            ui.Button(parent, T(key), invoke, height);

        private void Act(Transform parent, string key, ActionKind kind, int value = 0, int extra = 0) =>
            Button(parent, key, () => command(kind, value, extra));

        private RectTransform HudBox(string title, Vector2 anchor, Vector2 min, Vector2 max)
        {
            RectTransform rect = ui.Box(hud, title, UiFactory.Panel);
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            UiFactory.Anchor(rect, anchor, anchor, min, max);
            return rect;
        }

        private UnityEngine.UI.Text HudText(Transform parent, string value, Vector2 min, Vector2 max, int size, Vector2? anchor = null)
        {
            UnityEngine.UI.Text text = ui.Text(parent, value, size, anchor == null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            Vector2 a = anchor ?? Vector2.zero;
            UiFactory.Anchor(text.rectTransform, a, a, min, max);
            AddOutline(text);
            return text;
        }

        private static void AddOutline(UnityEngine.UI.Text text)
        {
            UnityEngine.UI.Outline outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.93f);
            outline.effectDistance = new Vector2(1, -1);
        }

        private void MakeMeter(int index, int y, Color color)
        {
            RectTransform bar = ui.Box(hudHeader, "Meter", new Color(0.16f, 0.19f, 0.24f));
            UiFactory.Anchor(bar, Vector2.zero, Vector2.zero, new Vector2(12, y), new Vector2(281, y + 22));
            bar.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            fills[index] = ui.Box(bar, "Fill", color);
            fills[index].GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            meters[index] = ui.Text(bar, "", 15, TextAnchor.MiddleCenter);
            UiFactory.Stretch(meters[index].rectTransform, Vector2.zero, Vector2.zero);
        }

        private void UpdateMeter(int index, string name, float value)
        {
            UiFactory.Anchor(fills[index], Vector2.zero, new Vector2(value / 100, 1), Vector2.zero, Vector2.zero);
            meters[index].text = name + "  " + Mathf.CeilToInt(value) + " / 100";
        }
    }
}
