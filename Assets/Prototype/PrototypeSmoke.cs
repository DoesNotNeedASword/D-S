#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DungeonsNShops
{
    public static class PrototypeSmoke
    {
        private const string PlayCheck = "PrototypeSmoke.PlayCheck";
        private const string BatchCheck = "PrototypeSmoke.BatchCheck";
        private static GameSession runtime;
        private static TestInput simulatedInput;
        private static int stage;
        private static double deadline;
        private static double holdUntil, gestureAt;
        private static (Ray ray, bool pressed, bool held) pointer;

        private sealed class TestInput : IPlayerInput
        {
            public Bind? next;
            public Bind? held;
            public int slot = -1;
            public MoveInput aim = new();
            public bool Attack => false;
            public int Slot
            {
                get
                {
                    int pressed = slot;
                    slot = -1;
                    return pressed;
                }
            }
            public void Orient(float yaw, float pitch) { }
            public MoveInput Sample() => aim;
            public bool Held(Bind action) => held == action;
            public bool Pressed(Bind action)
            {
                if (next != action) return false;
                next = null;
                return true;
            }
        }

        [MenuItem("Tools/Prototype/Validate rules, TCP and Play Mode")]
        private static void ValidateInEditor()
        {
            Domain();
            Wire();
            Debug.Log("PROTOTYPE RULES AND TCP PASSED: trade, crafting, late entry, reconnect, dawn, save");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool(PlayCheck, true);
            ResumePlayCheck();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Prototype/Validate rules, TCP and Play Mode", true)]
        private static bool CanValidateInEditor() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [InitializeOnLoadMethod]
        private static void ResumePlayCheck()
        {
            if (!SessionState.GetBool(PlayCheck, false)) return;
            stage = 0;
            gestureAt = 0;
            deadline = EditorApplication.timeSinceStartup + 20;
            EditorApplication.update += CheckPlay;
        }

        private static void CheckPlay()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Play Mode validation timed out at stage " + stage);

                PrototypeGame game = UnityEngine.Object.FindAnyObjectByType<PrototypeGame>();
                if (game == null) return;
                if (stage == 0)
                {
                    Check(HasButton("menu.solo"), "rendered start screen");
                    runtime = GameSession.Host(new Balance(), TestSave());
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(PrototypeGame).GetField("session", flags).SetValue(game, runtime);
                    typeof(PrototypeGame).GetMethod("StartPlaying", flags).Invoke(game, null);
                    simulatedInput = new TestInput
                    {
                        next = Bind.Interact,
                        aim = Aim(runtime.LocalPlayer, ShopTargets.CratePosition(0), new Vector3(-7, 0, 1.15f))
                    };
                    game.SetGameplayInput(simulatedInput);
                    stage++;
                }
                else if (stage == 1 && runtime.LocalPlayer.HoldingCrate)
                {
                    simulatedInput.aim = AimStation(runtime.LocalPlayer, Site.Shelf);
                    simulatedInput.next = Bind.Interact;
                    stage++;
                }
                else if (stage == 2 && runtime.State.shelf.Used == 1)
                {
                    simulatedInput.next = simulatedInput.held = Bind.Interact;
                    holdUntil = EditorApplication.timeSinceStartup + 1;
                    stage++;
                }
                else if (stage == 3 && runtime.State.shelf.Used == 8)
                {
                    simulatedInput.held = null;
                    runtime.Command(ActionKind.DropCrate);
                    Check(runtime.State.dropped.Count == 1 && !runtime.LocalPlayer.HoldingCrate, "throw frees both hands");
                    InventoryLayout.Add(runtime.LocalPlayer, (int)Item.Herb, 2);
                    simulatedInput.aim = AimStation(runtime.LocalPlayer, Site.Bench);
                    simulatedInput.next = Bind.Interact;
                    stage++;
                }
                else if (stage == 3 && EditorApplication.timeSinceStartup > holdUntil)
                    throw new InvalidOperationException("Holding interact did not stock the crate.");
                else if (stage == 4 && HasButton("bench.begin"))
                {
                    UnityEngine.UI.Button plus = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>()
                        .First(b => b.interactable && b.GetComponentInChildren<UnityEngine.UI.Text>()?.text == "+");
                    plus.onClick.Invoke();
                    Check(!FindButton("bench.begin").interactable, "one ingredient cannot start a brew");
                    plus.onClick.Invoke();
                    FindButton("bench.begin").onClick.Invoke();
                    stage++;
                }
                else if (stage >= 5 && stage <= 10 && runtime.State.craft != null)
                {
                    WorkbenchView bench = Workbench(game);
                    bench.PointerOverride ??= () =>
                    {
                        var current = pointer;
                        pointer.pressed = false;
                        return current;
                    };
                    StationSession brew = runtime.State.craft;
                    if (stage == 5 || stage == 7 || stage == 8)
                    {
                        int index = stage == 8 ? 1 : 0;
                        ReagentStage expected = stage == 5 ? ReagentStage.Mortar : ReagentStage.Pot;
                        if (brew.pieces[index].stage == expected)
                        {
                            stage++;
                            gestureAt = 0;
                            return;
                        }

                        GameObject piece = GameObject.Find("Reagent " + index);
                        GameObject destination = GameObject.Find(stage == 5 ? "Mortar zone" : "Cauldron zone");
                        if (piece == null || destination == null || piece.transform.position.x < 45) return;
                        Vector3 target = destination.transform.position + Vector3.up * 0.31f;
                        if (gestureAt == 0)
                        {
                            gestureAt = EditorApplication.timeSinceStartup;
                            pointer = (CameraRay(piece.transform.position), true, true);
                            if (Application.isBatchMode && stage == 5) Capture("PrototypeReagents");
                            return;
                        }

                        pointer = (CameraRay(target), false, EditorApplication.timeSinceStartup - gestureAt < 0.74);
                        if (!pointer.held && EditorApplication.timeSinceStartup - gestureAt > 1.6)
                            throw new InvalidOperationException($"Reagent {index} missed {destination.name}: {piece.transform.position}, target {target}.");
                    }
                    else if (stage == 6)
                    {
                        if (brew.pieces[0].stage == ReagentStage.Ground)
                        {
                            stage++;
                            gestureAt = 0;
                            pointer.held = false;
                            return;
                        }

                        GameObject pestle = GameObject.Find("Grinding pestle");
                        if (pestle == null) return;
                        if (gestureAt == 0)
                        {
                            gestureAt = EditorApplication.timeSinceStartup;
                            pointer = (CameraRay(pestle.transform.position + Vector3.up * 0.49f), true, true);
                            if (Application.isBatchMode) Capture("PrototypeWorkbench");
                            return;
                        }

                        Vector3 center = GameObject.Find("Mortar zone").transform.position;
                        float beat = (float)(EditorApplication.timeSinceStartup - gestureAt) % 0.86f;
                        pointer = (CameraRay(center + Vector3.up * (beat < 0.43f ? 1f : 0.49f)), false, true);
                    }
                    else if (stage == 9 && FindButton("craft.finish") != null && GameObject.Find("Stirring paddle") != null)
                    {
                        GameObject paddle = GameObject.Find("Stirring paddle");
                        if (gestureAt == 0)
                        {
                            gestureAt = EditorApplication.timeSinceStartup;
                            if (Application.isBatchMode)
                                Capture("PrototypeCauldron");
                            pointer = (CameraRay(paddle.transform.position + Vector3.up * 0.31f), true, true);
                            return;
                        }
                        Vector3 center = GameObject.Find("Cauldron zone").transform.position + Vector3.up * 0.23f;
                        float angle = (float)(EditorApplication.timeSinceStartup - gestureAt) * 6f;
                        Vector3 cursor = center + new Vector3(Mathf.Cos(angle) * 0.27f, Mathf.Sin(angle) * 0.23f, 0);
                        pointer = (CameraRay(cursor), false, true);
                        if (brew.stir >= 2.1f)
                        {
                            pointer.held = false;
                            stage++;
                        }
                    }
                    else if (stage == 10 && FindButton("craft.finish")?.interactable == true)
                    {
                        FindButton("craft.finish").onClick.Invoke();
                        Check(runtime.State.craft == null && runtime.State.warehouse.Any(c => c.quality >= 80),
                            "finished potion from physically moved paddle");
                        simulatedInput.next = Bind.Inventory;
                        stage++;
                    }
                }
                else if (stage == 11 && HasButton("panel.close"))
                {
                    InventoryCell[] cells = UnityEngine.Object.FindObjectsByType<InventoryCell>();
                    Check(cells.Length == 48, "40 backpack, four unlocked belt and four armor cells");
                    if (Application.isBatchMode)
                        Capture("PrototypeInventory");
                    InventoryCell trap = cells.First(c => c.Index < InventoryLayout.BackpackSize && c.Item == (int)Item.Trap);
                    trap.OnPointerEnter(null);
                    simulatedInput.slot = 3;
                    stage++;
                }
                else if (stage == 12 && runtime.LocalPlayer.Quick(3)?.item == (int)Item.Trap)
                {
                    InventoryCell[] cells = UnityEngine.Object.FindObjectsByType<InventoryCell>();
                    InventoryCell shortcut = cells.First(c => c.Index == InventoryLayout.HotbarStart + 3);
                    if (shortcut.Item < 0) return;
                    InventoryCell destination = cells.First(c => c.Index == 6);
                    destination.OnDrop(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {
                        pointerDrag = shortcut.gameObject
                    });
                    Check(runtime.LocalPlayer.Quick(3) == null && InventoryLayout.At(runtime.LocalPlayer, 15)?.item == (int)Item.Trap,
                        "hover key assigns and drop back clears shortcut");
                    FindButton("panel.close").onClick.Invoke();
                    PlayerState player = runtime.LocalPlayer;
                    runtime.Move(AimStation(player, Site.Sign));
                    runtime.Command(ActionKind.Close);
                    player.position = Map.SitePosition(Site.Gate);
                    runtime.Command(ActionKind.Enter);
                    player.position = runtime.State.raid.enemies[0].position - Vector3.forward * 4;
                    simulatedInput.aim = new MoveInput { crouch = true, block = true };
                    holdUntil = EditorApplication.timeSinceStartup + 0.6;
                    stage++;
                }
                else if (stage == 13 && EditorApplication.timeSinceStartup > holdUntil)
                {
                    Check(runtime.LocalPlayer.crouched && runtime.LocalPlayer.guarding &&
                        Camera.main.transform.position.y < 1.25f && GameObject.Find("Equipped shield") != null,
                        "crouch lowers the camera and the equipped shield animates");
                    var settled = runtime.State.dropped.Single();
                    Check(settled.position.y < 1.25f && settled.velocity.sqrMagnitude < 0.01f, "rigidbody crate settles");
                    if (Application.isBatchMode) Capture("PrototypeRaid");
                    Debug.Log("PROTOTYPE PLAY PASSED: individual reagents, pestle, stirring, inventory, crouch and shield");
                    EndPlayCheck(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EndPlayCheck(false);
            }
        }

        private static WorkbenchView Workbench(PrototypeGame game)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Blockout view = (Blockout)typeof(PrototypeGame).GetField("view", flags).GetValue(game);
            return (WorkbenchView)typeof(Blockout).GetField("workbench", flags).GetValue(view);
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1440, 900, 24);
            RenderTexture cameraTarget = camera.targetTexture;
            Texture2D frame = new(1440, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.04f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                File.WriteAllBytes(Path.GetFullPath("Logs/" + name + ".png"), frame.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = cameraTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(frame);
            }
        }

        private static Ray CameraRay(Vector3 point) =>
            new(Camera.main.transform.position, (point - Camera.main.transform.position).normalized);

        private static MoveInput Aim(PlayerState player, Vector3 point, Vector3 position)
        {
            player.position = position;
            Vector3 angles = Quaternion.LookRotation(point - position - Vector3.up * 1.62f).eulerAngles;
            return new MoveInput { yaw = angles.y, pitch = Mathf.DeltaAngle(0, angles.x) };
        }

        private static MoveInput AimStation(PlayerState player, Site site)
        {
            Vector3 point = ShopTargets.Station(site).center;
            return Aim(player, point, new Vector3(point.x, 0, point.z - 1.3f));
        }

        private static bool HasButton(string key) => FindButton(key) != null;
        private static UnityEngine.UI.Button FindButton(string key)
        {
            string title = new Localization().Text(key);
            return UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>()
                .FirstOrDefault(b => b.GetComponentInChildren<UnityEngine.UI.Text>()?.text == title);
        }

        private static void EndPlayCheck(bool passed)
        {
            EditorApplication.update -= CheckPlay;
            SessionState.EraseBool(PlayCheck);
            EditorApplication.isPlaying = false;
            if (SessionState.GetBool(BatchCheck, false))
            {
                SessionState.EraseBool(BatchCheck);
                EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        public static void RunPlay()
        {
            SessionState.SetBool(BatchCheck, true);
            try { ValidateInEditor(); }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void Run()
        {
            try
            {
                Domain();
                Wire();
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
                Check(scene.GetRootGameObjects().Any(root => root.GetComponent<PrototypeGame>() != null), "scene bootstrap");
                Check(Camera.main != null, "main camera");
                Check(UnityEngine.InputSystem.InputSystem.actions.FindActionMap("Player") != null, "input configuration");
                Check(new Localization().Text("title") != "title", "localized resources");
                Debug.Log("PROTOTYPE SMOKE PASSED: trade, crafting, late entry, loot, dawn, round-trip save, TCP, scene wiring");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Domain()
        {
            UnityEngine.Random.InitState(4821);
            Balance balance = new();
            GameWorld world = new(balance);
            Check(world.Connect("a", true) && world.Connect("b", false), "two players");
            var a = world.State.players[0];
            var b = world.State.players[1];
            WorldState state = world.State;

            void Go(PlayerState player, Site site) => player.position = Map.SitePosition(site);
            void Do(PlayerState player, ActionKind kind, int value = 0, int extra = 0, bool repeat = false,
                int[] ingredients = null) =>
                world.Execute(player.id, new GameAction { kind = kind, value = value, extra = extra,
                    operation = player.lastOperation + (repeat ? 0 : 1), ingredients = ingredients });
            void Advance(float seconds)
            {
                for (float elapsed = 0; elapsed < seconds; elapsed += 0.04f) world.Tick(0.04f);
            }

            void StirFor(int session, float seconds)
            {
                Advance(0.2f);
                for (int i = 0; i < Mathf.RoundToInt(seconds * 10); i++)
                {
                    Advance(0.1f);
                    Do(a, ActionKind.Stir, session, 100);
                }
            }

            Crate tonic = state.warehouse[0];
            Vector3 aisle = new(-7, 0, 1.15f);
            world.ReceiveMove(a.id, Aim(a, ShopTargets.CratePosition(tonic.rackSlot), aisle));
            world.ReceiveMove(b.id, Aim(b, ShopTargets.CratePosition(tonic.rackSlot), aisle));
            Do(a, ActionKind.CarryCrate, tonic.id);
            Do(b, ActionKind.CarryCrate, tonic.id);
            Check(a.HoldingCrate && !b.HoldingCrate && !state.warehouse.Contains(tonic), "crate pickup is exclusive");

            world.ReceiveMove(a.id, AimStation(a, Site.Shelf));
            Do(a, ActionKind.Stock, tonic.id, 1);
            Do(a, ActionKind.Stock, tonic.id, 1, true);
            Check(state.shelf.Used == 1, "short stock and duplicate operation");
            Do(a, ActionKind.Stock, tonic.id);
            Check(state.shelf.Used == 8 && tonic.count == 0, "hold stocks the remaining units");

            Vector3 rack = new(-8.22f, 0.6f, 1.15f);
            world.ReceiveMove(a.id, AimStation(a, Site.Bin));
            Do(a, ActionKind.DiscardCrate, tonic.id);
            Check(!a.HoldingCrate && a.carriedCrate == null, "discard bin removes the held crate");

            Crate sword = new() { id = state.nextId++, item = (int)Item.Blade, count = 2, rackSlot = 0 };
            state.warehouse.Add(sword);
            world.ReceiveMove(b.id, Aim(b, ShopTargets.CratePosition(sword.rackSlot), aisle));
            Do(b, ActionKind.CarryCrate, sword.id);
            world.ReceiveMove(b.id, AimStation(b, Site.Shelf));
            Do(b, ActionKind.Stock, sword.id);
            Check(state.shelf.Used == 12 && state.shelf.stock.Count == 2 && sword.count == 1, "mixed weighted shelf capacity");
            world.ReceiveMove(b.id, Aim(b, rack, aisle));
            Do(b, ActionKind.PlaceCrate, sword.id);
            Check(!b.HoldingCrate && state.warehouse.Contains(sword), "remaining goods stay in the returned crate");

            Go(a, Site.Shelf);
            ShelfStock display = state.shelf.stock.First(s => s.item == tonic.item);
            Do(a, ActionKind.Price, tonic.item, 13);
            Check(GameWorld.ShelfPrice(state, display) == 13, "manual price");
            Do(a, ActionKind.Price, tonic.item, -13);
            Check(GameWorld.ShelfPrice(state, display) == state.market[(int)Item.Tonic], "market price");

            world.ReceiveMove(a.id, AimStation(a, Site.Sign));
            Do(a, ActionKind.Open);
            Advance(6.5f);
            Check(state.customer?.phase == CustomerPhase.Checkout && state.customer.cart.Count == 2, "mixed cart at checkout");
            Customer customer = state.customer;
            int price = customer.price;
            Check(Money.Stacks(20).SequenceEqual(new[] { 10, 10 }) &&
                Money.Stacks(10205).SequenceEqual(new[] { Money.Gold, 2 * Money.Silver, 5 }), "currency and stacks");
            void AimAtStack(PlayerState collector, int index)
            {
                Vector3 point = ShopTargets.CoinPosition(index);
                world.ReceiveMove(collector.id, Aim(collector, point, new Vector3(point.x, 0, point.z - 0.65f)));
            }

            AimAtStack(a, 0);
            Do(a, ActionKind.Sell, customer.id);
            AimAtStack(b, 0);
            Do(b, ActionKind.Sell, customer.id);
            Check(customer.collected == 1 && state.money == 120 && state.shelf.Used == 12, "partial payment and duplicate stack");
            WorldState partialSave = WorldSave.Decode(JsonUtility.ToJson(state));
            Check(partialSave.customer.collected == 1 && partialSave.money == 120, "partial checkout persists");
            world.ReceiveMove(a.id, AimStation(a, Site.Sign));
            Do(a, ActionKind.Close);
            Check(state.phase == DayPhase.Trading, "partial payment prevents closing");
            for (int i = 1; i < customer.payment.Length; i++)
            {
                AimAtStack(a, i);
                Do(a, ActionKind.Sell, customer.id, i);
            }
            Check(state.money == 120 + price && state.shelf.Used == 7 && customer.phase == CustomerPhase.Leaving,
                "final stack commits money and cart exactly once");

            Go(a, Site.Supplier);
            Do(a, ActionKind.Order, (int)Item.Herb);
            Do(a, ActionKind.Order, (int)Item.Herb, 0, true);
            Check(state.deliveries.Count == 1 && state.deliveries[0].morning == 2, "pending paid delivery");
            int delivery = state.deliveries[0].crate.id;
            int paid = state.deliveries[0].paid;
            Check(state.money == 120 + price - paid, "paid exactly once");

            Crate herb = state.warehouse.Find(c => c.item == (int)Item.Herb);
            world.ReceiveMove(a.id, Aim(a, ShopTargets.CratePosition(herb.rackSlot),
                new Vector3(-7, 0, ShopTargets.CratePosition(herb.rackSlot).z)));
            Do(a, ActionKind.TakeCrate, herb.id);
            Do(a, ActionKind.TakeCrate, herb.id);
            int[] mix = new int[Catalog.Count];
            mix[(int)Item.Herb] = 2;
            Go(a, Site.Bench);
            Go(b, Site.Bench);
            Do(a, ActionKind.BeginCraft);
            Check(state.craft == null, "reject missing ingredients");
            Do(a, ActionKind.BeginCraft, ingredients: mix);
            Do(b, ActionKind.BeginCraft, ingredients: mix);
            Check(state.craft.owner == a.id, "exclusive bench");
            world.Disconnect(a.id);
            Check(state.craft == null && a.Count((int)Item.Herb) == 2, "disconnect cancels craft");
            Check(world.Connect(a.id, true), "reconnect");
            Do(a, ActionKind.BeginCraft, ingredients: mix);
            int craft = state.craft.id;
            Do(a, ActionKind.GrindIngredient, craft, 0);
            Check(state.craft.pieces[0].presses == 0, "pestle needs a reagent in the mortar");
            Do(a, ActionKind.AddIngredient, craft, 0);
            Do(a, ActionKind.AddIngredient, craft, 0);
            Check(state.craft.step == CraftStep.Ingredients, "each piece can only be added once");
            Do(a, ActionKind.AddIngredient, craft, 1);
            Do(a, ActionKind.FinishCraft, craft);
            Check(state.craft != null, "host verifies stir time");
            StirFor(craft, 2.1f);
            Do(a, ActionKind.FinishCraft, craft);
            Do(a, ActionKind.FinishCraft, craft);
            Check(state.craft == null && state.lastCraft.Length == 3 && a.Count((int)Item.Herb) == 0 &&
                state.warehouse.Count(c => c.item == (int)Item.Tonic && c.quality == 100) == 1, "one crafted output");

            InventoryLayout.Add(a, (int)Item.Ore, 4);
            mix[(int)Item.Herb] = 0;
            mix[(int)Item.Ore] = 2;
            foreach (float duration in new[] { 0.5f, 2.1f })
            {
                Do(a, ActionKind.BeginCraft, ingredients: mix);
                craft = state.craft.id;
                for (int i = 0; i < 2; i++)
                {
                    if (duration > 1)
                    {
                        Do(a, ActionKind.CraftZone, craft, i);
                        for (int press = 0; press < GameWorld.PressesToGrind; press++)
                        {
                            Advance(0.22f);
                            Do(a, ActionKind.GrindIngredient, craft, i);
                            Check(state.craft.pieces[i].ground == (press == GameWorld.PressesToGrind - 1),
                                "three spaced strokes are required");
                        }
                    }
                    Do(a, ActionKind.AddIngredient, craft, i);
                }
                StirFor(craft, duration);
                Do(a, ActionKind.FinishCraft, craft);
            }
            Check(state.warehouse.Any(c => c.item == (int)Item.Ward && c.quality == 50) &&
                state.warehouse.Any(c => c.item == (int)Item.Focus && c.quality == 100),
                "raw and ground ore have different effects");

            Go(a, Site.Shelf);
            Do(a, ActionKind.Price, tonic.item, 17);
            world.ReceiveMove(a.id, AimStation(a, Site.Sign));
            Do(a, ActionKind.Close);
            Go(a, Site.Gate);
            Do(a, ActionKind.Enter);
            int raidId = state.raid.id;
            var enemy = state.raid.enemies[0];
            a.position = enemy.position + Vector3.forward * 3.5f;
            world.ReceiveMove(a.id, new MoveInput { crouch = true, jump = true });
            Advance(0.05f);
            Check(a.crouched && a.EyeHeight < 1.62f && a.position.y == 0 &&
                !GameWorld.CanDetect(enemy, a, 3.5f) && GameWorld.CanDetect(enemy, a, 2f),
                "crouching lowers the player and hides outside close range behind a guard");
            a.position = enemy.position - Vector3.forward * 1.6f;
            world.ReceiveMove(a.id, new MoveInput());
            Advance(0.05f);
            for (int i = 0; i < 3; i++)
            {
                Do(a, ActionKind.Strike);
                Check(a.swing > 0, "accepted strike starts visible swing");
                Advance(0.66f);
            }
            Check(enemy.phase == EnemyPhase.Defeated, "authoritative melee");
            a.position = enemy.position;
            Do(a, ActionKind.Loot, 0);
            Do(a, ActionKind.Loot, 0);
            Check(a.Count((int)Item.Ore) == 1 && a.inventory.Find(s => s.item == (int)Item.Ore).unsecured == 1, "loot unique");
            Check(b.zone == Zone.Shop, "split party");
            Go(b, Site.Supplier);
            Do(b, ActionKind.Order, (int)Item.Herb);
            Go(b, Site.Gate);
            Do(b, ActionKind.Enter);
            Check(state.raid.id == raidId && enemy.looted && b.position == Map.RaidSpawn, "shared late entry");
            Go(a, Site.Exit);
            Do(a, ActionKind.Exit);
            Go(a, Site.Bed);
            Do(a, ActionKind.Sleep);
            Check(a.night == NightChoice.Raiding && !a.ready, "raid prevents sleep");
            Do(a, ActionKind.Ready);
            Check(state.day == 1, "no dawn with active explorer");
            Go(b, Site.Exit);
            Do(b, ActionKind.Exit);
            Do(b, ActionKind.Ready);
            Check(state.day == 2 && state.warehouse.Count(c => c.id == delivery) == 1 &&
                state.deliveries.Count == 0 && GameWorld.ShelfPrice(state, display) == 17, "single dawn, fixed price and delivery");

            WorldState restored = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(state));
            world = new GameWorld(balance, restored);
            Check(world.Connect(a.id, true), "load reconnect");
            Check(world.State.day == 2 && world.State.money == state.money && world.State.raid == null &&
                world.State.warehouse.Count(c => c.id == delivery) == 1, "save round trip");

            const string legacy = "{\"version\":1,\"day\":3,\"money\":107,\"nextId\":8,\"players\":[],\"warehouse\":[]," +
                "\"market\":[14,6,24,18,25,12,42,10,36,24],\"shelf\":{\"item\":2,\"count\":7,\"manualPrice\":31,\"marketPrice\":false}}";
            WorldState upgraded = WorldSave.Decode(legacy);
            Check(upgraded.version == WorldState.CurrentVersion && upgraded.money == 107 && upgraded.shelf.Used == 12 &&
                upgraded.shelf.stock[0].manualPrice == 31 && upgraded.warehouse.Single().count == 4, "version 1 shelf migrates without loss");
            a = world.State.players[0];
            InventoryLayout.Add(a, (int)Item.Boots, 1);
            int boots = a.inventory.Find(s => s.item == (int)Item.Boots).cell;
            int feet = InventoryLayout.ArmorStart + (int)ArmorSlot.Feet;
            Do(a, ActionKind.MoveInventory, boots, InventoryLayout.ArmorStart + (int)ArmorSlot.Head);
            Check(InventoryLayout.At(a, boots)?.item == (int)Item.Boots, "reject wrong armor slot");
            Do(a, ActionKind.MoveInventory, boots, feet);
            Do(a, ActionKind.MoveInventory, feet, 39);
            Check(InventoryLayout.At(a, feet)?.item == (int)Item.Boots, "two-cell boots cannot straddle grid edge");
            Do(a, ActionKind.MoveInventory, feet, 38);
            Check(InventoryLayout.At(a, 39)?.cell == 38 && a.Count((int)Item.Boots) == 1, "both squares represent one item");
            InventoryLayout.Add(a, (int)Item.Greatblade, 1);
            Do(a, ActionKind.MoveInventory, a.inventory.Find(s => s.item == (int)Item.Greatblade).cell, InventoryLayout.HotbarStart);
            Check(InventoryLayout.TwoHands(a) && !a.HasShield && a.Count((int)Item.Shield) == 1, "two hands safely stow the shield");
            Go(a, Site.Supplier);
            world.State.money = 200;
            Do(a, ActionKind.ExpandBelt);
            WorldState layoutSave = WorldSave.Decode(JsonUtility.ToJson(world.State));
            Check(layoutSave.players[0].quickSlots == 5 && world.State.money == 100, "purchased belt persists");
        }

        private static WorldSave TestSave() => new(Guid.NewGuid().ToString("N"),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "PrototypeSmoke")));

        private static void Wire()
        {
            WorldSave save = TestSave();
            string guestId = Guid.NewGuid().ToString("N");
            using GameSession host = GameSession.Host(new Balance(), save, new TcpTransport());
            using GameSession guest = GameSession.Join("127.0.0.1", guestId, new TcpTransport());
            void Until(GameSession client, Func<bool> passed, string context)
            {
                for (int i = 0; i < 350 && !passed(); i++)
                {
                    host.Tick(0.01f);
                    client?.Tick(0.01f);
                    Thread.Sleep(5);
                }
                Check(passed(), context);
            }

            Until(guest, () => guest.LocalPlayer != null, "network join and snapshot");
            PlayerState visitor = host.State.players.Single(p => p.id == guestId);
            Crate crate = host.State.warehouse[0];
            guest.Move(Aim(visitor, ShopTargets.CratePosition(crate.rackSlot), new Vector3(-7, 0, 1.15f)));
            guest.Command(ActionKind.CarryCrate, crate.id);
            Until(guest, () => guest.LocalPlayer.HoldingCrate, "guest picks up replicated crate");
            guest.Move(AimStation(visitor, Site.Shelf));
            guest.Command(ActionKind.Stock, crate.id, 1);
            Until(guest, () => guest.State.shelf.Used == 1, "guest mutation through the session");
            host.Move(AimStation(host.LocalPlayer, Site.Sign));
            host.Command(ActionKind.Close);
            host.LocalPlayer.position = Map.SitePosition(Site.Gate);
            host.Command(ActionKind.Enter);
            int raidId = host.State.raid.id;
            host.State.raid.enemies[0].phase = EnemyPhase.Defeated;
            host.State.raid.enemies[0].looted = true;
            Until(guest, () => guest.State.raid?.id == raidId && guest.LocalPlayer.zone == Zone.Shop, "split zones");
            guest.Command(ActionKind.DropCrate);
            Until(guest, () => guest.State.dropped.Exists(c => c.id == crate.id), "thrown crate replicates to guest");
            visitor.position = Map.SitePosition(Site.Gate);
            guest.Command(ActionKind.Enter);
            Until(guest, () => guest.LocalPlayer.zone == Zone.Raid, "guest late entry");
            Check(guest.State.raid.id == raidId && guest.State.raid.enemies[0].looted, "same changed raid snapshot");

            guest.Dispose();
            Until(null, () => !visitor.online, "disconnect recognized");
            using GameSession reconnect = GameSession.Join("127.0.0.1", guestId, new TcpTransport());
            Until(reconnect, () => reconnect.LocalPlayer != null, "same guest reconnects");
            Check(reconnect.LocalPlayer.zone == Zone.Raid && reconnect.State.raid.id == raidId, "reconnect preserves raid");
            visitor.position = Map.SitePosition(Site.Exit);
            reconnect.Command(ActionKind.Exit);
            Until(reconnect, () => reconnect.LocalPlayer.zone == Zone.Shop, "operation sequence resumes on reconnect");
            Check(save.Load() != null, "host writes checkpoint");
        }

        private static void Check(bool condition, string context)
        {
            if (!condition) throw new InvalidOperationException("Smoke failed: " + context);
        }
    }
}
#endif
