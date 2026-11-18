using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonsNShops
{
    internal sealed partial class PrototypeUi
    {
        private readonly InventoryCell[] inventoryCells = new InventoryCell[InventoryLayout.ArmorStart + InventoryLayout.ArmorSize];
        private readonly UnityEngine.UI.Text[] inventoryLabels = new UnityEngine.UI.Text[InventoryLayout.ArmorStart + InventoryLayout.ArmorSize];
        private InventoryCell hoveredCell;
        private UnityEngine.UI.Text dragLabel, inventoryHint;

        public void AssignHovered(int hotbarIndex)
        {
            if (hoveredCell != null && hoveredCell.Index < InventoryLayout.HotbarStart && hoveredCell.Item >= 0)
                command(ActionKind.MoveInventory, hoveredCell.Index, InventoryLayout.HotbarStart + hotbarIndex);
        }

        private void Inventory(PlayerState player, Action close)
        {
            Heading("panel.inventory");
            inventoryHint = ui.Label(panel, T("inventory.hint"), 36, 17);
            RectTransform layout = ui.Row(panel, 350);
            RectTransform pack = ui.Rect(layout, "Backpack");
            GridLayoutGroup grid = pack.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.cellSize = new Vector2(88, 61);
            grid.spacing = new Vector2(5, 5);
            LayoutElement width = pack.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = 553;
            for (int i = 0; i < InventoryLayout.BackpackSize; i++) AddCell(pack, i);

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
            Act(actions, "panel.offhand", ActionKind.Offhand);
            Button(actions, "panel.close", close);
            RefreshInventory(player);
        }

        private RectTransform AddCell(Transform parent, int index)
        {
            RectTransform rect = ui.Box(parent, "Inventory cell " + index, UiFactory.Accent);
            InventoryCell cell = rect.gameObject.AddComponent<InventoryCell>();
            cell.Index = index;
            cell.Drop = (from, to) => command(ActionKind.MoveInventory, from, to);
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
                int item = i < InventoryLayout.HotbarStart ? player.backpack[i]
                    : i < InventoryLayout.ArmorStart ? player.hotbar[i - InventoryLayout.HotbarStart]
                    : player.equipment[i - InventoryLayout.ArmorStart];
                inventoryCells[i].Item = item;
                string prefix = i >= InventoryLayout.ArmorStart
                    ? T("armor." + (ArmorSlot)(i - InventoryLayout.ArmorStart)) + "\n"
                    : i >= InventoryLayout.HotbarStart ? ((i - InventoryLayout.HotbarStart + 1) % 10) + "\n" : "";
                inventoryLabels[i].text = prefix + (item < 0 ? T("hud.empty")
                    : ItemName(item) + (i < InventoryLayout.BackpackSize ? " ×" + player.bag[item] : ""));
            }

            inventoryHint.text = hoveredCell != null && hoveredCell.Item >= 0
                ? ItemName(hoveredCell.Item) + "   ·   " + T("inventory.hint") : T("inventory.hint");
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
