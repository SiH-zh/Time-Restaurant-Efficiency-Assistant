using UnityEngine;
using UnityEngine.EventSystems;

namespace RestaurantBoost
{
    public sealed class ArrivalWindowDrag : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal RectTransform Target;
        internal RectTransform CanvasRect;
        internal ModBehaviour Owner;
        private Vector2 pointerStart, positionStart;
        private bool dragging, moved;
        public void OnPointerDown(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left) Begin(e.position); }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnBeginDrag(PointerEventData e)
        { if (!dragging) Begin(e.position); }
        private void Begin(Vector2 point)
        {
            if (Target == null) return;
            pointerStart = point; positionStart = Target.anchoredPosition; dragging = true; moved = false;
        }
        public void OnDrag(PointerEventData e)
        { if (dragging) Move(e.position); }
        private void Move(Vector2 point)
        {
            if (Target == null || CanvasRect == null) return;
            var canvas = CanvasRect.GetComponent<Canvas>();
            float scale = canvas == null ? 1f : Mathf.Max(.01f, canvas.scaleFactor);
            Target.anchoredPosition = positionStart + (point - pointerStart) / scale;
            Clamp(Target, CanvasRect);
            moved |= (Target.anchoredPosition - positionStart).sqrMagnitude > .01f;
        }
        public void OnEndDrag(PointerEventData e) { Finish(); }
        private void Update()
        {
            if (Owner == null || Owner.View == null || !Owner.View.FloatingArrivalInteractive)
            { dragging = false; return; }
            // 标题本身接收按压；另以游戏原生鼠标输入兼容未派发拖拽事件的输入模块。
            if (!dragging && Input.GetMouseButtonDown(0)
                && RectTransformUtility.RectangleContainsScreenPoint(transform as RectTransform, Input.mousePosition, null))
                Begin(Input.mousePosition);
            if (!dragging) return;
            if (Input.GetMouseButton(0)) Move(Input.mousePosition); else Finish();
        }
        private void Finish()
        {
            if (!dragging) return;
            dragging = false;
            if (!moved || Target == null || Owner == null) return;
            Owner.Settings.ArrivalWindowX = Target.anchoredPosition.x;
            Owner.Settings.ArrivalWindowY = Target.anchoredPosition.y;
            Owner.Changed("悬浮窗位置已保存。", false);
        }
        internal static void Clamp(RectTransform target, RectTransform canvas)
        {
            var point = target.anchoredPosition;
            point.x = Mathf.Clamp(point.x, Mathf.Min(-12f, -canvas.rect.width + target.rect.width + 12f), -12f);
            point.y = Mathf.Clamp(point.y, Mathf.Min(-12f, -canvas.rect.height + target.rect.height + 12f), -12f);
            target.anchoredPosition = point;
        }
    }
}
