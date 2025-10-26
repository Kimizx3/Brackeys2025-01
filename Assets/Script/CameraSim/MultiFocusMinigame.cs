using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MultiFocusMinigame : MonoBehaviour, IFocusMinigame
{
    [Header("=== 目标对象列表（清晰/模糊） ===")]
    public List<FocusTarget> targets = new List<FocusTarget>();

    [Header("=== 滑杆 ===")]
    public RectTransform trackRect;
    public RectTransform handleRect;

    [Header("=== 全局/默认参数（0~1） ===")]
    [Tooltip("初始参数值（0~1），用于定位手柄与初始清晰度")]
    [Range(0f, 1f)] public float initialValue = 0.25f;
    [Tooltip("默认的清晰→模糊的过渡宽度")]
    [Range(0.01f, 1f)] public float defaultBlurWidth = 0.5f;

    [Header("=== 初始与目标 ===")]
    [Tooltip("初始清晰对象”（-1 表示按 initialValue 自动决定）")]
    public int initialSharpTargetIndex = -1;
    [Tooltip("通过判定的目标对象")]
    public int goalTargetIndex = 0;
    [Tooltip("成功窗口半宽（目标对象最佳值±容差）")]
    [Range(0f, 0.5f)] public float successHalfWidth = 0.06f;

    [Header("=== 成功后的吸附 ===")]
    public bool  snapHandleOnSuccess = true;
    public float snapDuration = 0.15f;

    [Header("其它")]
    public bool debugLogs = false;

    // 运行时
    Action _onSuccess;
    bool _running;
    bool _successFired;
    float _xMin, _xMax;
    float _currentNorm = 0f;

    void Awake()
    {
        _currentNorm = Mathf.Clamp01(initialValue);
    }

    void OnEnable()
    {
        EnsureBaselineVisuals();
    }

    void EnsureBaselineVisuals()
    {
        foreach (var t in targets)
        {
            if (t == null) continue;
            t.EnsureBlurIsOpaque();
        }
        ApplyAllTargetVisuals(_currentNorm, forceWinnerOnly:true);
    }


    public void StartGame(Action onSuccess)
    {
        if (_running)
        {
            if (debugLogs) Debug.Log("[MultiFocus] StartGame ignored (already running).");
            return;
        }

        _onSuccess = onSuccess;
        _successFired = false;
        _running = true;

        if (!trackRect || !handleRect || targets.Count == 0)
        {
            Debug.LogError("[MultiFocus] 引用未配置完整：需要 trackRect / handleRect / 至少 1 个目标。");
            _running = false; onSuccess?.Invoke(); return;
        }

        var r = trackRect.rect;
        float halfHandle = handleRect.rect.width * 0.5f;
        _xMin = r.xMin + halfHandle;
        _xMax = r.xMax - halfHandle;

        // 初始化各目标的底层模糊图
        foreach (var t in targets) if (t != null) t.EnsureBlurIsOpaque();

        // 设定初始参数 & 初始清晰对象
        if (initialSharpTargetIndex >= 0 && initialSharpTargetIndex < targets.Count)
        {
            _currentNorm = Mathf.Clamp01(targets[initialSharpTargetIndex].bestValue);
        }
        else
        {
            _currentNorm = Mathf.Clamp01(initialValue);
        }

        SetValue(_currentNorm, updateHandle:true, updateVisuals:true);

        // 拖拽组件
        var dragger = handleRect.GetComponent<FocusHandleDragger>();
        if (dragger == null) dragger = handleRect.gameObject.AddComponent<FocusHandleDragger>();
        dragger.Init(trackRect, OnHandleValueChanged, OnHandleDragEnd);

        if (debugLogs) Debug.Log("[MultiFocus] StartGame");
    }

    public void StopGame()
    {
        _running = false;
        var dragger = handleRect ? handleRect.GetComponent<FocusHandleDragger>() : null;
        if (dragger) dragger.enabled = false;
    }


    public void SetValue(float normalized, bool updateHandle, bool updateVisuals)
    {
        _currentNorm = Mathf.Clamp01(normalized);

        if (updateHandle)
        {
            float x = Mathf.Lerp(_xMin, _xMax, _currentNorm);
            var p = handleRect.anchoredPosition;
            handleRect.anchoredPosition = new Vector2(x, p.y);
        }
        if (updateVisuals)
            ApplyAllTargetVisuals(_currentNorm, forceWinnerOnly:true);
    }

    // 拖动中不做胜负判定与成功判定
    void OnHandleValueChanged(float normalized)
    {
        if (!_running) return;
        SetValue(normalized, updateHandle:false, updateVisuals:true);
    }

    void OnHandleDragEnd(float normalized)
    {
        if (!_running || _successFired) return;

        _currentNorm = Mathf.Clamp01(normalized);

        int winner = GetWinnerIndex(_currentNorm);
        if (debugLogs) Debug.Log($"[MultiFocus] Winner = {winner}");

        int goal = (goalTargetIndex >= 0 && goalTargetIndex < targets.Count) ? goalTargetIndex : winner;

        var tgt = targets[goal];
        float half = Mathf.Max(0f, successHalfWidth);
        if (Mathf.Abs(_currentNorm - tgt.bestValue) <= half)
        {
            _successFired = true;

            if (snapHandleOnSuccess)
                StartCoroutine(SnapHandleTo(tgt.bestValue, snapDuration));
            else
                CompleteAndNotify();
        }
    }

    IEnumerator SnapHandleTo(float normalized, float dur)
    {
        normalized = Mathf.Clamp01(normalized);
        if (dur <= 0f)
        {
            SetValue(normalized, updateHandle:true, updateVisuals:true);
            CompleteAndNotify();
            yield break;
        }

        float startX = handleRect.anchoredPosition.x;
        float endX   = Mathf.Lerp(_xMin, _xMax, normalized);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float x = Mathf.Lerp(startX, endX, k);
            var p = handleRect.anchoredPosition;
            handleRect.anchoredPosition = new Vector2(x, p.y);

            float curNorm = Mathf.InverseLerp(_xMin, _xMax, x);
            _currentNorm = curNorm;
            ApplyAllTargetVisuals(curNorm, forceWinnerOnly:true);
            yield return null;
        }

        _currentNorm = normalized;
        ApplyAllTargetVisuals(_currentNorm, forceWinnerOnly:true);
        CompleteAndNotify();
    }

    void CompleteAndNotify()
    {
        if (!_running) return;
        if (debugLogs) Debug.Log("[MultiFocus] Success!");
        StopGame();
        _onSuccess?.Invoke();
    }

    // 只有一个对象最清晰
    void ApplyAllTargetVisuals(float value01, bool forceWinnerOnly)
    {
        if (targets.Count == 0) return;

        // 计算每个对象的 clarity（0 - 1）
        float bestScore = -1f;
        int   winnerIdx = -1;
        var   cache     = new float[targets.Count];

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || !t.Valid) { cache[i] = 0f; continue; }

            float width = (t.blurWidth > 0f) ? t.blurWidth : defaultBlurWidth;
            float delta = Mathf.Abs(value01 - t.bestValue);
            float clarity = 1f - Mathf.Clamp01(delta / Mathf.Max(0.0001f, width)); // 1=清晰, 0=模糊
            cache[i] = clarity;

            if (clarity > bestScore)
            {
                bestScore = clarity;
                winnerIdx = i;
            }
        }

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || !t.Valid) continue;

            float aSharp = (i == winnerIdx) ? cache[i] : 0f;
            t.SetSharpAlpha(aSharp);     // 顶层清晰图
            t.EnsureBlurIsOpaque();      // 底层模糊图始终 alpha=1
        }
    }

    int GetWinnerIndex(float value01)
    {
        float bestScore = -1f;
        int winner = -1;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || !t.Valid) continue;

            float width = (t.blurWidth > 0f) ? t.blurWidth : defaultBlurWidth;
            float delta = Mathf.Abs(value01 - t.bestValue);
            float clarity = 1f - Mathf.Clamp01(delta / Mathf.Max(0.0001f, width));
            if (clarity > bestScore)
            {
                bestScore = clarity;
                winner = i;
            }
        }
        return winner;
    }
}


[Serializable]
public class FocusTarget
{
    [Header("图像")]
    public Image sharp;
    public Image blurred;

    [Header("参数（0~1）")]
    [Tooltip("该物体最清晰的参数值（0~1）")]
    [Range(0f, 1f)] public float bestValue = 0.5f;
    [Tooltip("清晰→模糊过渡宽度，不填/<=0 时使用全局 defaultBlurWidth")]
    [Range(0f, 1f)] public float blurWidth = 0f;

    public bool Valid => sharp && blurred;

    // 把底层模糊图设为不透明（1）
    public void EnsureBlurIsOpaque()
    {
        if (!blurred) return;
        var c = blurred.color;
        if (c.a != 1f) { c.a = 1f; blurred.color = c; }
    }

    // 设置清晰图透明度（0 - 1）
    public void SetSharpAlpha(float a)
    {
        if (!sharp) return;
        var c = sharp.color;
        c.a = Mathf.Clamp01(a);
        sharp.color = c;
    }
}
