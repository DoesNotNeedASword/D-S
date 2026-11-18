using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonsNShops
{
    internal sealed class InventoryCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int Index { get; set; }
        public int Item { get; set; } = -1;
        public Action<InventoryCell, Vector2> Drag { get; set; }
        public Action EndDrag { get; set; }
        public Action<int, int> Drop { get; set; }
        public Action<InventoryCell, bool> Hover { get; set; }

        public void OnBeginDrag(PointerEventData eventData) => OnDrag(eventData);
        public void OnDrag(PointerEventData eventData)
        {
            if (Item >= 0 && eventData.button == PointerEventData.InputButton.Left)
                Drag(this, eventData.position);
        }
        public void OnEndDrag(PointerEventData eventData) => EndDrag();
        public void OnDrop(PointerEventData eventData)
        {
            InventoryCell source = eventData.pointerDrag?.GetComponent<InventoryCell>();
            if (source != null && source.Item >= 0) Drop(source.Index, Index);
        }
        public void OnPointerEnter(PointerEventData eventData) => Hover(this, true);
        public void OnPointerExit(PointerEventData eventData) => Hover(this, false);
    }
}
