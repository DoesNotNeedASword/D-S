using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed class MedievalModels
    {
        public static readonly Color Oak = new(0.29f, 0.15f, 0.085f);
        public static readonly Color Wood = new(0.45f, 0.25f, 0.13f);
        public static readonly Color Brass = new(0.75f, 0.50f, 0.21f);
        public static readonly Color Iron = new(0.25f, 0.31f, 0.34f);
        public static readonly Color Cloth = new(0.23f, 0.34f, 0.31f);
        private static readonly Color Linen = new(0.76f, 0.66f, 0.47f);
        private static readonly Color Skin = new(0.65f, 0.43f, 0.30f);
        private readonly MaterialPropertyBlock colors = new();
        private readonly Dictionary<string, Mesh> meshes = new();
        private Material meshMaterial;

        public GameObject Part(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color,
            PrimitiveType primitive = PrimitiveType.Cube, float roll = 0)
        {
            GameObject piece = GameObject.CreatePrimitive(primitive);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = size;
            piece.transform.localRotation = Quaternion.Euler(0, 0, roll);
            Object.Destroy(piece.GetComponent<Collider>());
            Renderer renderer = piece.GetComponent<Renderer>();
            meshMaterial = renderer.sharedMaterial;
            Paint(renderer, color);
            return piece;
        }

        public void Paint(Renderer renderer, Color color)
        {
            colors.Clear();
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_Color", color);
            renderer.SetPropertyBlock(colors);
        }

        private static GameObject Group(Transform parent, string name, Vector3 position)
        {
            GameObject group = new(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = position;
            return group;
        }

        public void Table(Transform parent, Vector3 position, Vector3 size, bool bench)
        {
            Transform table = Group(parent, "Joiner's table", position).transform;
            table.localScale = size;
            for (int plank = 0; plank < 5; plank++)
                Part(table, "Top plank", new Vector3(0, 0.94f, (plank - 2) * 0.196f),
                    new Vector3(1, 0.12f, 0.192f), Color.Lerp(Oak, Wood, 0.46f + (plank % 3) * 0.17f));
            foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                    Part(table, "Tapered leg", new Vector3(x * 0.42f, 0.47f, z * 0.39f),
                        new Vector3(0.065f, 0.94f, 0.13f), Oak);
            Part(table, "Apron", new Vector3(0, 0.78f, -0.43f), new Vector3(0.9f, 0.16f, 0.06f), Wood);
            if (bench)
                Part(table, "Lower stretcher", new Vector3(0, 0.28f, 0.15f), new Vector3(0.91f, 0.08f, 0.12f), Oak);
        }

        public void Crate(Transform parent)
        {
            for (int y = -1; y <= 1; y++)
            {
                Part(parent, "Front plank", new Vector3(0, y * 0.325f, -0.505f), new Vector3(0.97f, 0.302f, 0.065f), Wood);
                Part(parent, "Rear plank", new Vector3(0, y * 0.325f, 0.505f), new Vector3(0.97f, 0.302f, 0.065f), Wood);
            }
            for (int i = -1; i <= 1; i += 2)
            {
                Part(parent, "Strap", new Vector3(i * 0.31f, 0, -0.553f), new Vector3(0.086f, 1.04f, 0.03f), Oak);
                Part(parent, "Lid strap", new Vector3(i * 0.31f, 0.525f, 0), new Vector3(0.087f, 0.03f, 1.07f), Oak);
                Part(parent, "Rivet", new Vector3(i * 0.31f, 0.31f, -0.58f), new Vector3(0.07f, 0.07f, 0.03f), Brass);
            }
            Part(parent, "Brass latch", new Vector3(0, 0.35f, -0.555f), new Vector3(0.14f, 0.15f, 0.045f), Brass);
        }

        public ActorMotion Person(Transform parent, Color tunic, int creature = -1)
        {
            bool goblin = creature is 0 or 1;
            bool fire = creature == 2;
            Color skin = goblin ? new Color(0.47f, 0.51f, 0.22f) : fire ? new Color(0.29f, 0.13f, 0.09f) : Skin;
            Color scarlet = new(0.48f, 0.19f, 0.14f);
            Color aubergine = new(0.37f, 0.20f, 0.27f);
            Color flame = new(1f, 0.38f, 0.055f);
            Color cloth = creature == 1 ? aubergine : fire ? scarlet : tunic;
            Transform rig = Group(parent, "Animated body", Vector3.zero).transform;
            Transform upper = Group(rig, "Torso", Vector3.zero).transform;
            Transform face = Group(upper, "Face", new Vector3(0, 0.52f, 0.035f)).transform;

            Lathe(upper, "Layered tunic", Vector3.zero, cloth, "tunic",
                new[] { (0.23f, -0.32f), (0.35f, -0.19f), (0.24f, 0.0f), (0.35f, 0.22f), (0.17f, 0.37f), (0.07f, 0.39f) });
            upper.Find("Layered tunic").localScale = new Vector3(1, 1, 0.65f);
            Part(upper, "Leather cross strap", new Vector3(0, 0.05f, 0.202f), new Vector3(0.13f, 0.51f, 0.042f), Oak, roll: -38);
            Part(upper, "Belt", new Vector3(0, -0.19f, 0), new Vector3(0.59f, 0.10f, 0.44f), Oak);
            Part(upper, "Buckle", new Vector3(0, -0.19f, 0.235f), new Vector3(0.14f, 0.10f, 0.043f), Brass);
            Part(upper, "Pouch", new Vector3(-0.26f, -0.25f, 0.17f), new Vector3(0.21f, 0.21f, 0.16f), Wood, roll: -12);
            Part(upper, "Scarf", new Vector3(0, 0.32f, 0.05f), new Vector3(0.48f, 0.11f, 0.35f),
                creature == 1 ? Brass : goblin ? scarlet : Linen);

            Lathe(face, "Faceted face", Vector3.zero, skin, "head",
                new[] { (0.10f, -0.15f), (0.23f, -0.09f), (0.28f, 0.06f), (0.23f, 0.24f), (0.10f, 0.28f) });
            face.Find("Faceted face").localScale = new Vector3(1, 1, 0.84f);
            Part(face, "Angular nose", new Vector3(0, 0.014f, 0.228f), new Vector3(0.095f, 0.11f, 0.13f), skin, roll: 10);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(face, "Eye", new Vector3(side * 0.115f, 0.071f, 0.211f), new Vector3(0.079f, 0.044f, 0.031f),
                    fire ? flame : Linen, roll: side * (creature >= 0 ? 15 : 0));
                if (!fire)
                    Part(face, "Pupil", new Vector3(side * 0.114f, 0.068f, 0.231f), new Vector3(0.029f, 0.043f, 0.021f), Oak);
                if (goblin)
                {
                    Spike(face, "Pointed ear", new Vector3(side * 0.25f, 0.095f, -0.028f),
                        new Vector3(0.65f, 1.18f, 0.44f), skin, side * -69);
                    Spike(face, "Tusk", new Vector3(side * 0.11f, -0.09f, 0.18f), Vector3.one * 0.27f, Linen, side * -7);
                }
            }

            if (creature == 1 || creature < 0)
            {
                Lathe(face, "Hood", new Vector3(0, 0.12f, -0.095f), creature == 1 ? aubergine : creature == -2 ? scarlet : Linen, "hood",
                    new[] { (0.29f, -0.14f), (0.34f, 0.04f), (0.25f, 0.23f), (0.14f, 0.29f), (0.0f, 0.32f) });
                face.Find("Hood").localScale = new Vector3(1, 0.83f, 0.72f);
                Part(upper, "Travel cape", new Vector3(0, -0.03f, -0.24f), new Vector3(0.60f, 0.69f, 0.09f), cloth);
                if (creature == -2)
                {
                    Part(upper, "Trader pack", new Vector3(0, 0.03f, -0.33f), new Vector3(0.49f, 0.50f, 0.25f), Wood);
                    GameObject roll = Part(upper, "Rolled blanket", new Vector3(0, 0.34f, -0.35f),
                        new Vector3(0.19f, 0.30f, 0.19f), Linen, PrimitiveType.Cylinder);
                    roll.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            else
            {
                for (int strand = -2; strand <= 2; strand++)
                    Spike(face, fire ? "Flame crown" : "Hair lock", new Vector3(strand * 0.093f, 0.20f, -0.035f),
                        new Vector3(0.65f, fire ? 1.1f + (2 - Mathf.Abs(strand)) * 0.29f : 0.73f, 0.72f),
                        fire ? Color.Lerp(flame, Brass, Mathf.Abs(strand) / 4f) : Oak, strand * -15 - 9);
            }

            Transform leftArm = null, rightArm = null, leftLeg = null, rightLeg = null;
            for (int side = -1; side <= 1; side += 2)
            {
                Transform arm = Group(upper, "Shoulder", new Vector3(side * 0.36f, 0.23f, 0)).transform;
                Transform leg = Group(rig, "Hip", new Vector3(side * 0.16f, -0.28f, 0)).transform;
                if (side < 0) { leftArm = arm; leftLeg = leg; } else { rightArm = arm; rightLeg = leg; }
                Part(arm, "Sleeve", new Vector3(0, -0.18f, 0), new Vector3(0.20f, 0.40f, 0.23f), cloth);
                Part(arm, "Bracer", new Vector3(0, -0.39f, 0.02f), new Vector3(0.23f, 0.22f, 0.24f), fire ? scarlet : Wood);
                Part(arm, "Hand", new Vector3(0, -0.53f, 0.06f), new Vector3(0.20f, 0.19f, 0.21f), fire ? flame : skin);
                Part(leg, "Trouser", new Vector3(0, -0.24f, 0), new Vector3(0.22f, 0.49f, 0.23f), Iron);
                Part(leg, "Boot", new Vector3(0, -0.61f, 0.074f), new Vector3(0.24f, 0.21f, 0.36f), Oak);
                Part(leg, "Boot cuff", new Vector3(0, -0.49f, 0.01f), new Vector3(0.24f, 0.13f, 0.26f), Wood);
                if (fire || creature == 0)
                    Spike(arm, "Pointed shoulder plate", new Vector3(side * 0.06f, 0, 0), new Vector3(1.13f, 0.85f, 1.2f),
                        fire ? scarlet : Iron, side * -42);
                if (fire)
                    Part(arm, "Ember seam", new Vector3(0, -0.28f, 0.137f), new Vector3(0.058f, 0.20f, 0.035f), flame, roll: side * 23);
            }

            if (creature == 1)
            {
                Part(rightArm, "Runed staff", new Vector3(0.065f, -0.29f, 0.12f), new Vector3(0.09f, 0.65f, 0.09f),
                    Wood, PrimitiveType.Cylinder);
                Spike(rightArm, "Staff crystal", new Vector3(0.065f, 0.41f, 0.12f), Vector3.one * 0.8f,
                    new Color(0.75f, 0.37f, 0.78f), 15);
            }
            if (fire)
            {
                Part(upper, "Obsidian chest plate", new Vector3(0, 0.07f, 0.205f), new Vector3(0.54f, 0.37f, 0.09f), Iron);
                Part(upper, "Glowing core", new Vector3(0, 0.08f, 0.262f), new Vector3(0.20f, 0.20f, 0.065f), flame, roll: 45);
            }
            return new ActorMotion(rig, upper, leftArm, rightArm, leftLeg, rightLeg);
        }

        private void Spike(Transform parent, string name, Vector3 position, Vector3 size, Color color, float roll)
        {
            Lathe(parent, name, position, color, "facet point",
                new[] { (0.0f, -0.18f), (0.13f, -0.08f), (0.12f, 0.055f), (0.0f, 0.30f) });
            Transform model = parent.GetChild(parent.childCount - 1);
            model.localScale = size;
            model.localRotation = Quaternion.Euler(0, 0, roll);
        }

        public void ItemModel(Transform parent, Item item)
        {
            switch (item)
            {
                case Item.Tonic:
                case Item.Focus:
                case Item.Ward:
                    Lathe(parent, "Potion glass", Vector3.zero, item == Item.Tonic ? new Color(0.24f, 0.66f, 0.58f)
                        : item == Item.Ward ? new Color(0.83f, 0.46f, 0.16f) : new Color(0.59f, 0.24f, 0.75f), "bottle", new[] { (0.24f, -0.48f), (0.42f, -0.31f),
                        (0.38f, 0.1f), (0.16f, 0.25f), (0.16f, 0.40f), (0.23f, 0.45f), (0.12f, 0.50f) });
                    Part(parent, "Cork", Vector3.up * 0.51f, new Vector3(0.24f, 0.07f, 0.24f), Wood, PrimitiveType.Cylinder);
                    Part(parent, "Neck band", Vector3.up * 0.37f, new Vector3(0.42f, 0.035f, 0.42f), Iron, PrimitiveType.Cylinder);
                    Part(parent, "Leather tag", new Vector3(0.03f, 0.04f, -0.35f), new Vector3(0.23f, 0.30f, 0.07f), Oak, roll: 15);
                    Part(parent, "Tag clasp", new Vector3(0.03f, 0.15f, -0.40f), Vector3.one * 0.095f, Brass, roll: 45);
                    break;
                case Item.Blade:
                case Item.Greatblade:
                    Lathe(parent, "Forged blade", new Vector3(-0.20f, 0, 0), new Color(0.69f, 0.70f, 0.69f), "blade",
                        new[] { (0.07f, 0f), (0.115f, 0.07f), (0.087f, 0.55f), (0, 0.74f) });
                    Transform blade = parent.Find("Forged blade");
                    blade.localRotation = Quaternion.Euler(0, 0, -90);
                    blade.localScale = new Vector3(1, 1, 0.43f);
                    Part(parent, "Crossguard", new Vector3(-0.21f, 0, 0), new Vector3(0.095f, 0.65f, 0.73f), Brass);
                    Part(parent, "Grip", new Vector3(-0.36f, 0, 0), new Vector3(0.23f, 0.32f, 0.23f), Oak);
                    break;
                case Item.Shield:
                    Shield(parent);
                    break;
                case Item.Helm:
                    Part(parent, "Forged shell", Vector3.zero, new Vector3(0.84f, 0.66f, 0.83f), Iron, PrimitiveType.Sphere);
                    Part(parent, "Boss", new Vector3(0, 0.31f, 0), new Vector3(0.39f, 0.25f, 0.39f), Brass, PrimitiveType.Sphere);
                    break;
                case Item.Herb:
                    for (int i = -1; i <= 1; i++)
                    {
                        Part(parent, "Leaf", new Vector3(i * 0.22f, 0.12f, 0), new Vector3(0.29f, 0.64f, 0.30f),
                            Color.Lerp(Cloth, new Color(0.51f, 0.60f, 0.24f), 0.6f), PrimitiveType.Sphere, i * -36);
                        for (int petal = 0; petal < 3; petal++)
                            Spike(parent, "Lavender bud", new Vector3(i * 0.16f, 0.33f + petal * 0.063f, -0.07f),
                                Vector3.one * (0.33f - petal * 0.037f), new Color(0.50f, 0.32f, 0.62f), i * -16 + petal * 7);
                    }
                    Part(parent, "Tied stem", new Vector3(0, -0.18f, 0), new Vector3(0.27f, 0.09f, 0.24f), Brass);
                    break;
                case Item.Ore:
                case Item.Ember:
                    for (int i = -1; i <= 1; i++)
                        Part(parent, "Mineral", new Vector3(i * 0.24f, 0.04f, i % 2 * 0.18f),
                            new Vector3(0.39f, 0.66f - Mathf.Abs(i) * 0.18f, 0.42f),
                            item == Item.Ember ? new Color(0.81f, 0.27f, 0.12f) : Iron, PrimitiveType.Cube, i * 29 + 12);
                    break;
                case Item.Trap:
                    Part(parent, "Spring plate", Vector3.down * 0.14f, new Vector3(0.8f, 0.21f, 0.81f), Wood);
                    for (int i = -1; i <= 1; i++)
                        Part(parent, "Jaw", new Vector3(i * 0.28f, 0.15f, 0.04f), new Vector3(0.15f, 0.52f, 0.44f), Iron, roll: i * 22);
                    break;
                default:
                    Part(parent, "Folded garment", Vector3.zero, new Vector3(0.80f, 0.56f, 0.76f), item == Item.Boots ? Oak : Cloth);
                    Part(parent, "Fastening", Vector3.up * 0.28f, new Vector3(0.23f, 0.14f, 0.40f), Brass);
                    break;
            }
        }

        private void Shield(Transform parent)
        {
            Vector3[] rim =
            {
                new(0, 0.52f), new(0.38f, 0.34f), new(0.33f, -0.16f), new(0, -0.54f),
                new(-0.33f, -0.16f), new(-0.38f, 0.34f)
            };
            Transform shield = Group(parent, "Kite shield", Vector3.zero).transform;
            for (int i = 0; i < rim.Length; i++)
            {
                Vector3 from = rim[i], to = rim[(i + 1) % rim.Length];
                GameObject beam = Part(shield, "Rim iron", (from + to) / 2,
                    new Vector3(0.068f, Vector3.Distance(from, to) + 0.025f, 0.10f), Iron);
                beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            }
            for (int i = -2; i <= 2; i++)
                Part(shield, "Oak board", new Vector3(i * 0.139f, 0.06f, 0), new Vector3(0.133f, 0.82f - Mathf.Abs(i) * 0.145f, 0.071f),
                    Color.Lerp(Oak, Wood, 0.4f + (i + 2) * 0.13f));
            Part(shield, "Iron boss", new Vector3(0, 0.07f, 0.069f), new Vector3(0.24f, 0.24f, 0.11f), Iron, roll: 45);
            shield.localRotation = Quaternion.Euler(90, 0, 0);
        }

        public void Vessel(Transform parent, string name, Vector3 position, bool boiler, float size = 1)
        {
            Transform vessel = Group(parent, name, position).transform;
            vessel.localScale = Vector3.one * size;
            Lathe(vessel, "Vessel body", Vector3.zero, boiler ? Iron : new Color(0.32f, 0.43f, 0.40f),
                boiler ? "cauldron" : "mortar", boiler
                ? new[] { (0.12f, -0.43f), (0.32f, -0.36f), (0.47f, -0.18f), (0.46f, 0.06f), (0.43f, 0.12f),
                    (0.37f, 0.12f), (0.37f, 0.04f), (0.27f, -0.25f), (0.04f, -0.27f) }
                : new[] { (0.16f, -0.30f), (0.22f, -0.26f), (0.32f, -0.12f), (0.39f, 0.04f), (0.39f, 0.09f),
                    (0.32f, 0.09f), (0.26f, -0.12f), (0.08f, -0.18f) });
            if (boiler)
                Part(vessel, "Brew surface", Vector3.down * 0.065f, new Vector3(0.69f, 0.032f, 0.69f),
                    new Color(0.31f, 0.61f, 0.39f), PrimitiveType.Cylinder);
            for (int side = -1; side <= 1; side += 2)
                Part(vessel, "Handle", new Vector3(side * (boiler ? 0.46f : 0.37f), 0.005f, 0), new Vector3(0.13f, 0.12f, 0.22f), Brass);
        }

        public GameObject Paddle(Transform parent, Vector3 position)
        {
            GameObject paddle = Group(parent, "Stirring paddle", position);
            Part(paddle.transform, "Handle", Vector3.up * 0.28f, new Vector3(0.077f, 0.41f, 0.077f), Wood, PrimitiveType.Cylinder);
            Part(paddle.transform, "Grip", Vector3.up * 0.64f, new Vector3(0.11f, 0.09f, 0.11f), Brass, PrimitiveType.Cylinder);
            Part(paddle.transform, "Paddle", Vector3.down * 0.10f, new Vector3(0.20f, 0.25f, 0.085f), Oak, PrimitiveType.Sphere);
            return paddle;
        }

        private void Lathe(Transform parent, string name, Vector3 position, Color tint, string key, (float radius, float y)[] profile)
        {
            GameObject part = Group(parent, name, position);
            MeshFilter filter = part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = meshMaterial;
            Paint(renderer, tint);
            if (!meshes.TryGetValue(key, out Mesh mesh))
            {
                mesh = new Mesh { name = key };
                const int sides = 10;
                List<Vector3> vertices = new();
                List<int> indices = new();
                Vector3 Point(int ring, int side)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    return new Vector3(Mathf.Cos(angle) * profile[ring].radius, profile[ring].y,
                        Mathf.Sin(angle) * profile[ring].radius);
                }

                for (int i = 0; i < profile.Length - 1; i++)
                    for (int side = 0; side < sides; side++)
                    {
                        int start = vertices.Count;
                        vertices.Add(Point(i, side));
                        vertices.Add(Point(i + 1, side));
                        vertices.Add(Point(i + 1, side + 1));
                        vertices.Add(Point(i, side + 1));
                        indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                meshes.Add(key, mesh);
            }

            filter.sharedMesh = mesh;
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes.Values) Object.Destroy(mesh);
        }
    }
}
