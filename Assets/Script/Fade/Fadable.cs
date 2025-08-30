using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Fadable : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("Hide 完成后 SetActive(false)，Show 前 SetActive(true)")]
    public bool toggleGameObjectActive = false;

    [Tooltip("若根或子层有 CanvasGroup，则在淡出时联动 interactable/blocksRaycasts")]
    public bool controlRaycastsIfCanvasGroup = true;

    [Header("Activation Root (可选)")]
    [Tooltip("要被激活/隐藏的根对象。为空=本物体。若一开始关的是外层容器，可指定它。")]
    public GameObject activationRoot;

    [Tooltip("当目标在层级中不可见时，是否沿父链逐级 SetActive(true) 直到可见。")]
    public bool autoActivateAncestorsIfNeeded = true;

    [Tooltip("Hide 完成后，是否把自动点亮过的父物体再关回去（谨慎使用）。")]
    public bool revertAutoActivatedOnHide = false;

    [Header("Debug")]
    public bool debugLogs = false;

    // ------- runtime -------
    CanvasGroup _cg;
    readonly List<Graphic> _graphics = new List<Graphic>();
    readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    Coroutine _fadeCoro;
    bool _inited;
    readonly List<GameObject> _autoActivatedChain = new List<GameObject>();

    #region Public API
    public void Show(float duration) => FadeTo(1f, duration);
    public void Hide(float duration) => FadeTo(0f, duration);

    public void FadeTo(float targetAlpha, float duration)
    {
        // 目标根：默认=本物体
        GameObject targetRoot = activationRoot ? activationRoot : gameObject;

        // ★ 在启动前，确保层级可见 + 组件启用
        if (toggleGameObjectActive && targetAlpha > 0f)
        {
            EnsureHierarchyActive(targetRoot);
            // 确保脚本启用（有些同学会把组件勾掉）
            if (!enabled) enabled = true;
            // 懂事儿一点：要淡入就先把 alpha 置 0，避免激活瞬间闪现
            if (duration > 0f) { EnsureInit(); SetAlpha(0f); }
        }

        if (duration <= 0f)
        {
            EnsureInit();
            SetAlpha(targetAlpha);
            AfterFade(targetRoot, targetAlpha);
            return;
        }

        if (_fadeCoro != null) StopCoroutine(_fadeCoro);
        _fadeCoro = StartCoroutine(CoFade(targetRoot, targetAlpha, duration));
    }
    #endregion

    #region Internals
    void EnsureInit()
    {
        if (_inited) return;

        // 注意：即便对象一开始是 Inactive，这里也能收集到（true 会遍历不激活对象）
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);

        _graphics.Clear();
        GetComponentsInChildren(true, _graphics);

        _sprites.Clear();
        _sprites.AddRange(GetComponentsInChildren<SpriteRenderer>(true));

        _inited = true;
    }

    IEnumerator CoFade(GameObject targetRoot, float target, float dur)
    {
        EnsureInit();

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

    void EnsureHierarchyActive(GameObject targetRoot)
    {
        _autoActivatedChain.Clear();

        // 已经在层级里可见，仅需自己 activeSelf=true 即可
        if (targetRoot.activeInHierarchy)
        {
            if (!targetRoot.activeSelf)
            {
                targetRoot.SetActive(true);
                _autoActivatedChain.Add(targetRoot);
                if (debugLogs) Debug.Log($"[Fadable] '{name}' SetActive(true) on '{targetRoot.name}'");
            }
            return;
        }

        if (!autoActivateAncestorsIfNeeded) return;

        // 逐级点亮祖先链（从顶往下）
        Stack<Transform> stack = new Stack<Transform>();
        Transform cur = targetRoot.transform;
        while (cur != null) { stack.Push(cur); cur = cur.parent; }

        while (stack.Count > 0)
        {
            var tr = stack.Pop();
            if (!tr.gameObject.activeSelf)
            {
                tr.gameObject.SetActive(true);
                _autoActivatedChain.Add(tr.gameObject);
                if (debugLogs) Debug.Log($"[Fadable] '{name}' Auto-activate '{tr.gameObject.name}'");
            }
        }
    }

    float GetCurrentAlpha()
    {
        EnsureInit();
        if (_cg) return _cg.alpha;
        if (_graphics.Count > 0) { var g = _graphics[0]; return g ? g.color.a : 1f; }
        if (_sprites.Count > 0) { var s = _sprites[0]; return s ? s.color.a : 1f; }
        return 1f;
    }

    void SetAlpha(float a)
    {
        EnsureInit();

        if (_cg)
        {
            _cg.alpha = a;
            if (controlRaycastsIfCanvasGroup)
            {
                bool vis = a > 0.001f;
                _cg.interactable = vis;
                _cg.blocksRaycasts = vis;
            }
        }
        else
        {
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
    }

    void AfterFade(GameObject targetRoot, float a)
    {
        if (toggleGameObjectActive && a <= 0f)
        {
            // 可选：回退自动点亮的父链（谨慎）
            if (revertAutoActivatedOnHide && _autoActivatedChain.Count > 0)
            {
                for (int i = _autoActivatedChain.Count - 1; i >= 0; --i)
                {
                    var go = _autoActivatedChain[i];
                    if (go) go.SetActive(false);
                }
                _autoActivatedChain.Clear();
            }

            if (targetRoot && targetRoot.activeSelf)
            {
                targetRoot.SetActive(false);
                if (debugLogs) Debug.Log($"[Fadable] '{name}' SetActive(false) on '{targetRoot.name}'");
            }
        }

        if (debugLogs) Debug.Log($"[Fadable] '{name}' alpha={a:0.00} (root='{targetRoot?.name}')");
    }
    #endregion

#if UNITY_EDITOR
    // 右键菜单快速测试（运行时）
    [ContextMenu("Test/Show 0.2s")]
    void _TestShow() => Show(0.2f);

    [ContextMenu("Test/Hide 0.2s")]
    void _TestHide() => Hide(0.2f);

    [ContextMenu("Test/Instant Show")]
    void _TestShowInstant() => Show(0f);

    [ContextMenu("Test/Instant Hide")]
    void _TestHideInstant() => Hide(0f);
#endif
}
