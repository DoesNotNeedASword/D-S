using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonsNShops
{
    internal sealed class Blockout
    {
        private static readonly Color Stone = new(0.19f, 0.23f, 0.29f);
        private static readonly Color Wood = new(0.36f, 0.24f, 0.17f);
        private static readonly Color Gold = new(0.93f, 0.65f, 0.25f);
        private static readonly Color Teal = new(0.22f, 0.73f, 0.66f);
        private static readonly Color Blue = new(0.34f, 0.55f, 0.94f);
        private static readonly Color Threat = new(0.86f, 0.24f, 0.23f);
        private readonly Camera camera;
        private readonly Transform shop, raid, craft;
        private readonly GameObject customer, remote, gate, decoration, cache, heldCrate, sign;
        private readonly TextMesh signLabel;
        private Localization language;
        private readonly Renderer customerRenderer;
        private readonly GameObject[] veins = new GameObject[GameWorld.VeinCount];
        private readonly GameObject[] traps = new GameObject[12];
        private readonly List<GameObject> enemies = new(), loot = new(), stock = new(), crates = new(), payments = new();
        private readonly Dictionary<int, GameObject> looseCrates = new();
        private readonly List<int> removedCrates = new();
        private readonly Dictionary<Site, TextMesh> labels = new();
        private readonly MaterialPropertyBlock colorBlock = new();
        private readonly MedievalModels models = new();
        private readonly WorkbenchView workbench;
        private readonly Dictionary<GameObject, int> displayedItems = new();
        private readonly Dictionary<GameObject, GameObject> itemModels = new();
        private readonly Dictionary<GameObject, ActorMotion> actors = new();
        private readonly List<GameObject> serviceActors = new();
        private readonly Transform rightHand, leftHand;
        private GameObject sword, greatsword;
        private float shieldRaise;
        private Vector3 cameraPosition;
        private bool teleport = true;
        private int raidId;
        private Zone lastZone;
        private DayPhase displayedPhase = (DayPhase)(-1);

        public Blockout(Transform root, Camera camera)
        {
            this.camera = camera;
            shop = Group("Shop", root);
            raid = Group("Dungeon", root);
            craft = Group("Local alchemy", root);
            BuildShop();
            BuildRaid();
            Bounds signBounds = ShopTargets.Station(Site.Sign);
            sign = Box(shop, "Shop sign", signBounds.center, signBounds.size, Threat);
            models.Part(sign.transform, "Brass crest", new Vector3(0, 0.54f, 0), new Vector3(0.18f, 0.14f, 1.12f), MedievalModels.Brass, roll: 15);
            Box(shop, "Sign post", signBounds.center + Vector3.down * 0.65f, new Vector3(0.12f, 1.55f, 0.12f), Wood);
            signLabel = Label(shop, "Sign text", signBounds.center + Vector3.back * 0.1f, 0.022f);
            heldCrate = Box(camera.transform, "Crate in hand", Vector3.zero, new Vector3(0.47f, 0.36f, 0.44f), Wood);
            heldCrate.transform.localPosition = new Vector3(0.35f, -0.32f, 0.64f);
            heldCrate.transform.localRotation = Quaternion.Euler(12, -12, 0);
            heldCrate.GetComponent<Collider>().enabled = false;
            models.Crate(heldCrate.transform);
            rightHand = Group("First person weapon arm", camera.transform);
            leftHand = Group("First person shield arm", camera.transform);
            models.Part(rightHand, "Leather glove", new Vector3(0, -0.04f, 0), new Vector3(0.16f, 0.18f, 0.19f), MedievalModels.Oak);
            Transform shield = Group("Equipped shield", leftHand);
            shield.localScale = Vector3.one * 0.69f;
            shield.localRotation = Quaternion.Euler(-90, 180, 0);
            models.ItemModel(shield, Item.Shield);

            customer = Actor(shop, "Customer", Gold);
            customerRenderer = customer.transform.Find("Badge").GetComponent<Renderer>();
            remote = Actor(root, "Partner", Blue);
            gate = Box(raid, "Boss barrier", Map.BossGate + Vector3.up * 2, new Vector3(12, 4, 0.45f), Threat);
            cache = Box(raid, "Fireproof coat", Map.Cache + Vector3.up * 0.38f, new Vector3(0.95f, 0.65f, 0.65f), Teal);
            models.Crate(cache.transform);
            decoration = Box(shop, "Decoration", new Vector3(6, 1.2f, -8), new Vector3(0.6f, 1, 0.6f), Gold);
            decoration.transform.rotation = Quaternion.Euler(0, 45, 0);
            DisplayItem(decoration, (int)Item.Herb);
            for (int i = 0; i < veins.Length; i++)
            {
                veins[i] = Box(raid, "Ore", GameWorld.VeinPosition(i) + Vector3.up * 0.25f, Vector3.one * 0.48f, Gold);
                DisplayItem(veins[i], (int)Item.Ore);
            }
            for (int i = 0; i < traps.Length; i++)
            {
                traps[i] = Box(raid, "Trap", Vector3.zero, new Vector3(1.1f, 0.12f, 1.1f), Teal);
                DisplayItem(traps[i], (int)Item.Trap);
            }

            workbench = new WorkbenchView(craft, camera, models);
            Hide();
        }

        public void Relabel(Localization language)
        {
            this.language = language;
            displayedPhase = (DayPhase)(-1);
            foreach (KeyValuePair<Site, TextMesh> entry in labels)
                entry.Value.text = language.Text("site." + entry.Key);
        }

        public void Hide()
        {
            shop.gameObject.SetActive(false);
            raid.gameObject.SetActive(false);
            craft.gameObject.SetActive(false);
            remote.SetActive(false);
            heldCrate.SetActive(false);
            rightHand.gameObject.SetActive(false);
            leftHand.gameObject.SetActive(false);
            workbench.Hide();
            teleport = true;
        }

        public void Render(WorldState state, PlayerState player, Vector2 aim, float delta, bool showHands)
        {
            bool making = state.craft?.owner == player.id && state.craft.id > 0;
            bool inRaid = player.zone == Zone.Raid;
            shop.gameObject.SetActive(!making && !inRaid);
            raid.gameObject.SetActive(!making && inRaid);
            craft.gameObject.SetActive(making);
            heldCrate.SetActive(showHands && !making && player.HoldingCrate);
            if (heldCrate.activeSelf) Tint(heldCrate.GetComponent<Renderer>(), ItemColor(player.carriedCrate.item));
            if (making)
            {
                workbench.Show(state.craft);
                teleport = true;
            }
            else
            {
                workbench.Hide();
                Vector3 target = player.position + Vector3.up * player.EyeHeight;
                cameraPosition = teleport || lastZone != player.zone || (target - cameraPosition).sqrMagnitude > 16
                    ? target : Vector3.Lerp(cameraPosition, target, 1 - Mathf.Exp(-22 * delta));
                camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(aim.y, aim.x, 0));
                lastZone = player.zone;
                teleport = false;
            }

            PlayerState partner = state.players.Find(p => p.online && p.id != player.id && p.zone == player.zone);
            remote.SetActive(!making && partner != null);
            if (remote.activeSelf)
            {
                remote.transform.position = partner.position + Vector3.up;
                remote.transform.rotation = Quaternion.Euler(0, partner.yaw, 0);
                actors[remote].Update(partner.position, false, partner.crouched, partner.guarding, 0, partner.swing, delta);
            }
            else actors[remote].Hide();

            RenderHands(player, showHands && !making && inRaid, delta);
            if (making) return;
            if (inRaid && state.raid?.id > 0) RenderRaid(state.raid, delta);
            if (!inRaid) RenderShop(state, delta);
        }

        private void RenderHands(PlayerState player, bool visible, float delta)
        {
            rightHand.gameObject.SetActive(visible && !player.HoldingCrate);
            leftHand.gameObject.SetActive(visible && player.HasShield);
            if (!visible) return;

            sword ??= HandSword(Item.Blade);
            greatsword ??= HandSword(Item.Greatblade);
            sword.SetActive(player.Main == (int)Item.Blade);
            greatsword.SetActive(player.Main == (int)Item.Greatblade);
            float swing = Mathf.Sin(Mathf.Clamp01(player.swing / 0.36f) * Mathf.PI);
            shieldRaise = Mathf.MoveTowards(shieldRaise, player.guarding ? 1 : 0, delta * 9);
            rightHand.localPosition = new Vector3(0.38f - swing * 0.45f, -0.39f + swing * 0.21f, 0.77f + swing * 0.28f);
            rightHand.localRotation = Quaternion.Euler(14 - swing * 37, 18 - swing * 85, 73 - swing * 105);
            leftHand.localPosition = Vector3.Lerp(new Vector3(-0.47f, -0.51f, 0.80f),
                new Vector3(-0.25f, -0.10f, 0.79f), shieldRaise);
            leftHand.localRotation = Quaternion.Euler(-13 + shieldRaise * 11, -28 + shieldRaise * 25, 15 - shieldRaise * 11);
        }

        private GameObject HandSword(Item item)
        {
            Transform model = Group(item.ToString(), rightHand);
            model.localPosition = Vector3.right * 0.25f;
            model.localScale = Vector3.one * (item == Item.Blade ? 0.79f : 1.12f);
            models.ItemModel(model, item);
            return model.gameObject;
        }

        public void CraftPointer(Action<ActionKind, int> submit) => workbench.Pointer(submit);

        public void ReleaseDrag() => workbench.ResetGesture();
        public void FixedTick() => workbench.FixedTick();
        public void Dispose() => models.Dispose();

        private void BuildShop()
        {
            Box(shop, "Shop floor", new Vector3(0, -0.18f, 0), new Vector3(20, 0.35f, 20), new Color(0.34f, 0.34f, 0.32f));
            Box(shop, "West wall", new Vector3(-10, 1.4f, 0), new Vector3(0.4f, 2.8f, 20), Stone);
            Box(shop, "East wall", new Vector3(10, 1.4f, 0), new Vector3(0.4f, 2.8f, 20), Stone);
            Box(shop, "Rear wall", new Vector3(0, 1.4f, -10), new Vector3(20, 2.8f, 0.4f), Stone);
            Box(shop, "North wall", new Vector3(0, 1.4f, 10), new Vector3(20, 2.8f, 0.4f), Stone);
            for (int plank = 0; plank < 33; plank++)
                models.Part(shop, "Floorboard", new Vector3(-9.7f + plank * 0.60f, 0, 0), new Vector3(0.59f, 0.02f, 20),
                    Color.Lerp(MedievalModels.Oak, MedievalModels.Wood, (plank * 17 % 13) / 15f));
            for (int beam = -8; beam <= 8; beam += 4)
            {
                models.Part(shop, "North timber", new Vector3(beam, 1.4f, 9.74f), new Vector3(0.20f, 2.8f, 0.24f), MedievalModels.Oak);
                models.Part(shop, "West timber", new Vector3(-9.74f, 1.4f, beam), new Vector3(0.24f, 2.8f, 0.20f), MedievalModels.Oak);
                models.Part(shop, "East timber", new Vector3(9.74f, 1.4f, beam), new Vector3(0.24f, 2.8f, 0.20f), MedievalModels.Oak);
            }
            models.Part(shop, "Crossbeam", new Vector3(0, 2.36f, 9.73f), new Vector3(20, 0.21f, 0.23f), MedievalModels.Oak);
            foreach (Rect obstacle in Map.ShopObstacles)
            {
                bool rack = obstacle.height > 2;
                if (rack) continue;
                models.Table(shop, new Vector3(obstacle.center.x, 0, obstacle.center.y),
                    new Vector3(obstacle.width, 0.95f, obstacle.height), false);
            }
            for (int side = 0; side < 2; side++)
            {
                Box(shop, "Rack lower shelf", ShopTargets.RackPosition(side, 0.925f), new Vector3(0.65f, 0.065f, 6.1f), Wood);
                Box(shop, "Rack upper shelf", ShopTargets.RackPosition(side, 1.47f), new Vector3(0.65f, 0.065f, 6.1f), Wood);
                foreach (float end in new[] { -3f, 3f })
                    models.Part(shop, "Rack upright", ShopTargets.RackPosition(side, 0.8f) + Vector3.forward * end,
                        new Vector3(0.69f, 1.6f, 0.10f), MedievalModels.Oak);
            }
            Vector3 bed = Map.SitePosition(Site.Bed);
            models.Part(shop, "Bed frame", bed + Vector3.up * 0.27f, new Vector3(1.15f, 0.34f, 1.76f), MedievalModels.Oak);
            models.Part(shop, "Wool blanket", bed + Vector3.up * 0.48f, new Vector3(1.06f, 0.17f, 1.55f), MedievalModels.Cloth);
            models.Part(shop, "Pillow", bed + new Vector3(0, 0.61f, 0.55f), new Vector3(0.88f, 0.15f, 0.39f), new Color(0.77f, 0.68f, 0.52f));
            Vector3 gatePosition = Map.SitePosition(Site.Gate);
            for (int i = -3; i <= 3; i++)
                models.Part(shop, "Gate board", gatePosition + new Vector3(i * 0.22f, 1.04f, 0.35f),
                    new Vector3(0.21f, 2.08f - Mathf.Abs(i) * 0.11f, 0.12f), MedievalModels.Wood);

            Vector3 bin = Map.SitePosition(Site.Bin);
            models.Bin(shop, bin);
            AddSite(Site.Bin, Stone);
            AddSite(Site.Warehouse, Teal);
            AddSite(Site.Shelf, Gold);
            AddSite(Site.Register, Gold);
            AddSite(Site.Supplier, Blue, true);
            AddSite(Site.Bank, Blue, true);
            AddSite(Site.Bench, Teal);
            AddSite(Site.Gate, Threat);
            AddSite(Site.Bed, Blue);
            AddSite(Site.Decor, Teal);
        }

        private void BuildRaid()
        {
            Box(raid, "Dungeon floor", new Vector3(0, -0.2f, 39.8f), new Vector3(12, 0.4f, 82), new Color(0.28f, 0.27f, 0.32f));
            Box(raid, "Left wall", new Vector3(-6, 1.6f, 40), new Vector3(0.4f, 3.2f, 82), Stone);
            Box(raid, "Right wall", new Vector3(6, 1.6f, 40), new Vector3(0.4f, 3.2f, 82), Stone);
            Box(raid, "End wall", new Vector3(0, 1.6f, 80), new Vector3(12, 3.2f, 0.4f), Stone);
            Box(raid, "Entrance wall", new Vector3(0, 1.6f, -1), new Vector3(12, 3.2f, 0.4f), Stone);
            foreach (Rect obstacle in Map.RaidObstacles)
                Footprint(raid, obstacle, 2.6f, Stone);
            for (int column = 0; column < 29; column++)
                for (int side = -1; side <= 1; side += 2)
                    for (int course = 0; course < 3; course++)
                        models.Part(raid, "Chiseled stone", new Vector3(side * 5.75f, 0.48f + course * 0.98f,
                            column * 2.81f + (course % 2) * 0.8f - 0.7f), new Vector3(0.26f, 0.94f, 2.74f),
                            Color.Lerp(Stone, new Color(0.38f, 0.35f, 0.33f), (column * 19 + course * 7) % 11 / 14f));
            for (int i = 4; i < 80; i += 6)
                Box(raid, "Route marker", new Vector3(0, 0.01f, i), new Vector3(0.24f, 0.03f, 0.85f), Teal);
            AddSite(Site.Exit, Teal);
        }

        private void AddSite(Site site, Color tint, bool npc = false)
        {
            Transform parent = site == Site.Exit ? raid : shop;
            Vector3 point = Map.SitePosition(site);
            models.Part(parent, site + " woven runner", point + Vector3.up * 0.024f, new Vector3(1.7f, 0.035f, 1.1f),
                Color.Lerp(tint, MedievalModels.Oak, 0.58f));
            if (npc)
            {
                GameObject service = Actor(parent, site.ToString(), tint, site == Site.Supplier ? -2 : -1);
                service.transform.SetPositionAndRotation(point + Vector3.up, Quaternion.Euler(0, 225, 0));
                serviceActors.Add(service);
            }
            TextMesh text = Label(parent, site + " label", point + Vector3.up * (site == Site.Gate ? 2.55f : 2.13f), 0.035f);
            if (site == Site.Exit) text.transform.rotation = Quaternion.Euler(0, 180, 0);
            labels.Add(site, text);
        }

        private static TextMesh Label(Transform parent, string name, Vector3 position, float size)
        {
            GameObject label = new(name);
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            TextMesh text = label.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 38;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.97f, 0.96f, 0.91f);
            return text;
        }

        private void RenderShop(WorldState state, float delta)
        {
            decoration.SetActive(state.decorated);
            foreach (GameObject service in serviceActors)
                actors[service].Update(service.transform.position, false, false, false, 0, 0, delta);
            if (displayedPhase != state.phase)
            {
                displayedPhase = state.phase;
                Tint(sign.GetComponent<Renderer>(), state.phase == DayPhase.Trading ? AccentGreen : Threat);
                signLabel.text = language.Text(state.phase == DayPhase.Trading ? "sign.open" : "sign.closed");
            }

            Grow(stock, ShopShelf.Capacity, shop, "Shelf item", Vector3.one, Gold);
            int used = 0, units = 0;
            foreach (ShelfStock line in state.shelf.stock)
            {
                int size = Catalog.Get(line.item).ShelfSize;
                for (int i = 0; i < line.count; i++)
                {
                    GameObject product = stock[units++];
                    product.SetActive(true);
                    product.transform.position = new Vector3(-3.93f + (used + size / 2f) * 0.155f, 1.04f, 1.94f);
                    product.transform.localScale = new Vector3(size * 0.155f - 0.02f, size >= 4 ? 0.12f : 0.19f, 0.36f);
                    DisplayItem(product, line.item);
                    used += size;
                }
            }
            for (int i = units; i < stock.Count; i++) stock[i].SetActive(false);

            while (crates.Count < ShopTargets.RackSlots)
            {
                GameObject box = Box(shop, "Crate", Vector3.zero, ShopTargets.CrateSize, Wood);
                models.Crate(box.transform);
                crates.Add(box);
            }
            int visible = 0;
            foreach (Crate crate in state.warehouse)
            {
                if (crate.rackSlot < 0 || crate.rackSlot >= crates.Count) continue;
                GameObject box = crates[crate.rackSlot];
                box.SetActive(true);
                box.transform.position = ShopTargets.CratePosition(crate.rackSlot);
                Tint(box.GetComponent<Renderer>(), ItemColor(crate.item));
                visible |= 1 << crate.rackSlot;
            }
            for (int i = 0; i < crates.Count; i++)
                if ((visible & (1 << i)) == 0) crates[i].SetActive(false);

            foreach (var crate in state.dropped)
            {
                if (!looseCrates.TryGetValue(crate.id, out GameObject box))
                {
                    box = Box(shop, "Dropped crate " + crate.id, crate.position, ShopTargets.CrateSize, Wood);
                    box.GetComponent<Collider>().enabled = false;
                    models.Crate(box.transform);
                    looseCrates.Add(crate.id, box);
                }
                box.transform.SetPositionAndRotation(crate.position, Quaternion.Euler(crate.angles));
            }
            removedCrates.Clear();
            foreach (var entry in looseCrates)
                if (!state.dropped.Exists(c => c.id == entry.Key)) removedCrates.Add(entry.Key);
            foreach (int id in removedCrates)
            {
                UnityEngine.Object.Destroy(looseCrates[id]);
                looseCrates.Remove(id);
            }
            Customer visitor = state.customer;
            RenderPayments(visitor);
            customer.SetActive(visitor?.id > 0);
            if (!customer.activeSelf) { actors[customer].Hide(); return; }
            Vector3 entrance = new(4.5f, 1, 8.7f);
            Vector3 shelf = new(-2.5f, 1, 3.35f);
            Vector3 checkout = new(0.75f, 1, 6.0f);
            customer.transform.position = visitor.phase switch
            {
                CustomerPhase.Walking => Vector3.Lerp(shelf, entrance, visitor.remaining / 3),
                CustomerPhase.Choosing => shelf,
                CustomerPhase.Checkout => checkout,
                _ => Vector3.Lerp(entrance, checkout, visitor.remaining / 3)
            };
            customer.transform.rotation = Quaternion.Euler(0, visitor.phase == CustomerPhase.Leaving ? 35 : 180, 0);
            actors[customer].Update(customer.transform.position, visitor.phase is CustomerPhase.Walking or CustomerPhase.Leaving,
                false, false, 0, 0, delta);
            Tint(customerRenderer, visitor.archetype == 0 ? Gold : Teal);
        }

        private static readonly Color AccentGreen = new(0.13f, 0.49f, 0.37f);
        private static Color ItemColor(int item) => (Item)item switch
        {
            Item.Herb => AccentGreen,
            Item.Ore or Item.Shield => new Color(0.58f, 0.67f, 0.76f),
            Item.Blade or Item.Greatblade => Blue,
            Item.Focus => new Color(0.65f, 0.36f, 0.9f),
            Item.Tonic => Teal,
            Item.Ember => Threat,
            _ => Gold
        };

        private void RenderPayments(Customer buyer)
        {
            int count = buyer?.phase == CustomerPhase.Checkout ? buyer.payment?.Length ?? 0 : 0;
            while (payments.Count < count)
                payments.Add(Primitive(PrimitiveType.Cylinder, shop, "Coin stack", Vector3.zero, Vector3.one, Gold));
            for (int i = 0; i < payments.Count; i++)
            {
                GameObject stack = payments[i];
                bool active = i < count && (buyer.collected & (1 << i)) == 0;
                stack.SetActive(active);
                if (!active) continue;
                int denomination = Money.Denomination(buyer.payment[i]);
                float height = 0.045f + 0.011f * (buyer.payment[i] / denomination);
                stack.transform.position = ShopTargets.CoinPosition(i) + Vector3.down * (0.08f - height / 2);
                stack.transform.localScale = new Vector3(0.135f, height / 2, 0.135f);
                Tint(stack.GetComponent<Renderer>(), denomination == Money.Gold ? Gold : denomination == Money.Silver
                    ? new Color(0.72f, 0.80f, 0.85f) : new Color(0.72f, 0.34f, 0.16f));
            }
        }

        private void RenderRaid(RaidState state, float delta)
        {
            if (raidId != state.id)
            {
                raidId = state.id;
                while (enemies.Count < state.enemies.Length)
                    enemies.Add(Actor(raid, "Enemy", Threat, state.enemies[enemies.Count].kind));
                Grow(loot, state.enemies.Length, raid, "Loot", Vector3.one * 0.34f, Gold);
            }

            gate.SetActive(!state.bossUnlocked);
            cache.SetActive(!state.cacheTaken);
            for (int i = 0; i < veins.Length; i++) veins[i].SetActive((state.veinsTaken & (1 << i)) == 0);
            for (int i = 0; i < traps.Length; i++)
            {
                traps[i].SetActive(i < state.traps.Count);
                if (traps[i].activeSelf) traps[i].transform.position = state.traps[i] + Vector3.up * 0.06f;
            }

            for (int i = 0; i < state.enemies.Length; i++)
            {
                Enemy enemy = state.enemies[i];
                bool defeated = enemy.phase == EnemyPhase.Defeated;
                GameObject actor = enemies[i];
                actor.SetActive(!defeated && (enemy.kind != 2 || state.bossUnlocked));
                loot[i].SetActive(defeated && !enemy.looted);
                loot[i].transform.position = enemy.position + Vector3.up * 0.23f;
                if (loot[i].activeSelf) DisplayItem(loot[i], (int)(enemy.kind == 2 ? Item.Ember : enemy.kind == 1 ? Item.Herb : Item.Ore));
                if (!actor.activeSelf) { actors[actor].Hide(); continue; }
                float size = enemy.kind == 2 ? 1.65f : enemy.kind == 1 ? 0.77f : 1;
                actor.transform.SetPositionAndRotation(enemy.position + Vector3.up * size, Quaternion.Euler(0, enemy.yaw, 0));
                actor.transform.localScale = new Vector3(size, size, size);
                float windup = enemy.phase == EnemyPhase.Windup
                    ? 1 - Mathf.Clamp01(enemy.remaining / (enemy.kind == 2 ? 1.45f : 0.95f)) : 0;
                actors[actor].Update(enemy.position, enemy.phase == EnemyPhase.Pursuit, false, false, windup, enemy.swing, delta);
                Color tint = enemy.phase == EnemyPhase.Windup ? Gold : enemy.stun > 0 ? Teal :
                    enemy.kind == 1 ? new Color(0.76f, 0.42f, 0.9f) : Threat;
                Tint(actor.transform.Find("Badge").GetComponent<Renderer>(), tint);
            }
        }

        private void Grow(List<GameObject> list, int count, Transform parent, string name, Vector3 size, Color tint)
        {
            while (list.Count < count)
                list.Add(Box(parent, name, Vector3.zero, size, tint));
        }

        private void Footprint(Transform parent, Rect box, float height, Color tint) =>
            Box(parent, "Obstacle", new Vector3(box.center.x, height / 2, box.center.y),
                new Vector3(box.width, height, box.height), tint);

        private static Transform Group(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private GameObject Actor(Transform parent, string name, Color tint, int creature = -1)
        {
            GameObject actor = Primitive(PrimitiveType.Capsule, parent, name, Vector3.zero, Vector3.one, tint);
            actor.GetComponent<Renderer>().enabled = false;
            actors[actor] = models.Person(actor.transform, Color.Lerp(tint, MedievalModels.Oak, 0.50f), creature);
            models.Part(actor.transform, "Badge", new Vector3(0, 0.18f, 0.2f), new Vector3(0.21f, 0.27f, 0.07f), tint);
            return actor;
        }

        private void DisplayItem(GameObject root, int item)
        {
            if (displayedItems.TryGetValue(root, out int current) && current == item) return;
            if (itemModels.TryGetValue(root, out GameObject previous))
            {
                previous.SetActive(false);
                UnityEngine.Object.Destroy(previous);
            }
            root.GetComponent<Renderer>().enabled = false;
            Transform model = Group("Item model", root.transform);
            models.ItemModel(model, (Item)item);
            itemModels[root] = model.gameObject;
            displayedItems[root] = item;
        }

        private GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Color tint) =>
            Primitive(PrimitiveType.Cube, parent, name, position, size, tint);

        private GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 size, Color tint)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            obj.transform.localScale = size;
            Tint(obj.GetComponent<Renderer>(), tint);
            return obj;
        }

        private void Tint(Renderer renderer, Color color)
        {
            colorBlock.Clear();
            colorBlock.SetColor("_BaseColor", color);
            colorBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(colorBlock);
        }
    }
}
