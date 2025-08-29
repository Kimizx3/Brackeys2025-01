using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 极简点播版 TimelineManager：
/// - 不自动连播、不切场景；
/// - 仅提供 Play(key/index, onComplete)；
/// - 默认严格以 PlayableDirector 的 GameObject 名匹配；
/// - 播放前停掉其它导演机并关其它 Canvas（可配置），播完可关本 Canvas；
/// - 播放时强制复位（Stop/Reset/RebuildGraph/Evaluate）避免“残留帧/错播/重播”。
/// </summary>
public class TimelineManager : MonoBehaviour, ITimelinePlayer
{
    [Header("Timelines & Canvases (1:1 可选)")]
    public List<PlayableDirector> timelines = new List<PlayableDirector>();
    public List<GameObject> canvases = new List<GameObject>(); // 与 timelines 数量一致才会管理显示/隐藏

    [Header("Play Behavior")]
    public bool stopOthersBeforePlay = true;       // 点播前停掉其它导演机
    public bool deactivateOthersCanvas = true;     // 点播前关闭其它 Canvas
    public bool deactivateThisCanvasOnStop = true; // 播完关闭当前这条的 Canvas

    public enum KeyMatchMode { GameObjectNameOnly, GameObjectOrAssetOrIndex }
    [Header("Key Matching")]
    public KeyMatchMode keyMode = KeyMatchMode.GameObjectNameOnly; // 默认严格 GO 名匹配
    public bool caseSensitive = true;

    [Header("Debug")]
    public bool debugLogs = false;

    // 已注册的 stopped 回调，防重复叠加
    readonly Dictionary<PlayableDirector, Action<PlayableDirector>> _stopHandlers = new();

    void Awake()
    {
        // 初始化为干净状态：不自动播放
        SafeStopAll();
        SafeDeactivateAllCanvases();
    }

    void OnDestroy() => UnsubscribeAll();

    // ============ ITimelinePlayer：按 key 点播 ============
    public void Play(string key, Action onComplete)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (debugLogs) Debug.LogWarning("[TimelineManager] key 为空，直接回调。");
            onComplete?.Invoke(); return;
        }
        int index = ResolveIndex(key);
        if (index < 0)
        {
            if (debugLogs)
            {
                Debug.LogError($"[TimelineManager] 未找到 key='{key}' 对应的 PlayableDirector（模式={keyMode}）。列表：");
                DumpDirectors();
            }
            onComplete?.Invoke(); return;
        }
        Play(index, onComplete);
    }

    // ============ 按索引点播 ============
    public void Play(int index, Action onComplete = null)
    {
        if (timelines == null || timelines.Count == 0)
        {
            if (debugLogs) Debug.LogError("[TimelineManager] timelines 为空。");
            onComplete?.Invoke(); return;
        }
        if (index < 0 || index >= timelines.Count)
        {
            if (debugLogs) Debug.LogError($"[TimelineManager] index 越界：{index}。");
            onComplete?.Invoke(); return;
        }

        var director = timelines[index];
        if (!director)
        {
            if (debugLogs) Debug.LogError($"[TimelineManager] timelines[{index}] 为 NULL。");
            onComplete?.Invoke(); return;
        }

        // 播放前统一清场
        if (stopOthersBeforePlay) SafeStopAll(except: director);
        if (deactivateOthersCanvas) SafeDeactivateAllCanvases();

        // 1:1 Canvas 管理（只有数量一致时才生效）
        if (canvases != null && canvases.Count == timelines.Count && canvases[index] != null)
            canvases[index].SetActive(true);

        // 解绑旧回调，绑定新回调
        if (_stopHandlers.TryGetValue(director, out var old))
        {
            director.stopped -= old;
            _stopHandlers.Remove(director);
        }
        void Handler(PlayableDirector d)
        {
            d.stopped -= Handler;
            _stopHandlers.Remove(d);

            if (deactivateThisCanvasOnStop && canvases != null && canvases.Count == timelines.Count)
            {
                var canvas = canvases[index];
                if (canvas) canvas.SetActive(false);
            }

            if (debugLogs) Debug.Log($"[TimelineManager] '{director.gameObject.name}' 播放完毕。");
            onComplete?.Invoke();
        }
        director.stopped += Handler;
        _stopHandlers[director] = Handler;

        // 强制从头播放
        ForcePlayFromStart(director);

        if (debugLogs)
        {
            var assetName = director.playableAsset ? director.playableAsset.name : "(no asset)";
            Debug.Log($"[TimelineManager] Play → index={index}, GO='{director.gameObject.name}', Asset='{assetName}'");
        }
    }

    // ============ 工具函数 ============
    int ResolveIndex(string rawKey)
    {
        string key = caseSensitive ? rawKey.Trim() : rawKey.Trim().ToLowerInvariant();

        // 1) 严格按 GameObject 名称（默认）
        if (keyMode == KeyMatchMode.GameObjectNameOnly)
        {
            for (int i = 0; i < timelines.Count; i++)
            {
                var d = timelines[i];
                if (!d) continue;
                string goName = caseSensitive ? d.gameObject.name : d.gameObject.name.ToLowerInvariant();
                if (goName == key) return i;
            }
            return -1;
        }

        // 2) 兼容模式：GO 名 → Asset 名 → 数字索引
        for (int i = 0; i < timelines.Count; i++)
        {
            var d = timelines[i];
            if (!d) continue;
            string goName = caseSensitive ? d.gameObject.name : d.gameObject.name.ToLowerInvariant();
            if (goName == key) return i;
        }
        for (int i = 0; i < timelines.Count; i++)
        {
            var d = timelines[i];
            if (!d || !d.playableAsset) continue;
            string assetName = caseSensitive ? d.playableAsset.name : d.playableAsset.name.ToLowerInvariant();
            if (assetName == key) return i;
        }
        if (int.TryParse(rawKey.Trim(), out int idx) && idx >= 0 && idx < timelines.Count) return idx;

        return -1;
    }

    void ForcePlayFromStart(PlayableDirector director)
    {
        if (!director.gameObject.activeInHierarchy)
            director.gameObject.SetActive(true);

        director.Stop();
        director.time = 0;
        director.RebuildGraph();
        director.Play();
        director.Evaluate(); // 立刻评估到首帧
    }

    void SafeStopAll(PlayableDirector except = null)
    {
        if (timelines == null) return;
        foreach (var d in timelines)
        {
            if (!d || d == except) continue;
            try { d.Stop(); } catch { /* ignore */ }
        }
    }

    void SafeDeactivateAllCanvases()
    {
        if (canvases == null) return;
        if (timelines != null && canvases.Count == timelines.Count)
        {
            foreach (var c in canvases) if (c) c.SetActive(false);
        }
    }

    void UnsubscribeAll()
    {
        foreach (var kv in _stopHandlers)
            if (kv.Key != null) kv.Key.stopped -= kv.Value;
        _stopHandlers.Clear();
    }

    void DumpDirectors()
    {
        for (int i = 0; i < timelines.Count; i++)
        {
            var d = timelines[i];
            string goName = d ? d.gameObject.name : "NULL";
            string assetName = (d && d.playableAsset) ? d.playableAsset.name : "(no asset)";
            string canvasName = (canvases != null && i < canvases.Count && canvases[i]) ? canvases[i].name : "(no canvas)";
            Debug.Log($"  #{i}: GO='{goName}', Asset='{assetName}', Canvas='{canvasName}'");
        }
    }
}
