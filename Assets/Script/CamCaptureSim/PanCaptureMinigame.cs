using System;
using UnityEngine;
using UnityEngine.UI;

public class PanCaptureMinigame : MonoBehaviour, IFocusMinigame
{
    [Header("必填：UI 结构")]
    public RectTransform viewportRect;  // 在这里拖
    public RectTransform sceneRect;     // 被移动的画面（背景+拍摄对象）
    public RectTransform aimFrameRect;  // 瞄准框
    public RectTransform targetRect;    // 需要对准的拍摄对象

    [Header("判定设置")]
    public HitMode hitMode = HitMode.PivotInside;
    [Range(0f, 0.45f)] public float innerPaddingRatio = 0.1f;
    [Tooltip("是否必须在鼠标松开时才判定通关（命中反馈不受此限制）")]
    public bool checkOnPointerUpOnly = true;

    [Header("初始视角（0~1）")]
    [Range(0f, 1f)] public float initialPanX = 0.5f;
    [Range(0f, 1f)] public float initialPanY = 0.5f;

    [Header("手感")]
    [Tooltip("拖拽灵敏度（1 = 1:1 对应，本地像素）")]
    public float dragSensitivity = 1f;
    
    [Header("命中反馈（目标中心点入框时变色/脉冲）")]
    [Tooltip("给 AimFrame 上的 Graphic（例如 Image）")]
    public Graphic aimFrameGraphic;
    public Color aimNormalColor = Color.white;
    public Color aimHitColor    = new Color(0.2f, 1f, 0.2f, 1f);
    [Tooltip("颜色过渡速度（越大越跟手）")]
    public float colorLerpSpeed = 12f;

    [Tooltip("命中时是否做轻微脉冲动画")]
    public bool pulseOnHit = true;
    [Range(1f, 1.5f)] public float pulseScale = 1.08f;
    [Range(0.1f, 20f)] public float pulseSpeed = 6f;

    [Header("调试")]
    public bool debugLogs = false;

    private Action _onSuccess;
    private bool _running;
    private PanDragHandler _drag;
    private Canvas _canvas;

