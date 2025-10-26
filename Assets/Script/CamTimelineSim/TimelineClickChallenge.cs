using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class TimelineClickChallenge : MonoBehaviour, IFocusMinigame
{
    [Header("必填：播放资源")]
    public PlayableDirector director;

    [Header("时间轴 UI")]
    public RectTransform progressTrack;
    public RectTransform pointer;

    [Header("机位按钮")]
    public List<CameraToggleButton> cameraButtons = new List<CameraToggleButton>();

    [Header("判定配置（单位：秒）")]
    public List<ClickWindow> windows = new List<ClickWindow>();

    [Header("失败弹窗")]
    public GameObject failDialog;
    public Text failDialogText;
    [TextArea] public string failMessage = "操作失败！请在指定时间点击对应机位。点击任意处重试。";

    [Header("显示设置")]
    public Color defaultWindowColor = new Color(1, 1, 1, 0.3f);
    public bool showPointer = true;

    [Header("启动/同步")]
    public bool autoPlayDirector = true;
    public float startAtTime = 0f;
    public float waitDirectorPlayingTimeout = 2.0f;
    public bool requirePlayingForJudgement = true;

    [Header("诊断 / 自测")]
    [Tooltip("强制打印详细日志（无需再勾 debug）。")]
    public bool verboseAlways = true;
    [Tooltip("场景运行后自动调用 StartGame（用于单独测试，不依赖对话 Gate）。")]
    public bool autoStartForDebug = false;

    // runtime
    private Action _onSuccess;
    private bool _running;
    private bool _failed;
    private bool _completed;
    private bool _awaitingExternalPlay;
    private float _duration;
    private readonly List<GameObject> _markers = new List<GameObject>();
    private Coroutine _loopCoro;
    
    void Awake()
    {
        Log("Awake()");
        if (failDialog) failDialog.SetActive(false);
    }
    void OnEnable()  { Log("OnEnable()"); }
    void Start()
    {
        Log("Start()");
        if (autoStartForDebug) StartGame(() => Log("StartGame finished (auto)"));
    }
    void OnDisable() { Log("OnDisable()"); }
    void OnDestroy() { Log("OnDestroy()"); }

    // ====== IFocusMinigame ======
    public void StartGame(Action onSuccess)
    {
        Log("StartGame() begin");
        _onSuccess = onSuccess;
        _failed = _completed = false;
        _running = true;
        _awaitingExternalPlay = false;

        if (!director || !progressTrack)
        {
            LogError("引用未配置完整：director / progressTrack。");
            _running = false; onSuccess?.Invoke(); return;
        }

        EnsureEventSystem();
        ForceDirectorToGameTime(director);

        // 注入/复位按钮
        foreach (var btn in cameraButtons)
        {
            if (!btn) { LogWarn("cameraButtons 列表里有空元素"); continue; }
            btn.SetController(this);
            btn.ResetVisualToStopped();
            if (!btn.gameObject.activeInHierarchy) LogWarn($"按钮 {btn.name} 未激活，无法点击。");
        }

        // 定位时间 + 计算时长
        director.RebuildGraph();
        director.time = (double)Mathf.Max(0f, startAtTime);
        director.Evaluate();
        _duration = (director.duration > 0) ? (float)director.duration : GuessDurationFromWindows();
        Log($"Director prepared. time={director.time:F3}s, duration={_duration:F3}s");
        
        BuildMarkers();
        if (pointer) pointer.gameObject.SetActive(showPointer);
        if (pointer && showPointer) SetPointerRatio(_duration > 0 ? (float)director.time / _duration : 0f);
        
        director.played  -= OnDirectorPlayed;
        director.stopped -= OnDirectorStopped;
        director.played  += OnDirectorPlayed;
        director.stopped += OnDirectorStopped;

        // 开播
        if (autoPlayDirector)
        {
            Log("Calling director.Play() (autoPlayDirector=ON)");
            director.Play();
        }
        else
        {
            _awaitingExternalPlay = true;
            StartCoroutine(CoWaitDirectorPlaying());
            Log("等待外部播放（autoPlayDirector=OFF）");
        }

        // 开主循环
        if (_loopCoro != null) StopCoroutine(_loopCoro);
        _loopCoro = StartCoroutine(Loop());

        Log("StartGame() end");
    }

    public void StopGame()
    {
        Log("StopGame()");
        _running = false;
        if (_loopCoro != null) { StopCoroutine(_loopCoro); _loopCoro = null; }

        foreach (var btn in cameraButtons) if (btn) btn.SetController(null);

        if (director)
        {
            director.played  -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }

        ClearMarkers();
        if (failDialog) failDialog.SetActive(false);
    }
    
    void OnDirectorPlayed(PlayableDirector d)
    {
        Log("Director -> Played");
        _awaitingExternalPlay = false;
        _duration = (d.duration > 0) ? (float)d.duration : GuessDurationFromWindows();
        if (pointer && showPointer) SetPointerRatio(_duration > 0 ? (float)d.time / _duration : 0f);
    }
    void OnDirectorStopped(PlayableDirector d)
    {
        Log("Director -> Stopped（仅记录，不在这里判成功）");
    }

    IEnumerator CoWaitDirectorPlaying()
    {
        float t0 = Time.unscaledTime;
        while (_running && _awaitingExternalPlay &&
               director.state != PlayState.Playing &&
               (Time.unscaledTime - t0) < Mathf.Max(0.1f, waitDirectorPlayingTimeout))
        {
            yield return null;
        }
        Log($"外部播放等待结束，当前状态：{director.state}");
        _awaitingExternalPlay = false;
    }
    
    public void OnCameraButtonClicked(CameraToggleButton btn)
    {
        if (!_running || _failed || _completed) { Log("点击被忽略：玩法未运行/已失败/已完成"); return; }

        if (requirePlayingForJudgement && director.state != PlayState.Playing)
        {
            Log("（点击忽略）Director 未处于 Playing。");
            return;
        }

        float t = (float)director.time;
        bool hitAny = false;

        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null || w.fulfilled) continue;
            if (t >= w.start && t <= w.end && w.cameraId == btn.cameraId)
            {
                hitAny = true;
                w.fulfilled = true;
                btn.ToggleState();
                Log($"✔ 命中：{btn.cameraId} @ {t:F2}s in [{w.start:F2},{w.end:F2}]");
            }
        }

        if (!hitAny)
        {
            Log("✖ 错误点击：当前不在任何匹配窗口。");
            FailAndShowDialog();
        }
    }
    
    IEnumerator Loop()
    {
        Log("Loop() start");
        while (_running && !_failed && !_completed)
        {
            // 指针刷新
            if (pointer && showPointer)
            {
                float ratio = Mathf.Clamp01(_duration > 0 ? (float)director.time / _duration : 0f);
                SetPointerRatio(ratio);
            }

            if (!requirePlayingForJudgement || director.state == PlayState.Playing)
            {
                float t = (float)director.time;
                
                for (int i = 0; i < windows.Count; i++)
                {
                    var w = windows[i];
                    if (w == null || w.fulfilled) continue;
                    if (t > w.end + 0.0001f)
                    {
                        Log($"✖ 错过窗口：{w.cameraId} [{w.start:F2},{w.end:F2}]，当前 {t:F2}");
                        FailAndShowDialog();
                        break;
                    }
                }
                
                if (!_failed && director.state != PlayState.Playing && (float)director.time >= _duration - 0.0001f)
                {
                    _completed = true;
                    Log("✅ 成功：Timeline 播放完毕");
                    yield return null;
                    _running = false;
                    _onSuccess?.Invoke();
                    yield break;
                }
            }

            yield return null;
        }
        Log("Loop() stop");
    }
    
    void FailAndShowDialog()
    {
        if (_failed) return;
        _failed = true;

        if (director) director.Pause();
        Log("失败：暂停 Timeline 并显示弹窗/重开");

        if (failDialog)
        {
            if (failDialogText) failDialogText.text = string.IsNullOrEmpty(failMessage) ? "失败" : failMessage;
            failDialog.SetActive(true);

            var closer = failDialog.GetComponent<TapToClose>();
            if (!closer) closer = failDialog.AddComponent<TapToClose>();
            closer.onClosed = () =>
            {
                if (failDialog) failDialog.SetActive(false);
                foreach (var btn in cameraButtons) if (btn) btn.ResetVisualToStopped();
                RestartChallenge();
            };
        }
        else
        {
            RestartChallenge();
        }
    }

    void RestartChallenge()
    {
        Log("🔁 RestartChallenge()");
        for (int i = 0; i < windows.Count; i++) if (windows[i] != null) windows[i].fulfilled = false;

        director.RebuildGraph();
        ForceDirectorToGameTime(director);
        director.time = 0.0;
        director.Evaluate();
        _duration = (director.duration > 0) ? (float)director.duration : GuessDurationFromWindows();

        ClearMarkers();
        BuildMarkers();

        if (pointer && showPointer) SetPointerRatio(0f);

        _failed = false;
        _completed = false;

        if (autoPlayDirector)
        {
            Log("重开后调用 Play()");
            director.Play();
        }
        else
        {
            _awaitingExternalPlay = true;
            StartCoroutine(CoWaitDirectorPlaying());
        }

        if (_loopCoro == null) _loopCoro = StartCoroutine(Loop());
    }

    // ====== UI：标记 / 指针 ======
    void BuildMarkers()
    {
        ClearMarkers();
        if (!progressTrack) return;

        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null) continue;

            var go = new GameObject($"Win_{i}_{w.cameraId}", typeof(RectTransform), typeof(Image));
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
        Log($"BuildMarkers(): {_markers.Count} windows");
    }

    void ClearMarkers()
    {
        for (int i = 0; i < _markers.Count; i++)
            if (_markers[i]) Destroy(_markers[i]);
        _markers.Clear();
    }

    void SetPointerRatio(float r)
    {
        if (!pointer) return;
        r = Mathf.Clamp01(r);
        pointer.anchorMin = new Vector2(r, pointer.anchorMin.y);
        pointer.anchorMax = new Vector2(r, pointer.anchorMax.y);
        pointer.anchoredPosition = new Vector2(0f, pointer.anchoredPosition.y);
    }

    float GuessDurationFromWindows()
    {
        float m = 0f;
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w == null) continue;
            m = Mathf.Max(m, w.end);
        }
        if (m <= 0f) m = 10f;
        Log($"GuessDurationFromWindows() => {m:F3}s");
        return m;
    }
    
    void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (!es)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if UNITY_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Log("已创建 EventSystem + InputSystemUIInputModule");
