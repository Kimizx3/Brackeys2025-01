using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class FocusHandleDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform _track;
    RectTransform _handle;
    Action<float> _onValueChanged;
    Action<float> _onDragEnd;
    float _xMin, _xMax;
    bool _inited;

    public void Init(RectTransform track, Action<float> onValueChanged, Action<float> onDragEnd)
    {
        _track = track;
        _handle = GetComponent<RectTransform>();
        _onValueChanged = onValueChanged;
        _onDragEnd = onDragEnd;

        var r = _track.rect;
        float half = _handle.rect.width * 0.5f;
        _xMin = r.xMin + half;
        _xMax = r.xMax - half;
        _inited = true;
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_inited) return;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, eventData.position, eventData.pressEventCamera, out local);
        float x = Mathf.Clamp(local.x, _xMin, _xMax);
        var p = _handle.anchoredPosition;
        _handle.anchoredPosition = new Vector2(x, p.y);

        float norm = Mathf.InverseLerp(_xMin, _xMax, x);
        _onValueChanged?.Invoke(norm);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_inited) return;
        float norm = Mathf.InverseLerp(_xMin, _xMax, _handle.anchoredPosition.x);
        _onDragEnd?.Invoke(norm);
    }
}
