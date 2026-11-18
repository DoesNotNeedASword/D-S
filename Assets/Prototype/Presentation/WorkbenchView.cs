using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonsNShops
{
    internal sealed class WorkbenchView
    {
        private readonly Camera camera;
        private readonly Transform root;
        private sealed class Ingredient
        {
            public Rigidbody body;
            public Transform model;
            public Vector3 tray;
            public ReagentStage stage = (ReagentStage)(-1);
            public float pending, depth;
            public bool falling;
        }

        private readonly MedievalModels models;
        private readonly List<Ingredient> pieces = new();
        private readonly Rigidbody paddle, pestle;
        private readonly Vector3 origin, supply, mortar, pot;
        private Vector3 anchor, grabOffset, previousPaddle;
        private Vector2 paddleCursor;
        private bool grinding, stirring, pestleRaised;
        private int sessionId, grabbed = -1, mortarIndex = -1;
        private float pestleTarget, nextPestle;
        private float pendingStir;
        internal Func<(Ray ray, bool pressed, bool held)> PointerOverride { get; set; }

        public WorkbenchView(Transform root, Camera camera, MedievalModels models)
        {
            this.camera = camera;
            this.root = root;
            this.models = models;
            origin = new Vector3(50, 0, 2);
            supply = origin + new Vector3(1.72f, 0.2f, 0);
            mortar = origin + new Vector3(0.02f, 0.31f, 0);
            pot = origin + new Vector3(-1.65f, 0.48f, 0);
            models.Part(root, "Plaster wall", origin + new Vector3(0, 1.4f, 2.45f), new Vector3(15, 4.6f, 0.24f),
                new Color(0.48f, 0.38f, 0.27f));
            models.Part(root, "Workshop floor", origin + Vector3.down * 0.88f, new Vector3(13, 0.1f, 10), MedievalModels.Oak);
            models.Part(root, "Workshop crossbeam", origin + new Vector3(0, 1.65f, 2.24f),
                new Vector3(15, 0.22f, 0.28f), MedievalModels.Oak);
            for (int i = -2; i <= 2; i++)
                models.Part(root, "Workshop timber", origin + new Vector3(i * 2.35f, 1.4f, 2.24f),
                    new Vector3(0.18f, 4.6f, 0.25f), MedievalModels.Oak);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shelf = origin + new Vector3(side * 2.85f, 1.1f, 1.98f);
                models.Part(root, "Reagent shelf", shelf, new Vector3(1.35f, 0.095f, 0.53f), MedievalModels.Oak);
                for (int jar = 0; jar < 3; jar++)
                {
                    GameObject prop = new("Reagent bottle");
                    prop.transform.SetParent(root, false);
                    prop.transform.position = shelf + new Vector3((jar - 1) * 0.40f, 0.20f, -0.04f);
                    prop.transform.localScale = new Vector3(0.31f, 0.33f + jar * 0.055f, 0.30f);
                    models.ItemModel(prop.transform, (side + jar) % 2 == 0 ? Item.Focus : Item.Tonic);
                }
            }
            models.Table(root, origin + Vector3.down * 0.85f, new Vector3(4.8f, 0.85f, 1.75f), true);
            GameObject top = new("Table collision", typeof(BoxCollider));
            top.transform.SetParent(root);
            top.transform.position = origin + Vector3.down * 0.09f;
            top.GetComponent<BoxCollider>().size = new Vector3(4.8f, 0.18f, 1.75f);
            models.Vessel(root, "Mortar zone", mortar, false);
            models.Vessel(root, "Cauldron zone", pot, true);
            models.Vessel(root, "Ingredient tray", supply - Vector3.up * 0.18f, false, 0.7f);
            GameObject grinder = models.Paddle(root, mortar);
            grinder.name = "Grinding pestle";
            pestle = grinder.AddComponent<Rigidbody>();
            pestle.useGravity = false;
            pestle.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            pestle.interpolation = RigidbodyInterpolation.Interpolate;

            GameObject spoon = models.Paddle(root, pot);
            paddle = spoon.AddComponent<Rigidbody>();
            paddle.useGravity = false;
            paddle.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            paddle.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Show(StationSession session)
        {
            root.gameObject.SetActive(true);
            if (session.id != sessionId)
            {
                sessionId = session.id;
                ResetGesture();
                foreach (Ingredient ingredient in pieces) UnityEngine.Object.Destroy(ingredient.body.gameObject);
                pieces.Clear();
                for (int i = 0; i < session.pieces.Count; i++)
                {
                    ReagentPiece state = session.pieces[i];
                    GameObject item = new("Reagent " + i);
                    item.transform.SetParent(root, false);
                    Vector3 tray = supply + new Vector3((i % 3 - 1) * 0.31f, 0, (i / 3 - 0.5f) * 0.34f);
                    item.transform.position = tray;
                    BoxCollider collider = item.AddComponent<BoxCollider>();
                    collider.size = Vector3.one * 0.23f;
                    foreach (Ingredient existing in pieces)
                        Physics.IgnoreCollision(collider, existing.body.GetComponent<Collider>());
                    Rigidbody body = item.AddComponent<Rigidbody>();
                    body.mass = state.item == (int)Item.Herb ? 0.28f : state.item == (int)Item.Ore ? 0.66f : 0.46f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    Transform model = new GameObject("Reagent model").transform;
                    model.SetParent(item.transform, false);
                    model.localScale = Vector3.one * 0.27f;
                    models.ItemModel(model, (Item)state.item);
                    pieces.Add(new Ingredient
                    {
                        body = body, model = model,
                        tray = tray
                    });
                }
                ResetBody(pestle, mortar + Vector3.up * 0.24f);
                ResetBody(paddle, pot + Vector3.right * 0.17f);
                previousPaddle = paddle.position;
            }

            mortarIndex = -1;
            bool grindable = false;
            for (int i = 0; i < pieces.Count; i++)
            {
                Ingredient piece = pieces[i];
                ReagentPiece state = session.pieces[i];
                if (state.stage is ReagentStage.Mortar or ReagentStage.Ground) mortarIndex = i;
                if (state.stage == ReagentStage.Mortar) grindable = true;
                if (piece.stage == state.stage) continue;
                piece.stage = state.stage;
                piece.pending = 0;
                piece.body.gameObject.SetActive(state.stage != ReagentStage.Pot);
                if (state.stage == ReagentStage.Ground) piece.model.localScale = new Vector3(0.29f, 0.13f, 0.27f);
                if (state.stage != ReagentStage.Pot)
                {
                    piece.body.isKinematic = state.stage is ReagentStage.Mortar or ReagentStage.Ground;
                    ResetBody(piece.body, Rest(piece));
                }
                if (grabbed == i) grabbed = -1;
            }

            pestle.gameObject.SetActive(grindable);
            if (!grindable) grinding = false;
            paddle.gameObject.SetActive(session.step == CraftStep.Stir);
            camera.transform.SetPositionAndRotation(origin + new Vector3(0, 1.18f, -4.22f), Quaternion.Euler(8, 0, 0));
        }

        public void Hide()
        {
            sessionId = 0;
            ResetGesture();
            root.gameObject.SetActive(false);
        }

        public void Pointer(Action<ActionKind, int> submit)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null && PointerOverride == null) return;
            var (ray, pressed, held) = PointerOverride != null ? PointerOverride() :
                (camera.ScreenPointToRay(mouse.position.ReadValue()), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);
            if (paddle.gameObject.activeSelf)
            {
                MovePaddle(ray, pressed, held);
                int milliseconds = DrainStir();
                if (milliseconds > 0) submit(ActionKind.Stir, milliseconds);
                return;
            }

            if (pestle.gameObject.activeSelf && pressed &&
                new Bounds(pestle.position + Vector3.up * 0.45f, new Vector3(0.22f, 0.54f, 0.28f)).IntersectRay(ray))
            {
                grinding = true;
                pestleRaised = false;
            }

            if (grinding && CursorOnTable(ray, mortar.z, out Vector3 toolPoint))
                pestleTarget = Mathf.Clamp(toolPoint.y - 0.50f, mortar.y - 0.02f, mortar.y + 0.49f);
            if (!held) grinding = false;

            if (grinding && pestle.position.y > mortar.y + 0.25f)
                pestleRaised = true;
            if (grinding && pestleRaised && pestle.position.y < mortar.y + 0.085f && Time.unscaledTime >= nextPestle)
            {
                pestleRaised = false;
                nextPestle = Time.unscaledTime + 0.23f;
                submit(ActionKind.GrindIngredient, mortarIndex);
            }

            if (pressed && !grinding)
                BeginGrab(ray);
            if (grabbed >= 0)
            {
                Ingredient ingredient = pieces[grabbed];
                if (CursorOnTable(ray, ingredient.depth, out Vector3 point))
                {
                    point += grabOffset;
                    anchor = new Vector3(Mathf.Clamp(point.x, origin.x - 2.22f, origin.x + 2.22f),
                        Mathf.Clamp(point.y, origin.y + 0.14f, origin.y + 1.3f), ingredient.depth);
                }
                if (!held)
                {
                    ingredient.falling = true;
                    grabbed = -1;
                }
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                Ingredient ingredient = pieces[i];
                if (ingredient.pending > 0 && Time.unscaledTime > ingredient.pending)
                {
                    ingredient.pending = 0;
                    ResetBody(ingredient.body, Rest(ingredient));
                }

                if (!ingredient.falling || ingredient.stage == ReagentStage.Pot) continue;
                ActionKind? action = NearVessel(ingredient.body.position, pot) ? ActionKind.AddIngredient
                    : mortarIndex < 0 && ingredient.stage == ReagentStage.Tray && NearVessel(ingredient.body.position, mortar)
                        ? ActionKind.CraftZone : null;
                if (!action.HasValue) continue;
                ingredient.falling = false;
                ingredient.pending = Time.unscaledTime + 1.5f;
                submit(action.Value, i);
            }
        }

        private void BeginGrab(Ray ray)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < pieces.Count; i++)
            {
                Ingredient candidate = pieces[i];
                if (candidate.stage == ReagentStage.Pot || candidate.pending > 0 ||
                    !new Bounds(candidate.body.transform.position + Vector3.up * 0.04f, new Vector3(0.30f, 0.38f, 0.30f))
                        .IntersectRay(ray, out float distance) ||
                    distance >= nearest) continue;
                nearest = distance;
                grabbed = i;
            }

            if (grabbed < 0) return;
            Ingredient ingredient = pieces[grabbed];
            ingredient.body.isKinematic = false;
            ingredient.falling = false;
            ingredient.depth = ingredient.body.position.z;
            if (CursorOnTable(ray, ingredient.depth, out Vector3 point))
                grabOffset = ingredient.body.position - point;
            anchor = ingredient.body.position;
        }

        private Vector3 Rest(Ingredient ingredient) =>
            ingredient.stage == ReagentStage.Tray ? ingredient.tray : mortar + Vector3.up * 0.08f;

        private static bool NearVessel(Vector3 position, Vector3 vessel) =>
            Mathf.Abs(position.x - vessel.x) < 0.36f && Mathf.Abs(position.z - vessel.z) < 0.42f &&
            position.y < vessel.y + 0.27f && position.y > vessel.y - 0.18f;

        private void MovePaddle(Ray ray, bool pressed, bool held)
        {
            if (pressed && new Bounds(paddle.position + Vector3.up * 0.34f, new Vector3(0.29f, 0.80f, 0.28f)).IntersectRay(ray))
                stirring = true;
            if (!held) stirring = false;
            if (stirring && CursorOnTable(ray, origin.z, out Vector3 point))
            {
                Vector2 offset = new(point.x - pot.x, (point.y - pot.y - 0.23f) * 1.22f);
                paddleCursor = Vector2.ClampMagnitude(offset, 0.32f);
            }
        }

        private int DrainStir()
        {
            if (pendingStir < 0.105f && (stirring || pendingStir < 0.01f)) return 0;
            int milliseconds = Mathf.Min(250, Mathf.FloorToInt(pendingStir * 1000));
            pendingStir -= milliseconds / 1000f;
            return milliseconds;
        }

        public void FixedTick()
        {
            if (!root.gameObject.activeSelf) return;
            if (grabbed >= 0)
            {
                Rigidbody body = pieces[grabbed].body;
                body.AddForce(Vector3.ClampMagnitude((anchor - body.position) * 124 - body.linearVelocity * 14, 46));
            }

            foreach (Ingredient piece in pieces)
                if (piece.body.gameObject.activeSelf &&
                    (piece.body.position.y < origin.y - 0.65f || Mathf.Abs(piece.body.position.z - origin.z) > 1.2f))
                {
                    piece.falling = false;
                    ResetBody(piece.body, Rest(piece));
                }

            if (pestle.gameObject.activeSelf)
            {
                float targetY = grinding ? pestleTarget : mortar.y + 0.24f;
                pestle.AddForce(Vector3.up * ((targetY - pestle.position.y) * 145 - pestle.linearVelocity.y * 17),
                    ForceMode.Acceleration);
            }

            if (!paddle.gameObject.activeSelf) return;
            if (stirring)
            {
                Vector3 target = pot + new Vector3(paddleCursor.x, 0, paddleCursor.y);
                paddle.AddForce((target - paddle.position) * 95 - paddle.linearVelocity * 17, ForceMode.Acceleration);
                Vector3 previous = previousPaddle - pot;
                Vector3 current = paddle.position - pot;
                if (previous.sqrMagnitude > 0.012f && current.sqrMagnitude > 0.012f)
                {
                    float degrees = Mathf.Abs(Vector3.SignedAngle(previous, current, Vector3.up));
                    pendingStir += Mathf.Min(Time.fixedDeltaTime, degrees / 140f);
                }
            }
            else paddle.linearVelocity *= 0.74f;
            previousPaddle = paddle.position;
        }

        public void ResetGesture()
        {
            grabbed = -1;
            grinding = stirring = false;
            pendingStir = 0;
        }

        private static bool CursorOnTable(Ray ray, float z, out Vector3 point)
        {
            Plane plane = new(Vector3.back, new Vector3(0, 0, z));
            bool hit = plane.Raycast(ray, out float distance);
            point = hit ? ray.GetPoint(distance) : default;
            return hit;
        }

        private static void ResetBody(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.rotation = Quaternion.identity;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