    // 命中反馈状态
    bool _isAimHit = false;
    Vector3 _aimBaseScale = Vector3.one;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (!aimFrameGraphic && aimFrameRect) aimFrameGraphic = aimFrameRect.GetComponent<Graphic>();
    }

    public void StartGame(Action onSuccess)
    {
        if (_running) return;
        _onSuccess = onSuccess;
        _running = true;

        if (!viewportRect || !sceneRect || !aimFrameRect || !targetRect)
        {
            Debug.LogError("[PanCapture] 引用未配置完整：Viewport/Scene/AimFrame/Target 必须赋值。");
            _running = false; onSuccess?.Invoke(); return;
        }

        if (aimFrameGraphic)
        {
            _aimBaseScale = aimFrameGraphic.rectTransform.localScale;
            aimFrameGraphic.color = aimNormalColor;
        }

        // 挂拖拽器
        _drag = viewportRect.GetComponent<PanDragHandler>();
        if (_drag == null) _drag = viewportRect.gameObject.AddComponent<PanDragHandler>();
        _drag.Init(this, viewportRect, _canvas ? _canvas.worldCamera : null);

        ApplyInitialPan();

        // 开局刷新一次命中反馈
        UpdateAimFeedback();

        if (debugLogs) Debug.Log("[PanCapture] StartGame");
    }

    public void StopGame()
    {
        _running = false;
        if (_drag) _drag.enabled = false;
        // 复位外观
        if (aimFrameGraphic)
        {
            aimFrameGraphic.color = aimNormalColor;
            aimFrameGraphic.rectTransform.localScale = _aimBaseScale;
        }
    }
    
    public void OnDragLocalDelta(Vector2 localDelta)
    {
        if (!_running) return;

        // 同向拖拽
        Vector2 candidate = sceneRect.anchoredPosition + localDelta * dragSensitivity;
        sceneRect.anchoredPosition = ClampSceneAnchoredPosition(candidate);

        // 实时命中反馈
        UpdateAimFeedback();
        
        if (!checkOnPointerUpOnly && IsTargetInsideAimForClear())
            Succeed();
    }

    public void OnPointerUp()
    {
        if (!_running) return;
        
        UpdateAimFeedback();

        if (checkOnPointerUpOnly && IsTargetInsideAimForClear())
            Succeed();
    }
    

    void ApplyInitialPan()
    {
        Vector2 min, max;
        GetScenePanLimits(out min, out max);

        var v = viewportRect.rect.size;
        var s = sceneRect.rect.size;

        float x = (s.x <= v.x) ? (min.x + max.x) * 0.5f : Mathf.Lerp(min.x, max.x, Mathf.Clamp01(initialPanX));
        float y = (s.y <= v.y) ? (min.y + max.y) * 0.5f : Mathf.Lerp(min.y, max.y, Mathf.Clamp01(initialPanY));

        sceneRect.anchoredPosition = new Vector2(x, y);
    }

    void GetScenePanLimits(out Vector2 min, out Vector2 max)
    {
        Vector2 v = viewportRect.rect.size;
        Vector2 s = sceneRect.rect.size;

        float halfVx = v.x * 0.5f, halfVy = v.y * 0.5f;
        float halfSx = s.x * 0.5f, halfSy = s.y * 0.5f;

        float rangeX = Mathf.Max(0f, halfSx - halfVx);
        float rangeY = Mathf.Max(0f, halfSy - halfVy);

        min = new Vector2(-rangeX, -rangeY);
        max = new Vector2(+rangeX, +rangeY);
    }

    Vector2 ClampSceneAnchoredPosition(Vector2 candidate)
    {
        Vector2 min, max; GetScenePanLimits(out min, out max);

        var v = viewportRect.rect.size;
        var s = sceneRect.rect.size;

        if (s.x <= v.x) candidate.x = (min.x + max.x) * 0.5f;
        else candidate.x = Mathf.Clamp(candidate.x, min.x, max.x);

        if (s.y <= v.y) candidate.y = (min.y + max.y) * 0.5f;
        else candidate.y = Mathf.Clamp(candidate.y, min.y, max.y);

        return candidate;
    }
    

    // 通关判定
    bool IsTargetInsideAimForClear()
    {
        if (!aimFrameRect || !targetRect) return false;

        Rect aim = GetScreenRect(aimFrameRect);
        Rect tgt = GetScreenRect(targetRect);
        Rect inner = ShrinkRect(aim, innerPaddingRatio);

        if (hitMode == HitMode.PivotInside)
        {
            Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(
                _canvas ? _canvas.worldCamera : null,
                targetRect.TransformPoint(PivotLocal(targetRect))
            );
            return inner.Contains(pivotScreen);
        }
        else
        {
            return inner.Contains(tgt.min) && inner.Contains(tgt.max);
        }
    }

    // 命中反馈
    void UpdateAimFeedback()
    {
        if (!aimFrameRect || !targetRect || !aimFrameGraphic) return;

        Rect aim = GetScreenRect(aimFrameRect);
        Rect inner = ShrinkRect(aim, innerPaddingRatio);

        Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(
            _canvas ? _canvas.worldCamera : null,
            targetRect.TransformPoint(PivotLocal(targetRect))
        );
        bool nowHit = inner.Contains(pivotScreen);
        
        Color targetColor = nowHit ? aimHitColor : aimNormalColor;
        aimFrameGraphic.color = Color.Lerp(aimFrameGraphic.color, targetColor, Time.unscaledDeltaTime * colorLerpSpeed);
        
        if (pulseOnHit)
        {
            if (nowHit)
            {
                float k = (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f); // 0..1
                float s = Mathf.Lerp(1f, pulseScale, k);
                aimFrameGraphic.rectTransform.localScale = _aimBaseScale * s;
            }
            else
            {
                aimFrameGraphic.rectTransform.localScale = Vector3.Lerp(
                    aimFrameGraphic.rectTransform.localScale,
                    _aimBaseScale,
                    Time.unscaledDeltaTime * colorLerpSpeed
                );
            }
        }

        _isAimHit = nowHit;
    }

    void Succeed()
    {
        if (!_running) return;
        if (debugLogs) Debug.Log("[PanCapture] Success!");
        StopGame();
        _onSuccess?.Invoke();
    }


    static Rect GetScreenRect(RectTransform rt)
    {
        Vector3[] world = new Vector3[4];
        rt.GetWorldCorners(world);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, world[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, world[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static Rect ShrinkRect(Rect r, float paddingRatio)
    {
        paddingRatio = Mathf.Clamp01(paddingRatio);
        float px = r.width * paddingRatio;
        float py = r.height * paddingRatio;
        return new Rect(r.x + px, r.y + py, r.width - 2 * px, r.height - 2 * py);
    }

    static Vector2 PivotLocal(RectTransform rt)
    {
        var sz = rt.rect.size;
        return new Vector2((rt.pivot.x - 0.5f) * sz.x, (rt.pivot.y - 0.5f) * sz.y);
    }

    public void NotifyPointerUp() => OnPointerUp();

    public enum HitMode { PivotInside, FullBoundsInside }
}
