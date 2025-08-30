using UnityEngine;
using UnityEngine.EventSystems;

public class FocusHandleDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform _track, _handle;
    Canvas _canvas;
    //允许的X轴范围
    float _xMin, _xMax;
    //拖动过程回调（0~1）
    System.Action<float> _onValueChanged;
    //松手回调（0~1）
    System.Action<float> _onEndDrag;

    public void Init(RectTransform trackRect,
        System.Action<float> onValueChanged,
        System.Action<float> onEndDrag = null)
    {
        _track = trackRect;
        _handle = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        _onValueChanged = onValueChanged;
        _onEndDrag = onEndDrag;

        var r = _track.rect;
        float half = _handle.rect.width * 0.5f;
        _xMin = r.xMin + half;
        _xMax = r.xMax - half;
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        float scale = (_canvas && _canvas.scaleFactor != 0) ? _canvas.scaleFactor : 1f;
        float x = _handle.anchoredPosition.x + eventData.delta.x / scale;
        x = Mathf.Clamp(x, _xMin, _xMax);
        _handle.anchoredPosition = new Vector2(x, _handle.anchoredPosition.y);

        float t = Mathf.InverseLerp(_xMin, _xMax, x);
        //拖动中：仅更新清晰度，不判定成功
        _onValueChanged?.Invoke(t);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float t = Mathf.InverseLerp(_xMin, _xMax, _handle.anchoredPosition.x);
        //松手时：再做成功判定
        _onEndDrag?.Invoke(t);
    }
}