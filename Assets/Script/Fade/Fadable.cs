using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Fadable : MonoBehaviour
{
    [Header("Behavior")]
    public bool toggleGameObjectActive = false;
    public bool controlRaycastsIfCanvasGroup = true;

    [Header("Graphics Control")]
    [Tooltip("若不存在 CanvasGroup，是否改动子节点的 Graphic/SpriteRenderer 的 alpha。拍照玩法建议关掉。")]
    public bool affectChildrenGraphics = true;

    [Header("Activation Root (可选)")]
    public GameObject activationRoot;
    public bool autoActivateAncestorsIfNeeded = true;
    public bool revertAutoActivatedOnHide = false;

    [Header("SFX（可选）")]
    [Tooltip("用于播放显隐音效的 AudioSource；留空则会自动挂在本物体上")]
    public AudioSource sfxSource;
    public AudioClip sfxOnShow;
    public AudioClip sfxOnHide;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Debug")]
    public bool debugLogs = false;

    CanvasGroup _cg;
    readonly List<Graphic> _graphics = new List<Graphic>();
    readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    Coroutine _fadeCoro;
    bool _inited;
    readonly List<GameObject> _autoActivatedChain = new List<GameObject>();

    void EnsureInit()
    {
        if (_inited) return;
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);
        _graphics.Clear(); GetComponentsInChildren(true, _graphics);
        _sprites.Clear();  _sprites.AddRange(GetComponentsInChildren<SpriteRenderer>(true));
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        _inited = true;
    }

    public void Show(float duration) => FadeTo(1f, duration);
    public void Hide(float duration) => FadeTo(0f, duration);

    public void FadeTo(float targetAlpha, float duration)
    {
        EnsureInit();

        GameObject targetRoot = activationRoot ? activationRoot : gameObject;
        bool wantShow = targetAlpha > 0f;

        // 显示：先激活层级，再播放 SFX
        if (toggleGameObjectActive && wantShow)
        {
            EnsureHierarchyActive(targetRoot);
            if (!enabled) enabled = true;
            if (duration > 0f) SetAlpha(0f);

            if (sfxOnShow) SafePlayOneShot(sfxOnShow);
        }
        else
        {
            // 在开始时播 SFX（对象仍是激活状态，能听到）
            if (!wantShow && sfxOnHide) SafePlayOneShot(sfxOnHide);
        }

        // 宿主不可用或 duration=0 -> 直接生效（不启协程）
        if (duration <= 0f || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            SetAlpha(targetAlpha);
            AfterFade(targetRoot, targetAlpha);
            if (debugLogs && (!isActiveAndEnabled || !gameObject.activeInHierarchy))
                Debug.Log($"[Fadable] Instant apply because host inactive/disabled. target={targetAlpha}");
            return;
        }

        if (_fadeCoro != null) StopCoroutine(_fadeCoro);
        _fadeCoro = StartCoroutine(CoFade(targetRoot, targetAlpha, duration));
    }

    IEnumerator CoFade(GameObject targetRoot, float target, float dur)
    {
        float start = GetCurrentAlpha();
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float a = Mathf.Lerp(start, target, k);
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(target);
        AfterFade(targetRoot, target);
    }

    float GetCurrentAlpha()
    {
        if (_cg) return _cg.alpha;
        if (affectChildrenGraphics)
        {
            if (_graphics.Count > 0 && _graphics[0]) return _graphics[0].color.a;
            if (_sprites.Count  > 0 && _sprites[0])  return _sprites[0].color.a;
        }
        return 1f;
    }

    void SetAlpha(float a)
    {
        if (_cg)
        {
            _cg.alpha = a;
            if (controlRaycastsIfCanvasGroup)
            {
                bool vis = a > 0.001f;
                _cg.interactable = vis;
                _cg.blocksRaycasts = vis;
            }
            return;
        }

        if (!affectChildrenGraphics) return;

        for (int i = 0; i < _graphics.Count; i++)
        {
            var g = _graphics[i]; if (!g) continue;
            var c = g.color; c.a = a; g.color = c;
        }
        for (int i = 0; i < _sprites.Count; i++)
        {
            var s = _sprites[i]; if (!s) continue;
            var c = s.color; c.a = a; s.color = c;
        }
    }

    void EnsureHierarchyActive(GameObject targetRoot)
    {
        _autoActivatedChain.Clear();

        if (targetRoot.activeInHierarchy)
        {
            if (!targetRoot.activeSelf)
            {
                targetRoot.SetActive(true);
                _autoActivatedChain.Add(targetRoot);
            }
            return;
        }

        if (!autoActivateAncestorsIfNeeded) return;

        Stack<Transform> stk = new Stack<Transform>();
        for (var tr = targetRoot.transform; tr != null; tr = tr.parent) stk.Push(tr);
        while (stk.Count > 0)
        {
            var tr = stk.Pop();
            if (!tr.gameObject.activeSelf)
            {
                tr.gameObject.SetActive(true);
                _autoActivatedChain.Add(tr.gameObject);
            }
        }
    }

    void AfterFade(GameObject targetRoot, float a)
    {
        if (toggleGameObjectActive && a <= 0f)
        {
            if (revertAutoActivatedOnHide && _autoActivatedChain.Count > 0)
            {
                for (int i = _autoActivatedChain.Count - 1; i >= 0; --i)
                {
                    var go = _autoActivatedChain[i];
                    if (go) go.SetActive(false);
                }
                _autoActivatedChain.Clear();
            }
            if (targetRoot && targetRoot.activeSelf) targetRoot.SetActive(false);
        }
        if (debugLogs) Debug.Log($"[Fadable] '{name}' alpha={a:0.00} (root='{targetRoot?.name}')");
    }

    void SafePlayOneShot(AudioClip clip)
    {
        if (!clip || !sfxSource) return;
        
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Show 0.2s")]  void _t1() => Show(0.2f);
    [ContextMenu("Test/Hide 0.2s")]  void _t2() => Hide(0.2f);
    [ContextMenu("Test/Instant Show")] void _t3() => Show(0f);
    [ContextMenu("Test/Instant Hide")] void _t4() => Hide(0f);
#endif
}