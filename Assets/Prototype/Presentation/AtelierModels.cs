using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed partial class MedievalModels
    {
        private readonly Dictionary<string, GameObject> imported = new();
        private readonly Dictionary<string, Material> paintedMaterials = new();

        private Transform PlaceModel(Transform parent, string asset, string palette = "Atelier")
        {
            if (!imported.TryGetValue(asset, out var prefab))
                imported[asset] = prefab = Resources.Load<GameObject>("Atelier/" + asset);
            if (!paintedMaterials.TryGetValue(palette, out var paint))
            {
                paint = new Material(meshMaterial) { name = palette + " painted steel and cloth" };
                var texture = Resources.Load<Texture2D>("Atelier/" + palette);
                paint.mainTexture = texture;
                if (paint.HasProperty("_BaseMap")) paint.SetTexture("_BaseMap", texture);
                if (paint.HasProperty("_Smoothness")) paint.SetFloat("_Smoothness", 0.21f);
                paintedMaterials.Add(palette, paint);
            }
            var root = Object.Instantiate(prefab, parent, false);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = paint;
            return root.transform;
        }
    }
}
