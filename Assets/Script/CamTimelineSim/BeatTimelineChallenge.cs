using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class BeatTimelineChallenge : MonoBehaviour, IFocusMinigame
{
    [Header("A. 播放资源（二选一或都配）")]
    public PlayableDirector director;                           // 直接播放这条 Director
    public UnityEngine.Object timelinePlayer;                   // 你的 TimelineManager（含 PlayByKey/StopByKey）
    public string playKeyOnStart;                               // 点播的 key（如 TL_Intro）

    [Header("B. 进度 UI")]
    public RectTransform progressTrack;
    public RectTransform pointer;

    [Header("C. 机位按钮（与窗口 cameraId 对应）")]
    public List<CameraToggleButton> cameraButtons = new List<CameraToggleButton>();

    [Header("D. 时间窗（秒）")]
    public List<BeatWindow> windows = new List<BeatWindow>();
    
    [Header("判定完成条件")]
    [Tooltip("命中所有窗口后立即通过（不再等待时间轴结束）")]
    public bool completeWhenAllWindowsHit = true;

    [Tooltip("若没有配置任何窗口，是否直接判定为通过")]
    public bool passIfNoWindows = true;


    [Header("E. 失败弹窗")]
    public GameObject failDialog;
    public Text failDialogText;
    [TextArea] public string failMessage = "失败：请在指定时间点击对应机位。\n点击任意位置重试。";

    [Header("F. 显示")]
    public Color defaultWindowColor = new Color(1,1,1,0.3f);
    public bool showPointer = true;

    [Header("G. 同步/兜底策略")]
    public bool autoPlayDirector = true;
    public float startAtTime = 0f;
    public float waitExternalPlayTimeout = 1.0f;
    public int   playRetryFrames = 5;
    public float playRetryInterval = 0.05f;
    public bool  enableInternalTimerFallback = true;
    public bool  requirePlayingToJudge = false;

    [Header("H. 成功回调控制")]
    public bool disableComponentAfterSuccess = true;

    [Header("I. 诊断 & 事件钩子")]
    public bool log = true;
    [Tooltip("（可选）额外触发的成功事件：可在 Inspector 里把 DialogueController.Next() 或自定义方法拖进来")]
    public UnityEvent onGateSuccess;
    [Tooltip("（可选）额外触发的失败事件")]
    public UnityEvent onGateFail;

    // 运行时
    private Action _onSuccess;
    private bool _running, _failed, _completed;
    private float _duration;
    private Coroutine _loopCo;
    private readonly List<GameObject> _markers = new();

    // 兜底计时器
    private bool _usingInternalTimer = false;
    private float _tInternal = 0f;

    // 单次回调保护
    private bool _successFired = false;
    
    // [ContextMenu("Force Pass For Debug")]
    // public void ForcePassForDebug()
    // {
    //     // 忽略判定，直接按“成功”处理，用于验证：回调是否把对话推进
    //     StopGame();                 // 先清理自身（协程/事件）
    //     try { _onSuccess?.Invoke(); } catch (Exception e) { Debug.LogError("[BeatChallenge] onSuccess error: " + e.Message); }
    //     if (disableComponentAfterSuccess) this.enabled = false;  // 避免后续被误触重启
    //     if (log) Debug.Log("[BeatChallenge] ForcePassForDebug() -> callback invoked");
    // }

    // ---- IFocusMinigame ----
    public void StartGame(Action onSuccess)
    {
        _onSuccess = onSuccess;
        _running = true; _failed = false; _completed = false; _successFired = false;
        _usingInternalTimer = false; _tInternal = 0f;

        EnsureEventSystem();

        // 注入按钮
        foreach (var b in cameraButtons)
        {
            if (!b) continue;
            b.SetController(this);
            b.ResetVisualToStopped();
        }

        // 打印 Director 初始状态
        if (log)
        {
            string dName = director ? director.name : "null";
            Debug.Log($"[BeatChallenge] StartGame() director={dName} autoPlay={autoPlayDirector}");
        }

        // 准备 Director & 时长
        PrepareDirectorGraph();
        _duration = CalcDuration();
        if (_duration <= 0f) _duration = 10f;
        if (log) Debug.Log($"[BeatChallenge] duration={_duration:F3}s (source={(director && director.duration>0? "director":"windows")})");

        // UI 初始化
        RebuildMarkers();
        if (pointer) pointer.gameObject.SetActive(showPointer);
        UpdatePointer(0f);

        // 点播（两通道都打日志）
        if (director)
        {
            StartCoroutine(CoPlayDirectorMultiRetry());
            
            // 激活父节点（Canvas）以保证 Graph 可执行
            if (director.transform.parent != null)
            {
                var parent = director.transform.parent.gameObject;
                if (!parent.activeSelf) parent.SetActive(true);
            }

            // 彻底脱离 Manager：只操作这条 Director
            director.RebuildGraph();
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0;
            director.Evaluate();
            director.Play();
            Debug.Log($"[BeatChallenge] Direct Play: {director.name}");
        }
        if (timelinePlayer && !string.IsNullOrEmpty(playKeyOnStart))
        {
            if (log) Debug.Log($"[BeatChallenge] Try PlayByKey('{playKeyOnStart}') via {timelinePlayer.name}");
            SafeInvokePlayByKey(timelinePlayer, playKeyOnStart);
        }

        RestartLoop();
    }

    public void StopGame()
    {
        if (!_running) return;
        if (log) Debug.Log("[BeatChallenge] StopGame()");
        _running = false;

        if (_loopCo != null) { StopCoroutine(_loopCo); _loopCo = null; }
        foreach (var b in cameraButtons) if (b) b.SetController(null);

        ClearMarkers();
        if (failDialog) failDialog.SetActive(false);
    }

    // ---- 播放相关 ----
    void PrepareDirectorGraph()
    {
        if (!director) return;
        director.RebuildGraph();
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        if (director.playableGraph.IsValid())
            director.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        director.extrapolationMode = DirectorWrapMode.None;

        director.time = (double)Mathf.Max(0f, startAtTime);
        director.Evaluate();

        if (log)
        {
            Debug.Log($"[BeatChallenge] PrepareDirector: state={director.state}, time={(float)director.time:F3}, dur={(float)director.duration:F3}");
        }
    }

    IEnumerator CoPlayDirectorMultiRetry()
    {
        if (!director) yield break;

        if (autoPlayDirector)
        {
            for (int i = 0; i < Mathf.Max(1, playRetryFrames); i++)
            {
                director.Play();
                if (log) Debug.Log($"[BeatChallenge] director.Play() try#{i+1} → state={director.state}");
                yield return new WaitForSeconds(playRetryInterval);
                if (director.state == PlayState.Playing)
                {
                    if (log) Debug.Log("[BeatChallenge] Director entered Playing.");
                    yield break;
                }
            }
            if (log) Debug.LogWarning("[BeatChallenge] Director.Play() retried and failed → may fallback");
        }
        else
        {
            float t0 = Time.unscaledTime;
            if (log) Debug.Log("[BeatChallenge] Waiting external Play...");
            while (director.state != PlayState.Playing && Time.unscaledTime - t0 < waitExternalPlayTimeout)
                yield return null;
            if (log) Debug.Log($"[BeatChallenge] External wait done. state={director.state}");
        }

        if (director.state != PlayState.Playing && enableInternalTimerFallback)
        {
            _usingInternalTimer = true;
            _tInternal = (float)director.time;
            if (log) Debug.LogWarning("[BeatChallenge] Fallback: Internal timer engaged.");
        }
    }

    float CalcDuration()
    {
        float d = 0f;
        if (director && director.duration > 0) d = (float)director.duration;
        if (d <= 0f)
        {
            for (int i = 0; i < windows.Count; i++)
                if (windows[i] != null) d = Mathf.Max(d, windows[i].end);
        }
        return d;
    }

    // ---- 按钮点击 ----
    public void OnCameraButtonClicked(CameraToggleButton btn)
    {
        float t = CurrentTime();
        if (!CanJudgeNow(t))
        {
            if (log) Debug.Log($"[BeatChallenge] Click ignored. running={_running} failed={_failed} completed={_completed} playing={IsPlaying()}");
            return;
        }

        bool hit = false;
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null || w.fulfilled) continue;
            if (t >= w.start && t <= w.end && w.cameraId == btn.cameraId)
            {
                w.fulfilled = true;
                hit = true;
                btn.ToggleState();
                
                // 命中后立即检查是否全部完成
                if (completeWhenAllWindowsHit && AllWindowsFulfilled())
                {
                    // 立刻判定成功：先清理，再回调推进下一步
                    StopGame();
                    if (log) Debug.Log("[BeatChallenge] ✅ all windows fulfilled -> callback");
                    try { _onSuccess?.Invoke(); } catch (Exception e) { Debug.LogError(e.Message); }
                    if (disableComponentAfterSuccess) this.enabled = false;
                    return; // 不再继续后续判定
                }

                if (log) Debug.Log($"[BeatChallenge] ✔ HIT {btn.cameraId} @ {t:F2} in [{w.start:F2},{w.end:F2}]");
            }
        }

        if (!hit) FailAndPrompt("Bad timing or camera mismatch");
    }

    // ---- 主循环 ----
    void RestartLoop()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = StartCoroutine(CoLoop());
    }

    IEnumerator CoLoop()
    {
        while (_running && !_failed && !_completed)
        {
            if (_usingInternalTimer) _tInternal += Time.deltaTime;

            float t = CurrentTime();
            UpdatePointer(t);

            if (CanJudgeNow(t))
            {
                // 超时失败
                for (int i = 0; i < windows.Count; i++)
                {
                    var w = windows[i];
                    if (w == null || w.fulfilled) continue;
                    if (t > w.end + 0.0001f)
                    {
                        FailAndPrompt($"Miss window {w.cameraId} [{w.start:F2},{w.end:F2}] at t={t:F2}");
                        break;
                    }
                }

                // 成功：到达末尾
                bool finished = t >= _duration - 0.0001f;
                bool directorEnded = !director || director.state != PlayState.Playing;
                if (!_failed && finished && (_usingInternalTimer || directorEnded))
                {
                    _completed = true;
                    if (log) Debug.Log("[BeatChallenge] -> SUCCESS(decided).");

                    StopGame();              // 先收尾
                    FireSuccessOnce();       // 再回调（含日志）
                    yield break;
                }
            }

            yield return null;
        }
    }

    // ---- 失败 & 重开 ----
    void FailAndPrompt(string reason)
    {
        if (_failed) return;
        _failed = true;

        if (director && director.state == PlayState.Playing) director.Pause();
        if (log) Debug.LogWarning($"[BeatChallenge] -> FAIL: {reason}");
        try { onGateFail?.Invoke(); } catch (Exception e) { Debug.LogWarning($"[BeatChallenge] onGateFail exception: {e.Message}"); }

        if (failDialog)
        {
            if (failDialogText) failDialogText.text = string.IsNullOrEmpty(failMessage) ? "失败" : failMessage;
            failDialog.SetActive(true);

            var closer = failDialog.GetComponent<TapToClose>();
            if (!closer) closer = failDialog.AddComponent<TapToClose>();
            closer.onClosed = () =>
            {
                if (failDialog) failDialog.SetActive(false);
                foreach (var b in cameraButtons) if (b) b.ResetVisualToStopped();
                RestartAll();
            };
        }
        else
        {
            foreach (var b in cameraButtons) if (b) b.ResetVisualToStopped();
            RestartAll();
        }
    }

    void RestartAll()
    {
        for (int i = 0; i < windows.Count; i++) if (windows[i] != null) windows[i].fulfilled = false;

        PrepareDirectorGraph();
        _duration = CalcDuration();
        if (_duration <= 0f) _duration = 10f;

        _usingInternalTimer = false; _tInternal = 0f;

        ClearMarkers(); RebuildMarkers(); UpdatePointer(0f);

        _failed = false; _completed = false;

        if (director) StartCoroutine(CoPlayDirectorMultiRetry());
        if (timelinePlayer && !string.IsNullOrEmpty(playKeyOnStart))
            SafeInvokePlayByKey(timelinePlayer, playKeyOnStart);

        RestartLoop();
        if (log) Debug.Log("[BeatChallenge] RestartAll()");
    }

    // ---- UI ----
    void RebuildMarkers()
    {
        ClearMarkers();
        if (!progressTrack) return;

        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null) continue;

            var go = new GameObject($"Win_{w.cameraId}_{w.start:F2}-{w.end:F2}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(progressTrack, false);
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            img.color = w.color.a > 0 ? w.color : defaultWindowColor;

            float a0 = Mathf.Clamp01(_duration > 0 ? w.start / _duration : 0f);
            float a1 = Mathf.Clamp01(_duration > 0 ? w.end   / _duration : 0f);
            rt.anchorMin = new Vector2(a0, 0f);
            rt.anchorMax = new Vector2(a1, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _markers.Add(go);
        }
    }

    void ClearMarkers()
    {
        for (int i = 0; i < _markers.Count; i++)
            if (_markers[i]) Destroy(_markers[i]);
        _markers.Clear();
    }

    void UpdatePointer(float t)
    {
        if (!pointer || !showPointer) return;
        float r = Mathf.Clamp01(_duration > 0 ? t / _duration : 0f);
        pointer.anchorMin = new Vector2(r, pointer.anchorMin.y);
        pointer.anchorMax = new Vector2(r, pointer.anchorMax.y);
        pointer.anchoredPosition = new Vector2(0f, pointer.anchoredPosition.y);
    }

    // ---- 工具 ----
    float CurrentTime()
    {
        if (_usingInternalTimer) return _tInternal;
        return director ? (float)director.time : _tInternal;
    }
    bool CanJudgeNow(float t)
    {
        if (!_running || _failed || _completed) return false;
        if (_usingInternalTimer) return true;
        if (!requirePlayingToJudge) return true;
        return IsPlaying();
    }
    bool IsPlaying() => director && director.state == PlayState.Playing;

    void EnsureEventSystem()
    {
        if (!FindFirstObjectByType<EventSystem>())
        {
#if UNITY_INPUT_SYSTEM
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
            if (log) Debug.Log("[BeatChallenge] EventSystem created");
        }
    }

    void FireSuccessOnce()
    {
        if (_successFired) return;
        _successFired = true;

        if (log) Debug.Log("[BeatChallenge] CALLBACK(invoking) _onSuccess + UnityEvent");
        try { _onSuccess?.Invoke(); } catch (Exception e) { Debug.LogError($"[BeatChallenge] _onSuccess exception: {e.Message}"); }
        try { onGateSuccess?.Invoke(); } catch (Exception e) { Debug.LogWarning($"[BeatChallenge] onGateSuccess exception: {e.Message}"); }
        if (log) Debug.Log("[BeatChallenge] CALLBACK(done)");

        if (disableComponentAfterSuccess) this.enabled = false;
    }

    static void SafeInvokePlayByKey(UnityEngine.Object obj, string key)
    {
        if (!obj || string.IsNullOrEmpty(key)) return;
        var m = obj.GetType().GetMethod("PlayByKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            try { m.Invoke(obj, new object[] { key }); }
            catch (Exception e) { Debug.LogWarning($"[BeatChallenge] PlayByKey({key}) failed: {e.Message}"); }
        }
        else
        {
            Debug.LogWarning("[BeatChallenge] timelinePlayer has no PlayByKey(string)");
        }
    }
    
    bool AllWindowsFulfilled()
    {
        if (windows == null || windows.Count == 0) return passIfNoWindows;
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null) continue;
            if (!w.fulfilled) return false;
        }
        return true;
    }

    
    
}

[Serializable]
public class BeatWindow
{
    public string cameraId = "A";
    public float start = 1f, end = 2f;
    public Color color = new Color(1,1,1,0.3f);
    [NonSerialized] public bool fulfilled;
}
