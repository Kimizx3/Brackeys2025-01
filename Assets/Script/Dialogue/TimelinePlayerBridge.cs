using System;
using UnityEngine;
using UnityEngine.Playables;

public class TimelinePlayerBridge : MonoBehaviour, ITimelinePlayer
{
    [Header("Connect your existing manager")]
    public TimelineManager manager;

    [Header("Behavior")]
    public bool stopOthersBeforePlay = true;
    public bool deactivateCanvasOnStop = true;

    [Header("Debug")]
    public bool debugLogs = true;
    public string diagnosticPlayOnStartKey = "";
    public bool diagnosticPlayOnStart = false;

    void Start()
    {
        if (diagnosticPlayOnStart && !string.IsNullOrEmpty(diagnosticPlayOnStartKey))
        {
            if (debugLogs) Debug.Log($"[Bridge] Diagnostic start → Play('{diagnosticPlayOnStartKey}')");
            Play(diagnosticPlayOnStartKey, () => Debug.Log("[Bridge] Diagnostic: stopped callback received."));
        }
    }

    public void Play(string key, Action onComplete)
    {
        if (manager == null || manager.timelines == null || manager.canvases == null)
        {
            if (debugLogs) Debug.LogError("[Bridge] manager/timelines/canvases 为 null，无法播放。");
            onComplete?.Invoke(); return;
        }
        if (manager.timelines.Count == 0)
        {
            if (debugLogs) Debug.LogError("[Bridge] manager.timelines.Count==0。");
            onComplete?.Invoke(); return;
        }
        if (manager.timelines.Count != manager.canvases.Count)
        {
            if (debugLogs) Debug.LogError($"[Bridge] timelines({manager.timelines.Count}) 与 canvases({manager.canvases.Count}) 数量不一致。");
            onComplete?.Invoke(); return;
        }

        int index = ResolveIndex(key);
        if (index < 0 || index >= manager.timelines.Count)
        {
            if (debugLogs)
            {
                Debug.LogError($"[Bridge] 未找到 key='{key}' 对应的时间线。可用列表如下：");
                for (int i = 0; i < manager.timelines.Count; i++)
                {
                    var d = manager.timelines[i];
                    string goName = d ? d.gameObject.name : "NULL";
                    string assetName = (d && d.playableAsset) ? d.playableAsset.name : "(no asset)";
                    Debug.Log($"  #{i}: GO='{goName}', Asset='{assetName}'  Canvas='{(manager.canvases[i] ? manager.canvases[i].name : "NULL")}'");
                }
            }
            onComplete?.Invoke(); return;
        }

        var director = manager.timelines[index];
        var canvas   = manager.canvases[index];

        if (!director)
        {
            if (debugLogs) Debug.LogError($"[Bridge] index={index} 的 PlayableDirector 为 NULL。");
            onComplete?.Invoke(); return;
        }
        if (!director.gameObject.activeInHierarchy)
        {
            if (debugLogs) Debug.LogWarning($"[Bridge] 注意：PlayableDirector '{director.gameObject.name}' 不在激活层级里。");
        }
        if (!director.playableAsset)
        {
            if (debugLogs) Debug.LogError($"[Bridge] PlayableDirector '{director.gameObject.name}' 未设置 PlayableAsset。");
            onComplete?.Invoke(); return;
        }
        if (debugLogs)
        {
            Debug.Log($"[Bridge] 即将播放：GO='{director.gameObject.name}', Asset='{director.playableAsset.name}', TimeScale={Time.timeScale}, UpdateMode={director.timeUpdateMode}");
        }

        if (stopOthersBeforePlay)
        {
            foreach (var d in manager.timelines) if (d) d.Stop();
            foreach (var c in manager.canvases) if (c) c.SetActive(false);
        }

        if (canvas) canvas.SetActive(true);

        void Handler(PlayableDirector d)
        {
            d.stopped -= Handler;
            if (debugLogs) Debug.Log($"[Bridge] stopped 事件收到：'{director.gameObject.name}' 播放完毕。");
            if (deactivateCanvasOnStop && canvas) canvas.SetActive(false);
            onComplete?.Invoke();
        }
        director.stopped -= Handler;
        director.stopped += Handler;

        director.time = 0;
        director.Play();

        if (debugLogs) Debug.Log($"[Bridge] 已调用 Play()：'{director.gameObject.name}'。");
    }

    int ResolveIndex(string key)
    {
        if (!string.IsNullOrEmpty(key) && int.TryParse(key, out int idx))
            if (idx >= 0 && idx < manager.timelines.Count) return idx;

        for (int i = 0; i < manager.timelines.Count; i++)
        {
            var d = manager.timelines[i];
            if (d && d.gameObject.name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        for (int i = 0; i < manager.timelines.Count; i++)
        {
            var d = manager.timelines[i];
            if (d && d.playableAsset && d.playableAsset.name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        for (int i = 0; i < manager.canvases.Count; i++)
        {
            var c = manager.canvases[i];
            if (c && c.name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
