using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FocusMinigame : MonoBehaviour, IFocusMinigame
{
    [Header("=== 拍照画面")]
    // 顶层：清晰图
    public Image focusImageSharp;
    // 底层：模糊图
    public Image focusImageBlurred;

    [Header("=== 滑块与刻度条")]
    public RectTransform trackRect;
    public RectTransform handleRect;

    [Header("=== 数值映射（0~1）")]
    [Range(0f, 1f)] public float initialValue = 0.25f;
    [Range(0f, 1f)] public float bestValue = 0.7f;
    [Range(0f, 0.5f)] public float successHalfWidth = 0.06f; // 成功窗口
    [Range(0.01f, 1f)] public float blurWidth = 0.5f;        // 清晰→模糊过渡宽度

    [Header("=== 成功后的吸附")]
    public bool  snapHandleOnSuccess = true;
    public float snapDuration = 0.15f;

    [Header("其他")]
    public bool debugLogs = false;

    // 运行时
    Action _onSuccess;
    bool _running;
    bool _successFired;
    float _xMin, _xMax;

    public void StartGame(Action onSuccess)
    {
        if (_running) { if (debugLogs) Debug.Log("[FocusMinigame] StartGame ignored (already running)."); return; }

        _onSuccess = onSuccess;
        _successFired = false;
        _running = true;

        if (!trackRect || !handleRect || !focusImageSharp || !focusImageBlurred)
        {
            Debug.LogError("[FocusMinigame] 引用未配置完整：需要 trackRect / handleRect / 两张图片。");
            _running = false; onSuccess?.Invoke(); return;
        }

        //确保底层模糊图 alpha=1
        var cb = focusImageBlurred.color; cb.a = 1f; focusImageBlurred.color = cb;

        // 计算滑动范围
        var r = trackRect.rect;
        float halfHandle = handleRect.rect.width * 0.5f;
        _xMin = r.xMin + halfHandle;
        _xMax = r.xMax - halfHandle;

        // 初始位置与视觉
        SetValue(initialValue, updateHandle: true, updateVisuals: true);

        // 安装拖拽（拖动中只更新清晰度；松手判定）
        var dragger = handleRect.GetComponent<FocusHandleDragger>();
        if (dragger == null) dragger = handleRect.gameObject.AddComponent<FocusHandleDragger>();
        dragger.Init(trackRect, OnHandleValueChanged, OnHandleDragEnd);

        if (debugLogs) Debug.Log("[FocusMinigame] StartGame");
    }

    public void StopGame()
    {
        _running = false;
        var dragger = handleRect ? handleRect.GetComponent<FocusHandleDragger>() : null;
        if (dragger) dragger.enabled = false;
    }

    public void SetValue(float normalized, bool updateHandle, bool updateVisuals)
    {
        normalized = Mathf.Clamp01(normalized);
        if (updateHandle)
        {
            float x = Mathf.Lerp(_xMin, _xMax, normalized);
            var p = handleRect.anchoredPosition;
            handleRect.anchoredPosition = new Vector2(x, p.y);
        }
        if (updateVisuals) ApplyFocusVisuals(normalized);
    }

    //拖动仅更新清晰度
    void OnHandleValueChanged(float normalized)
    {
        if (!_running) return;
        ApplyFocusVisuals(normalized);
    }

    //松手再判定
    void OnHandleDragEnd(float normalized)
    {
        if (!_running || _successFired) return;

        float min = bestValue - successHalfWidth;
        float max = bestValue + successHalfWidth;

        if (normalized >= min && normalized <= max)
        {
            _successFired = true;

            if (snapHandleOnSuccess)
                StartCoroutine(SnapHandleTo(bestValue, snapDuration));
            else
            {
                ForceSharpVisuals();
                CompleteAndNotify();
            }
        }
        //未命中保持当前位置与对应清晰度
    }

    IEnumerator SnapHandleTo(float normalized, float dur)
    {
        if (dur <= 0f)
        {
            SetValue(normalized, updateHandle: true, updateVisuals: true);
            ForceSharpVisuals();
            CompleteAndNotify();
            yield break;
        }

        float startX = handleRect.anchoredPosition.x;
        float endX = Mathf.Lerp(_xMin, _xMax, Mathf.Clamp01(normalized));
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float x = Mathf.Lerp(startX, endX, k);
            var p = handleRect.anchoredPosition;
            handleRect.anchoredPosition = new Vector2(x, p.y);

            float curNorm = Mathf.InverseLerp(_xMin, _xMax, x);
            ApplyFocusVisuals(curNorm);
            yield return null;
        }

        ForceSharpVisuals();
        CompleteAndNotify();
    }

    void ForceSharpVisuals()
    {
        var cs = focusImageSharp.color; cs.a = 1f; focusImageSharp.color = cs;
        var cb = focusImageBlurred.color;
    }

    void CompleteAndNotify()
    {
        if (!_running) return;
        if (debugLogs) Debug.Log("[FocusMinigame] Success!");
        StopGame();
        _onSuccess?.Invoke();
    }
    
    void ApplyFocusVisuals(float value01)
    {
        float delta   = Mathf.Abs(value01 - bestValue);
        float clarity = 1f - Mathf.Clamp01(delta / Mathf.Max(0.0001f, blurWidth)); // 1清晰→0模糊

        var cs = focusImageSharp.color;
        cs.a = clarity;
        focusImageSharp.color = cs;
    }
}
