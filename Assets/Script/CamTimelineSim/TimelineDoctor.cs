using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public class TimelineDoctor : MonoBehaviour
{
    [Header("自动体检/修复")]
    public bool diagnoseOnStart = true;
    public bool tryAutoFixOnStart = true;
    public bool autoPlayAfterFix = true;

    [Header("（可选）指定 Director；留空则自动 GetComponent")]
    public PlayableDirector director;

    void Reset() { if (!director) director = GetComponent<PlayableDirector>(); }
    void Awake() { if (!director) director = GetComponent<PlayableDirector>(); }

    void Start()
    {
        if (!director) { Debug.LogError("[TimelineDoctor] 没有找到 PlayableDirector"); return; }
        if (diagnoseOnStart) DiagnoseNow();
        if (tryAutoFixOnStart) FixItNow(autoPlayAfterFix);
    }

    // 右上角 “⋮” → 立即体检
    [ContextMenu("Diagnose Now")]
    public void DiagnoseNow()
    {
        if (!director) { Debug.LogError("[TimelineDoctor] Director=null"); return; }

        var sb = new StringBuilder();
        sb.AppendLine("===== TimelineDoctor: Diagnose =====");
        sb.AppendLine($"GO: {director.gameObject.name} (activeInHierarchy={director.gameObject.activeInHierarchy})");
        sb.AppendLine($"Director.state={director.state} time={director.time:F3} duration={(float)director.duration:F3}");
        sb.AppendLine($"timeUpdateMode={director.timeUpdateMode} extrapolationMode={director.extrapolationMode}");
        sb.AppendLine($"Graph valid={director.playableGraph.IsValid()} IsDone={(director.playableGraph.IsValid() && director.playableGraph.IsDone())}");
        sb.AppendLine($"enabled={director.enabled} componentActive={enabled}");
        sb.AppendLine($"game Time.timeScale={Time.timeScale}");

        var asset = director.playableAsset;
        sb.AppendLine($"PlayableAsset: {(asset ? asset.name : "<null>")}");
        if (asset is TimelineAsset tla)
        {
            sb.AppendLine($"TimelineAsset.tracks={tla.outputTrackCount}");
            int missing = 0, muted = 0;

            foreach (var track in tla.GetOutputTracks())
            {
                if (track.muted) muted++;

                // —— 不引用 CinemachineTrack：用类型名字符串判断 ——
                var typeName = track.GetType().FullName ?? track.GetType().Name;
                bool looksLikeCinemachine = typeName.Contains("Cinemachine"); // 兼容无包环境

                // 这些常见 Track 通常需要绑定；Cinemachine 也通常需要绑定（Brain/VCam等）
                bool needBinding =
                    track is AnimationTrack ||
                    track is AudioTrack ||
                    track is ControlTrack ||
                    track is ActivationTrack ||
                    looksLikeCinemachine;

                var binding = director.GetGenericBinding(track);
                if (needBinding && binding == null) missing++;

                sb.AppendLine($" - [{track.GetType().Name}] {track.name}  muted={track.muted}  binding={(binding ? binding.name : "<null>")}  needBinding={needBinding}");
            }

            if (muted > 0) sb.AppendLine($"[Warn] 有 {muted} 条 Track 处于 muted 状态。");
            if (missing > 0) sb.AppendLine($"[Warn] 有 {missing} 条 Track 需要绑定但绑定为 null。");
        }
        else
        {
            if (!asset) sb.AppendLine("[Error] Director.playableAsset 为 null（未指定 TimelineAsset）");
        }

        // 其它常见原因
        if (!director.gameObject.activeInHierarchy) sb.AppendLine("[Error] 挂着 Director 的 GameObject 是 inactive。");
        if (!director.enabled) sb.AppendLine("[Error] Director 组件被禁用。");
        if (Time.timeScale == 0f) sb.AppendLine("[Warn] Time.timeScale = 0（建议设回 1 再试）");

        Debug.Log(sb.ToString(), director);
    }

    // 右上角 “⋮” → 一键修复并可选播放
    [ContextMenu("FixIt Now (Rebuild+GameTime+Play)")]
    public void FixItNow() => FixItNow(true);

    public void FixItNow(bool tryPlay)
    {
        if (!director) { Debug.LogError("[TimelineDoctor] Director=null"); return; }

        // 统一设置为 GameTime，停止环绕
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        if (director.playableGraph.IsValid())
            director.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        director.extrapolationMode = DirectorWrapMode.None;

        // 从 0s 重建评估
        director.RebuildGraph();
        director.time = 0;
        director.Evaluate();

        Debug.Log("[TimelineDoctor] 已 RebuildGraph + Evaluate @0s", director);

        if (tryPlay)
        {
            StopAllCoroutines();
            StartCoroutine(CoTryPlayMultiFrame());
        }
    }

    // 右上角 “⋮” → 强制播放（多帧重试）
    [ContextMenu("Force Play (Multi-Frame Retry)")]
    public void ForcePlay()
    {
        StopAllCoroutines();
        StartCoroutine(CoTryPlayMultiFrame());
    }

    IEnumerator CoTryPlayMultiFrame()
    {
        if (!director) yield break;

        for (int i = 0; i < 8; i++)
        {
            director.Play();
            yield return null; // 下一帧检查
            if (director.state == PlayState.Playing)
            {
                Debug.Log("[TimelineDoctor] Director 进入 Playing。", director);
                yield break;
            }
        }

        Debug.LogWarning("[TimelineDoctor] 连续多帧 Play() 仍未进入 Playing —— 请查看体检日志的 Missing Binding / Muted / Asset 是否为空。", director);
    }
}
