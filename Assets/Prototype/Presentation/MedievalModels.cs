using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed partial class MedievalModels
    {
        public static readonly Color Oak = new(0.29f, 0.15f, 0.085f);
        public static readonly Color Wood = new(0.45f, 0.25f, 0.13f);
        public static readonly Color Brass = new(0.75f, 0.50f, 0.21f);
        public static readonly Color Iron = new(0.25f, 0.31f, 0.34f);
        public static readonly Color Cloth = new(0.23f, 0.34f, 0.31f);
        private static readonly Color Linen = new(0.76f, 0.66f, 0.47f);
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
            parent.GetComponent<Renderer>().enabled = false;
            PlaceModel(parent, "CargoBox");
        }

        public void Bin(Transform parent, Vector3 position)
        {
            Transform root = Group(parent, "Salvage bin", position).transform;
            Lathe(root, "Iron lining", Vector3.zero, Iron, "bin lining", new[] { (0.32f, 0.03f), (0.44f, 0.85f),
                (0.44f, 0.89f), (0.39f, 0.89f), (0.29f, 0.15f), (0f, 0.15f) });
            for (int i = 0; i < 14; i++)
            {
                float a = i * 360f / 14;
                Quaternion turn = Quaternion.Euler(0, a, 0);
                var stave = Part(root, "Oak stave", turn * new Vector3(0, 0.43f, 0.38f),
                    new Vector3(0.16f, 0.82f, 0.06f), Color.Lerp(Wood, Oak, i % 3 * 0.2f));
                stave.transform.localRotation = turn * Quaternion.Euler(8, 0, 0);
            }
        }

        public ActorMotion Person(Transform parent, int creature = -1)
        {
            string asset = creature switch
            {
                0 => "GoblinRaider", 1 => "CinderShaman", 2 => "EmberSentinel", -2 => "CaravanMerchant", _ => "Apprentice"
            };
            Transform rig = Group(parent, "Animated body", Vector3.zero).transform;
            Transform upper = Group(rig, "Torso", Vector3.zero).transform;
            Transform model = PlaceModel(upper, asset);
            Transform Limb(string name, Transform group, Vector3 pivot)
            {
                Transform joint = Group(group, name + " joint", pivot).transform;
                Transform part = model.Find(name);
                if (part == null) throw new System.InvalidOperationException(asset + " is missing " + name);
                part.SetParent(joint, true);
                return joint;
            }
            return new ActorMotion(rig, upper, Limb("ArmL", upper, new Vector3(-0.375f, 0.23f, 0)),
                Limb("ArmR", upper, new Vector3(0.375f, 0.23f, 0)), Limb("LegL", rig, new Vector3(-0.164f, -0.28f, 0)),
                Limb("LegR", rig, new Vector3(0.164f, -0.28f, 0)));
        }

        public void ItemModel(Transform parent, Item item)
        {
            switch (item)
            {
                case Item.Tonic:
                case Item.Focus:
                case Item.Ward:
                case Item.Brew:
                    PlaceModel(parent, "CorkedFlask", item == Item.Tonic ? "TonicGlass" : item == Item.Ward ? "WardGlass" : "Atelier");
                    break;
                case Item.Blade:
                case Item.Greatblade:
                    PlaceModel(parent, "Longsword").localRotation = Quaternion.Euler(0, 0, -90);
                    break;
                case Item.Shield:
                    PlaceModel(parent, "KiteShield").localRotation = Quaternion.Euler(90, 0, 0);
                    break;
                default:
                    string asset = item switch
                    {
                        Item.Herb => "HerbalBundle", Item.Helm => "VisoredHelm", Item.Ore => "IronOre",
                        Item.Ember => "CoalHeart", Item.Trap => "SpringTrap", Item.Coat => "FieldCoat",
                        Item.Boots => "Boots", Item.Leggings => "Breeches", _ => throw new System.ArgumentOutOfRangeException(nameof(item))
                    };
                    PlaceModel(parent, asset);
                    break;
            }
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
            foreach (Material material in paintedMaterials.Values) Object.Destroy(material);
            foreach (Mesh mesh in meshes.Values) Object.Destroy(mesh);
        }
    }
}