#else
            go.AddComponent<StandaloneInputModule>();
            Log("已创建 EventSystem + StandaloneInputModule");
#endif
        }
        else
        {
#if UNITY_INPUT_SYSTEM
            if (!es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Log("已补充 InputSystemUIInputModule");
            }
#else
            if (!es.GetComponent<StandaloneInputModule>())
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
                Log("已补充 StandaloneInputModule");
            }
#endif
        }
    }

    void ForceDirectorToGameTime(PlayableDirector d)
    {
        if (!d) return;
        d.timeUpdateMode = DirectorUpdateMode.GameTime;
        var g = d.playableGraph;
        if (g.IsValid()) g.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        d.extrapolationMode = DirectorWrapMode.None;
        Log($"Director Set GameTime. state={d.state}, time={d.time:F3}, dur={(float)d.duration:F3}");
    }
    
    void Log(string msg)     { if (verboseAlways) Debug.Log($"[TCC:{name}] {msg}", this); }
    void LogWarn(string msg) { if (verboseAlways) Debug.LogWarning($"[TCC:{name}] {msg}", this); }
    void LogError(string msg){ Debug.LogError($"[TCC:{name}] {msg}", this); }
}

[Serializable]
public class ClickWindow
{
    public string cameraId = "A";
    public float start = 1f, end = 2f;
    public Color color = new Color(1, 1, 1, 0.3f);
    [NonSerialized] public bool fulfilled;
}
