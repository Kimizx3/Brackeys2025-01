using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SimpleDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Drag Basics")]
    
    // 开始时把中心吸到指针
    public bool snapToPointerOnBegin = false;
    [Tooltip("松手未命中时的回弹时间(0=瞬间)")]
    public float returnDuration = 0f;

    [Header("Movement Constraints")]
    [Tooltip("沿X轴移动")]
    public bool allowX = true;
    [Tooltip("沿Y轴移动")]
    public bool allowY = true;

    [Tooltip("矩形范围内移动")]
    public bool limitWithinBounds = false;
    public RectTransform movementBounds;
    // 限制区域
    [Tooltip("缩小有效范围")]
    public Vector2 boundsPadding = Vector2.zero;

    [Header("Hit Test (独立命中范围)")]
    [Tooltip("若留空则用Draggable自身Rect")]
    public RectTransform draggableHitRect;
    [Tooltip("若留空则用Init传入的dropZone")]
    public RectTransform targetHitRect;

    public enum HitMode { RectOverlap, PointerInTarget, CenterInTarget }
    [Tooltip("命中判定方式：矩形重叠/指针在目标内/中心点在目标内")]
    public HitMode hitMode = HitMode.RectOverlap;

    [Header("Snap on Success")]
    public bool snapOnSuccess = true;
    [Tooltip("吸附到此锚点")]
    public RectTransform snapAnchor;
    public Vector2 snapOffset = Vector2.zero;
    [Tooltip("吸附动画时长，0=瞬间定位")]
    public float snapDuration = 0.15f;
    [Tooltip("命中后把拖拽物提到最上层")]
    public bool bringToFrontOnSnap = true;

    // ------- 运行时 -------
    RectTransform _self;
    Canvas _canvas;
    RectTransform _targetZone;
    Action _onSucceed;
    Vector2 _startPos;
    Vector2 _dragStartPos;
    bool _enabled;

    public void Init(RectTransform targetZone, Action onSucceed)
    {
        _self = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _targetZone = targetZone;
        _onSucceed  = onSucceed;
        if (_self == null || _canvas == null)
        {
            Debug.LogError("[SimpleDragItem] 缺少 RectTransform/Canvas。");
            enabled = false; return;
        }

        if (_self.GetComponent<Graphic>() == null)
            Debug.LogWarning("[SimpleDragItem] 建议在 Draggable 上挂一个 Graphic（Image/Text等）并勾选 Raycast Target。");

        _startPos = _self.anchoredPosition;
        _enabled = true;
        enabled = true;

        if (debugLogs) Debug.Log("[SimpleDragItem] Init 完成");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_enabled) return;
        _dragStartPos = _self.anchoredPosition;

        if (snapToPointerOnBegin &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, eventData.position, eventData.pressEventCamera, out var local))
        {
            _self.anchoredPosition = local;
        }

        if (debugLogs) Debug.Log("[SimpleDragItem] BeginDrag");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_enabled) return;

        float scale = (_canvas && _canvas.scaleFactor != 0) ? _canvas.scaleFactor : 1f;
        Vector2 next = _self.anchoredPosition + eventData.delta / scale;

        // 轴向限制
        if (!allowX) next.x = _dragStartPos.x;
        if (!allowY) next.y = _dragStartPos.y;

        _self.anchoredPosition = next;

        // 边界限制
        if (limitWithinBounds && movementBounds)
            ClampInsideBounds();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_enabled) return;

        bool success = EvaluateHit(eventData);
        if (debugLogs) Debug.Log("[SimpleDragItem] EndDrag, hit=" + success);

        if (success)
        {
            _enabled = false;

            if (snapOnSuccess)
                StartCoroutine(SnapToTargetCo());
            else
                _onSucceed?.Invoke();
        }
        else
        {
            // 回弹
            if (returnDuration > 0f && Application.isPlaying)
                StartCoroutine(LerpTo(_startPos, returnDuration));
            else
                _self.anchoredPosition = _startPos;
        }
    }

    // ============ 约束到 Bounds ============
    void ClampInsideBounds()
    {
        // 取两个矩形
        Rect dragRect = GetScreenRect(_self);
        Rect boundRect = GetScreenRect(movementBounds);

        // 应用 padding
        boundRect.xMin += boundsPadding.x;
        boundRect.xMax -= boundsPadding.x;
        boundRect.yMin += boundsPadding.y;
        boundRect.yMax -= boundsPadding.y;

        Vector2 shift = Vector2.zero;
        if (dragRect.xMin < boundRect.xMin) shift.x += (boundRect.xMin - dragRect.xMin);
        if (dragRect.xMax > boundRect.xMax) shift.x -= (dragRect.xMax - boundRect.xMax);
        if (dragRect.yMin < boundRect.yMin) shift.y += (boundRect.yMin - dragRect.yMin);
        if (dragRect.yMax > boundRect.yMax) shift.y -= (dragRect.yMax - boundRect.yMax);

        if (shift != Vector2.zero)
        {
            float scale = (_canvas && _canvas.scaleFactor != 0) ? _canvas.scaleFactor : 1f;
            _self.anchoredPosition += shift / scale;
        }
    }

    // ============ 命中判定 ============
    bool EvaluateHit(PointerEventData eventData)
    {
        RectTransform targetRect = targetHitRect ? targetHitRect : _targetZone;
        if (!targetRect) return false;

        switch (hitMode)
        {
            case HitMode.PointerInTarget:
                return RectTransformUtility.RectangleContainsScreenPoint(targetRect, eventData.position, eventData.pressEventCamera);

            case HitMode.CenterInTarget:
            {
                Vector2 dragCenterScreen = GetScreenRect(_self).center;
                return RectTransformUtility.RectangleContainsScreenPoint(targetRect, dragCenterScreen, eventData.pressEventCamera);
            }

            case HitMode.RectOverlap:
            default:
            {
                Rect a = GetScreenRect(draggableHitRect ? draggableHitRect : _self);
                Rect b = GetScreenRect(targetRect);
                return a.Overlaps(b);
            }
        }
    }

    // ============ 吸附 ============
    IEnumerator SnapToTargetCo()
    {
        // 计算目标屏幕中心
        RectTransform anchor = snapAnchor ? snapAnchor : (targetHitRect ? targetHitRect : _targetZone);
        if (!anchor)
        {
            _onSucceed?.Invoke();
            yield break;
        }

        // 目标世界中心 → Canvas 本地坐标
        Vector2 targetLocal = WorldCenterToLocal(anchor, _self.parent as RectTransform) + snapOffset;

        if (bringToFrontOnSnap)
            _self.SetAsLastSibling();

        if (snapDuration > 0f && Application.isPlaying)
            yield return LerpTo(targetLocal, snapDuration);
        else
            _self.anchoredPosition = targetLocal;

        _onSucceed?.Invoke();
    }

    IEnumerator LerpTo(Vector2 target, float dur)
    {
        Vector2 from = _self.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            _self.anchoredPosition = Vector2.Lerp(from, target, k);
            yield return null;
        }
        _self.anchoredPosition = target;
    }

    // ============ 工具 ============
    Rect GetScreenRect(RectTransform rt)
    {
        if (!rt) return new Rect();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // 转屏幕矩形（AABB）
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return new Rect(min, max - min);
    }

    Vector2 WorldCenterToLocal(RectTransform worldRt, RectTransform localSpace)
    {
        Vector3[] corners = new Vector3[4];
        worldRt.GetWorldCorners(corners);
        Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
        RectTransform canvasRt = _canvas.transform as RectTransform;
        // 世界 → 屏幕 → localSpace
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, centerWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(localSpace, screen, null, out var local);
        return local;
    }
}
