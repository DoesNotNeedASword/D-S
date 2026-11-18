using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed partial class PrototypeUi
    {
        private string Effects(PotionEffects e)
        {
            var parts = new List<string>(3);
            void Add(string key, int value)
            {
                if (value != 0) parts.Add(T(key) + " " + (value > 0 ? "+" : "") + value);
            }
            Add("effect.hp", e.health);
            Add("effect.energy", e.energy);
            Add("effect.armor", e.armor);
            return parts.Count == 0 ? T("effect.inert") : string.Join(" · ", parts);
        }

        private void ShowItemInfo(int item, PotionEffects formula, int count, PotionEffects coating, Vector2 position, int price = -1)
        {
            Vector2Int size = InventoryLayout.Size(item);
            string details = ItemName(item) + "  ×" + count + "\n" + T("item.dimensions", size.x, size.y,
                Currency(price >= 0 ? price : Catalog.Get(item).BaseValue));
            if (PotionEffects.IsBottle(item))
            {
                details += "\n" + Effects(PotionEffects.Bottle(item, formula));
                if (mode == ScreenMode.Inventory) details += "\n" + T("item.coat");
            }
            else if (Catalog.IsIngredient(item))
                details += "\n" + T("item.rawGround", Effects(PotionEffects.Ingredient((Item)item, false)),
                    Effects(PotionEffects.Ingredient((Item)item, true)));
            else if (Catalog.Get(item).Damage > 0)
                details += "\n" + T("item.damage", Catalog.Get(item).Damage) +
                    (Catalog.Get(item).TwoHanded ? " · " + T("hud.both") : "");
            if (!coating.Empty) details += "\n" + T("item.coated", Effects(coating));
            tooltipText.text = details;
            tooltip.sizeDelta = new Vector2(315, Mathf.Max(70, tooltipText.preferredHeight + 18));
            var canvas = (RectTransform)tooltip.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, position, null, out Vector2 local);
            tooltip.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x + 16, canvas.rect.xMin, canvas.rect.xMax - tooltip.rect.width),
                Mathf.Clamp(local.y - 14, canvas.rect.yMin + tooltip.rect.height, canvas.rect.yMax));
            tooltip.SetAsLastSibling();
            tooltip.gameObject.SetActive(true);
        }
    }
}
