using System;
using System.Collections.Generic;
using UnityEngine;

// #if UNITY_EDITOR
// using UnityEditor;
// #endif
// using UnityEngine;


public class BGMController : MonoBehaviour
{
    public enum SegmentScope
    {
        Any,        // 主线/支线都生效
        Mainline,   // 仅主线段生效
        Branch      // 仅支线段生效（不在 mainlineSegments 里的段）
    }

    [Serializable]
    public class BgmRule
    {
        [Header("Rule Name (仅用于识别)")]
        public string name = "BGM Rule";

        [Header("Scope")]
        public SegmentScope scope = SegmentScope.Any;

        [Header("Target Segment")]
        public DialogueSegment segment;  // 直接拖你要作用的段（主线段 or 支线段）

        [Header("Step Range (段内范围, inclusive)")]
        [Tooltip("开始步（段内 index，从0开始）")]
        public int startStep = 0;

        [Tooltip("结束步（段内 index，从0开始）。-1 表示到段末")]
        public int endStep = -1;

        [Header("BGM")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;

        [Header("Priority")]
        [Tooltip("当多个规则命中时，priority 更大的生效；相同则后面的覆盖前面")]
        public int priority = 0;

        public bool Matches(DialogueSegment curSeg, int stepInSeg, bool isMainlineSeg)
        {
            if (segment == null || curSeg == null) return false;
            if (segment != curSeg) return false;

            // scope check
            if (scope == SegmentScope.Mainline && !isMainlineSeg) return false;
            if (scope == SegmentScope.Branch && isMainlineSeg) return false;

            int s = Mathf.Max(0, startStep);
            int e = endStep < 0 ? int.MaxValue : Mathf.Max(s, endStep);

            return stepInSeg >= s && stepInSeg <= e;
        }
    }

    [Header("Audio Source")]
    public AudioSource bgmSource;

    [Header("Rules (按段+范围配置)")]
    public List<BgmRule> rules = new List<BgmRule>();

    [Header("Debug")]
    public bool log = false;

    AudioClip _currentClip;

    void Reset()
    {
        bgmSource = GetComponent<AudioSource>();
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
    }

    /// <summary>
    /// 在每一步对话开始时调用：
    /// - 如果命中某条规则且 clip 与当前不同：切 BGM
    /// - 如果没命中任何规则：不做任何事（保持原 BGM 连续播放）
    /// </summary>
    public void Apply(DialogueSegment curSeg, int stepInSeg, bool isMainlineSeg)
    {
        if (bgmSource == null) return;
        if (curSeg == null) return;

        BgmRule best = null;

        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r == null) continue;
            if (!r.Matches(curSeg, stepInSeg, isMainlineSeg)) continue;

            if (best == null) best = r;
            else
            {
                if (r.priority > best.priority) best = r;
                else if (r.priority == best.priority)
                {
                    // 同 priority：后面的覆盖前面（更符合 Inspector 从上到下调试）
                    best = r;
                }
            }
        }

        if (best == null)
        {
            if (log)
                Debug.Log($"[BGM] No rule matched. Keep playing: {(_currentClip ? _currentClip.name : "None")}");
            return; // ✅关键：不匹配时不打断现有 BGM
        }

        if (best.clip == null)
        {
            if (log) Debug.Log($"[BGM] Rule matched but clip is null: {best.name}. Keep current.");
            return;
        }

        // clip 相同：不重播，保持连续
        if (_currentClip == best.clip)
        {
            // 确保音量/loop更新（可选）
            bgmSource.volume = best.volume;
            bgmSource.loop = best.loop;

            if (!bgmSource.isPlaying) bgmSource.Play();

            if (log) Debug.Log($"[BGM] Matched {best.name}, same clip keep: {_currentClip.name}");
            return;
        }

        // 切换 BGM
        _currentClip = best.clip;
        bgmSource.Stop();
        bgmSource.clip = best.clip;
        bgmSource.volume = best.volume;
        bgmSource.loop = best.loop;
        bgmSource.Play();

        if (log) Debug.Log($"[BGM] Switch -> {_currentClip.name} (rule: {best.name}) seg={curSeg.name} step={stepInSeg}");
    }

    /// <summary>手动强制播放某个 BGM（可选）</summary>
    public void ForcePlay(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (bgmSource == null || clip == null) return;
        _currentClip = clip;
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.loop = loop;
        bgmSource.Play();
    }
    
    // #if UNITY_EDITOR
    //     [ContextMenu("DEBUG/Force Play First Rule Clip")]
    //     void DebugForcePlayFirstRuleClip()
    //     {
    //         if (rules == null || rules.Count == 0 || rules[0] == null || rules[0].clip == null)
    //         {
    //             Debug.LogError("[BGM] No rules or first rule has no clip.");
    //             return;
    //         }
    //         ForcePlay(rules[0].clip, rules[0].volume, rules[0].loop);
    //         Debug.Log("[BGM] ForcePlay executed.");
    //     }
    // #endif
    
}
