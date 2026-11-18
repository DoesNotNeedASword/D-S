using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonsNShops
{
    internal sealed partial class PrototypeUi
    {
        private readonly InventoryCell[] inventoryCells = new InventoryCell[InventoryLayout.ArmorStart + InventoryLayout.ArmorSize];
        private readonly UnityEngine.UI.Text[] inventoryLabels = new UnityEngine.UI.Text[InventoryLayout.ArmorStart + InventoryLayout.ArmorSize];
        private readonly RectTransform[] footprints = new RectTransform[InventoryLayout.BackpackSize];
        private readonly Text[] itemTitles = new Text[InventoryLayout.BackpackSize];
        private const int Pitch = 62;
        private InventoryCell hoveredCell;
        private UnityEngine.UI.Text dragLabel, inventoryHint;

        public void AssignHovered(int hotbarIndex)
        {
            if (hotbarIndex >= 0 && hotbarIndex < InventoryLayout.HotbarSize && hoveredCell != null && hoveredCell.Index < InventoryLayout.HotbarStart && hoveredCell.Item >= 0)
                command(ActionKind.MoveInventory, hoveredCell.Index, InventoryLayout.HotbarStart + hotbarIndex);
        }

        private void Inventory(PlayerState player, Action close)
        {
            Heading("panel.inventory");
            inventoryHint = ui.Label(panel, T("inventory.hint"), 36, 17);
            RectTransform layout = ui.Row(panel, 350);
            RectTransform pack = ui.Rect(layout, "Backpack");
            LayoutElement width = pack.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = InventoryLayout.Columns * Pitch;
            for (int i = 0; i < InventoryLayout.BackpackSize; i++)
                Position(AddCell(pack, i), i, Vector2Int.one);
            for (int i = 0; i < footprints.Length; i++)
            {
                RectTransform tile = footprints[i] = ui.Box(pack, "Packed item", UiFactory.Accent);
                tile.GetComponent<Image>().raycastTarget = false;
                itemTitles[i] = ui.Text(tile, "", 16, TextAnchor.MiddleCenter);
                UiFactory.Stretch(itemTitles[i].rectTransform, new Vector2(3, 2), new Vector2(-3, -2));
            }

            RectTransform armor = ui.Rect(layout, "Armor");
            UiFactory.Column(armor, 8, 3);
            for (int i = 0; i < InventoryLayout.ArmorSize; i++)
            {
                RectTransform rect = AddCell(armor, InventoryLayout.ArmorStart + i);
                UiFactory.Height(rect.gameObject, 62);
            }

            RectTransform shortcuts = ui.Row(panel, 54);
            for (int i = 0; i < InventoryLayout.HotbarSize; i++)
                AddCell(shortcuts, InventoryLayout.HotbarStart + i);
            RectTransform actions = ui.Row(panel, 38);
            Button(actions, "panel.close", close);
            RefreshInventory(player);
        }

        private static void Position(RectTransform rect, int cell, Vector2Int size)
        {
            Vector2 topLeft = new(cell % InventoryLayout.Columns * Pitch, -cell / InventoryLayout.Columns * Pitch);
            UiFactory.Anchor(rect, Vector2.up, Vector2.up,
                topLeft + new Vector2(1, 1 - size.y * Pitch), topLeft + new Vector2(size.x * Pitch - 1, -1));
        }

        private RectTransform AddCell(Transform parent, int index)
        {
            RectTransform rect = ui.Box(parent, "Inventory cell " + index, UiFactory.Accent);
            InventoryCell cell = rect.gameObject.AddComponent<InventoryCell>();
            cell.Index = index;
            cell.Coat = from => command(ActionKind.CoatWeapon, from, 0);
            cell.Drop = (from, to) =>
            {
                int hand = to - InventoryLayout.HotbarStart;
                bool coat = hand is 0 or 1 && PotionEffects.IsBottle(inventoryCells[from].Item) &&
                    Catalog.Valid(inventoryCells[to].Item) && Catalog.Get(inventoryCells[to].Item).Damage > 0;
                command(coat ? ActionKind.CoatWeapon : ActionKind.MoveInventory, from, coat ? hand : to);
            };
            cell.Drag = MoveGhost;
            cell.EndDrag = ClearGhost;
            cell.Hover = (target, entered) =>
            {
                if (entered) hoveredCell = target;
                else if (hoveredCell == target) hoveredCell = null;
            };
            inventoryCells[index] = cell;
            UnityEngine.UI.Text label = ui.Text(rect, "", 14, TextAnchor.MiddleCenter);
            UiFactory.Stretch(label.rectTransform, new Vector2(4, 0), new Vector2(-4, 0));
            inventoryLabels[index] = label;
            return rect;
        }

        private void RefreshInventory(PlayerState player)
        {
            for (int i = 0; i < inventoryCells.Length; i++)
            {
                InventoryStack stack = InventoryLayout.At(player, i);
                int item = stack?.item ?? -1;
                inventoryCells[i].Item = item;
                string title = stack == null ? T("hud.empty") : ItemName(item) + (stack.count > 1 ? " ×" + stack.count : "");
                if (i < InventoryLayout.BackpackSize)
                {
                    bool origin = stack?.cell == i;
                    footprints[i].gameObject.SetActive(origin);
                    if (!origin) continue;
                    Position(footprints[i], i, InventoryLayout.Size(item));
                    itemTitles[i].text = title;
                    continue;
                }
                bool armor = i >= InventoryLayout.ArmorStart;
                int slot = i - InventoryLayout.HotbarStart;
                inventoryCells[i].gameObject.SetActive(armor || slot < player.quickSlots);
                string label = armor ? T("armor." + (ArmorSlot)(i - InventoryLayout.ArmorStart))
                    : (slot + 1) + (slot < 2 ? " · " + T(slot == 0 ? "slot.weapon" : "slot.offhand") : "");
                if (!armor && slot == 1 && InventoryLayout.TwoHands(player)) title = T("hud.both");
                inventoryLabels[i].text = label + "\n" + title;
            }
            inventoryHint.text = T("inventory.hint");
            var hovered = hoveredCell == null ? null : InventoryLayout.At(player, hoveredCell.Index);
            if (hovered == null || dragLabel != null && dragLabel.gameObject.activeSelf)
                tooltip.gameObject.SetActive(false);
            else
            {
                Vector2 cursor = UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ?? Vector2.zero;
                ShowItemInfo(hovered.item, hovered.formula, hovered.count, hovered.coating, cursor);
            }
        }

        private void MoveGhost(InventoryCell source, Vector2 position)
        {
            if (dragLabel == null)
            {
                dragLabel = ui.Text(panel.parent, "", 22, TextAnchor.MiddleCenter);
                dragLabel.rectTransform.sizeDelta = new Vector2(220, 42);
                AddOutline(dragLabel);
            }
            dragLabel.text = ItemName(source.Item);
            dragLabel.rectTransform.position = position + new Vector2(0, 23);
            dragLabel.gameObject.SetActive(true);
        }

        private void ClearGhost()
        {
            if (dragLabel != null) dragLabel.gameObject.SetActive(false);
        }
    }
}
