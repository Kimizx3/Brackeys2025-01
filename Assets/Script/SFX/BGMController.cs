using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGMController : MonoBehaviour
{
    [Header("BGM列表")]
    public List<BGMRange> ranges = new List<BGMRange>();

    [Header("播放器设置")]
    public AudioSource sourceA;
    public AudioSource sourceB;
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("行为")]
    [Tooltip("某步无匹配时保持上一首不断播。")]
    public bool holdLastIfNoMatch = true;

    [Tooltip("命中同一首歌但当前源未播放时，强制恢复播放。")]
    public bool forcePlayIfSameClipNotPlaying = true;

    [Tooltip("若应该在播但两路都静默，定期自动恢复播放。")]
    public bool watchdogRestartIfSilent = true;
    public float watchdogInterval = 0.5f;

    [Header("调试")]
    public bool debugLogs = false;

    // runtime
    AudioSource _active;
    AudioSource _inactive;
    AudioClip   _currentClip;
    float       _currentTargetVol = 0f;
    bool        _shouldBePlaying = false;
    Coroutine   _xfadeCoro;
    bool        _indexedBuilt = false;
    float       _lastWatchdogTime = -999f;

    void Awake() => EnsureSources();

    void EnsureSources()
    {
        if (!sourceA)
        {
            var go = new GameObject("BGM_A");
            go.transform.SetParent(transform, false);
            sourceA = go.AddComponent<AudioSource>();
            sourceA.playOnAwake = false;
        }
        if (!sourceB)
        {
            var go = new GameObject("BGM_B");
            go.transform.SetParent(transform, false);
            sourceB = go.AddComponent<AudioSource>();
            sourceB.playOnAwake = false;
        }

        //统一成2D & 抗打断
        SetupSrc(sourceA);
        SetupSrc(sourceB);

        sourceA.loop = sourceB.loop = true;
        sourceA.volume = sourceB.volume = 0f;
        _active = sourceA;
        _inactive = sourceB;
    }

    static void SetupSrc(AudioSource s)
    {
        if (!s) return;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.rolloffMode  = AudioRolloffMode.Linear;
        s.ignoreListenerPause = true; // 不受AudioListener.pause影响
        s.mute = false;
    }

    public void BuildIndexFromSegments(DialogueSegment[] segments)
    {
        _indexedBuilt = true;

        var prefix = new List<int>();
        int sum = 0;
        if (segments != null)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                prefix.Add(sum);
                sum += segments[i]?.steps?.Length ?? 0;
            }
        }

        for (int i = 0; i < ranges.Count; i++)
        {
            var r = ranges[i];
            if (r.useAbsolute)
            {
                r._absStart = Mathf.Max(0, r.startAbsoluteIndex);
                r._absEnd   = Mathf.Max(r._absStart, r.endAbsoluteIndex);
                continue;
            }

            int s0 = Mathf.Clamp(r.startSegmentIndex, 0, Mathf.Max(0, (segments?.Length ?? 1) - 1));
            int s1 = Mathf.Clamp(r.endSegmentIndex,   0, Mathf.Max(0, (segments?.Length ?? 1) - 1));

            int base0 = (s0 < prefix.Count) ? prefix[s0] : 0;
            int base1 = (s1 < prefix.Count) ? prefix[s1] : 0;

            int cnt0  = (segments != null && s0 < segments.Length && segments[s0]) ? (segments[s0].steps?.Length ?? 0) : 0;
            int cnt1  = (segments != null && s1 < segments.Length && segments[s1]) ? (segments[s1].steps?.Length ?? 0) : 0;

            int step0 = Mathf.Clamp(r.startStepInSegment, 0, Mathf.Max(0, cnt0 - 1));
            int step1 = Mathf.Clamp(r.endStepInSegment,   0, Mathf.Max(0, cnt1 - 1));

            r._absStart = base0 + step0;
            r._absEnd   = base1 + step1;
            if (r._absEnd < r._absStart) r._absEnd = r._absStart;
        }

        if (debugLogs)
        {
            Debug.Log($"[BGM] BuildIndexFromSegments: totalRanges={ranges.Count}");
            for (int i = 0; i < ranges.Count; i++)
            {
                var r = ranges[i];
                string clipName = r.clip ? r.clip.name : "(None)";
                Debug.Log($"[BGM] Range[{i}] '{clipName}' => [{r._absStart}..{r._absEnd}] (useAbs={r.useAbsolute})");
            }
        }
    }

    void Update()
    {
        if (!watchdogRestartIfSilent || !_shouldBePlaying) return;
        if (Time.unscaledTime - _lastWatchdogTime < Mathf.Max(0.1f, watchdogInterval)) return;
        _lastWatchdogTime = Time.unscaledTime;

        // 若目标是应该在播但两路都没声，强制恢复
        if (!IsAnyPlaying())
        {
            if (debugLogs) Debug.Log("[BGM] Watchdog: silent -> force resume.");
            if (_currentClip != null)
            {
                // 直接在 _active上开播，不换源
                if (_active)
                {
                    _active.clip = _currentClip;
                    _active.loop = true;
                    _active.Play();
                    _active.volume = _currentTargetVol;
                }
            }
        }
    }

    bool IsAnyPlaying()
    {
        return (sourceA && sourceA.isPlaying) || (sourceB && sourceB.isPlaying);
    }

    // DialogueController 每步调用
    public void ApplyForAbsoluteIndex(int absIndex)
    {
        if (!_indexedBuilt && debugLogs)
            Debug.LogWarning("[BGM] index not built; call BuildIndexFromSegments first.");

        SyncCurrentFromSources(); //回读真实源

        int matchedIdx = -1;
        BGMRange match = null;
        for (int i = 0; i < ranges.Count; i++)
        {
            var r = ranges[i];
            if (absIndex >= r._absStart && absIndex <= r._absEnd)
            {
                match = r; matchedIdx = i;
            }
        }

        if (match == null || match.clip == null)
        {
            _shouldBePlaying = false;
            if (holdLastIfNoMatch)
            {
                if (debugLogs) Debug.Log($"[BGM] Step {absIndex}: no match -> HOLD '{_currentClip?.name ?? "None"}'");
                return;
            }
            if (debugLogs) Debug.Log($"[BGM] Step {absIndex}: no match -> fade out");
            CrossfadeTo(null, 0f, 0f, 0.5f);
            _currentClip = null;
            return;
        }

        _shouldBePlaying = true;

        float targetVol = match.volume * masterVolume;
        _currentTargetVol = targetVol;

        bool aPlayingThis = sourceA && sourceA.isPlaying && sourceA.clip == match.clip;
        bool bPlayingThis = sourceB && sourceB.isPlaying && sourceB.clip == match.clip;
        bool anyTargetPlaying = aPlayingThis || bPlayingThis;

        if (anyTargetPlaying)
        {
            _active   = aPlayingThis ? sourceA : sourceB;
            _inactive = aPlayingThis ? sourceB : sourceA;

            if (debugLogs) Debug.Log($"[BGM] Step {absIndex}: same[{matchedIdx}] '{match.clip.name}', vol->{targetVol:0.00}");

            // 同曲确保在播 & 调音量
            if (!_active.isPlaying) _active.Play();
            _active.loop = match.loop;
            StartCoroutine(LerpVolume(_active, _active.volume, targetVol, Mathf.Max(0.05f, match.fadeIn)));
            _currentClip = match.clip;
        }
        else
        {
            // 当前没在播目标曲,切换或恢复
            if (forcePlayIfSameClipNotPlaying && _active && _active.clip == match.clip && !_active.isPlaying)
            {
                _active.loop = match.loop;
                _active.Play();
                StartCoroutine(LerpVolume(_active, _active.volume, targetVol, Mathf.Max(0.05f, match.fadeIn)));
                _currentClip = match.clip;
                if (debugLogs) Debug.Log($"[BGM] Step {absIndex}: resume same clip '{match.clip.name}'");
            }
            else
            {
                if (debugLogs) Debug.Log($"[BGM] Step {absIndex}: switch[{matchedIdx}] '{_currentClip?.name ?? "None"}' -> '{match.clip.name}'");
                CrossfadeTo(match.clip, targetVol, Mathf.Max(0.05f, match.fadeIn), Mathf.Max(0.05f, match.fadeOut), keepActive:false, loop:match.loop);
            }
        }
    }

    void SyncCurrentFromSources()
    {
        AudioSource playing = null;
        if (sourceA && sourceA.isPlaying) playing = sourceA;
        else if (sourceB && sourceB.isPlaying) playing = sourceB;

        if (playing != null)
        {
            _currentClip = playing.clip;
            _active   = playing;
            _inactive = (playing == sourceA) ? sourceB : sourceA;
        }
        else
        {
            if (_active == null) _active = sourceA ?? sourceB;
            if (_inactive == null) _inactive = (_active == sourceA ? sourceB : sourceA);
        }
    }

    void CrossfadeTo(AudioClip nextClip, float nextVol, float fadeIn, float fadeOut, bool keepActive=false, bool loop=true)
    {
        if (_xfadeCoro != null) StopCoroutine(_xfadeCoro);
        _xfadeCoro = StartCoroutine(CoCrossfade(nextClip, nextVol, fadeIn, fadeOut, keepActive, loop));
    }

    IEnumerator CoCrossfade(AudioClip nextClip, float nextVol, float fadeIn, float fadeOut, bool keepActive, bool loop)
    {
        EnsureSources();

        if (keepActive && _active != null)
        {
            if (!_active.isPlaying) _active.Play();
            _active.loop = loop;
            yield return LerpVolume(_active, _active.volume, nextVol, Mathf.Max(0.05f, fadeIn));
            _currentClip = _active.clip;
            yield break;
        }

        var from = _active;
        var to   = _inactive;

        if (nextClip == null)
        {
            yield return LerpVolume(from, from ? from.volume : 0f, 0f, Mathf.Max(0.05f, fadeOut));
            if (from) from.Stop();
            _currentClip = null;
        }
        else
        {
            to.clip = nextClip;
            to.volume = 0f;
            to.loop = loop;
            to.Play();

            float fi = Mathf.Max(0.05f, fadeIn);
            float fo = Mathf.Max(0.05f, fadeOut);
            float t = 0f, dur = Mathf.Max(fi, fo);

            while (t < dur)
            {
                t += Time.deltaTime;
                float kin  = (fi <= 0f) ? 1f : Mathf.Clamp01(t / fi);
                float kout = (fo <= 0f) ? 1f : Mathf.Clamp01(t / fo);
                to.volume   = Mathf.Lerp(0f, nextVol, kin);
                if (from) from.volume = Mathf.Lerp(from.volume, 0f, kout);
                yield return null;
            }

            if (from) { from.Stop(); from.volume = 0f; }
            to.volume = nextVol;

            _active = to;
            _inactive = from;
            _currentClip = nextClip;
        }
    }

    IEnumerator LerpVolume(AudioSource src, float from, float to, float dur)
    {
        if (!src) yield break;
        dur = Mathf.Max(0.05f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; //不受TimeScale影响
            float k = Mathf.Clamp01(t / dur);
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }
        src.volume = to;
        if (to <= 0f) src.Stop();
    }
}

[System.Serializable]
public class BGMRange
{
    [Header("曲目")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;

    [Header("淡入淡出")]
    public float fadeIn = 0.5f;
    public float fadeOut = 0.5f;

    [Header("范围表示法（二选一）")]
    public bool useAbsolute = false;

    [Tooltip("当 useAbsolute=true：使用“拼接后”的绝对步下标（含头含尾，0 开始）")]
    public int startAbsoluteIndex = 0, endAbsoluteIndex = 0;

    [Tooltip("当 useAbsolute=false：使用 段/步 范围（含头含尾，步从 0 开始）")]
    public int startSegmentIndex = 0, startStepInSegment = 0;
    public int endSegmentIndex   = 0, endStepInSegment   = 0;

    [HideInInspector] public int _absStart = 0, _absEnd = 0;
}
