using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SimpleDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform _self;
    Canvas _canvas;
    RectTransform _targetZone;
    Action _onSucceed;
    Vector2 _startPos;
    bool _enabled;

    public void Init(RectTransform targetZone, Action onSucceed)
    {
        _self = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _targetZone = targetZone;
        _onSucceed = onSucceed;
        _startPos = _self.anchoredPosition;
        _enabled = true;
        enabled = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_enabled) return;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_enabled) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position, eventData.pressEventCamera, out var localPoint);
        _self.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_enabled) return;

        bool inZone = RectTransformUtility.RectangleContainsScreenPoint(
            _targetZone, eventData.position, eventData.pressEventCamera);

        if (inZone)
        {
            _enabled = false;
            _onSucceed?.Invoke();
        }
        else
        {
            _self.anchoredPosition = _startPos;
        }
    }
}