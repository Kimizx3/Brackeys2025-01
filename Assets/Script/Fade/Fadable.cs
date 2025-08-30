using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Fadable : MonoBehaviour
{
    [Header("Behavior")]
    //Hide完成后SetActive(false)，Show前SetActive(true)
    public bool toggleGameObjectActive = false;
    public bool controlRaycastsIfCanvasGroup = true;

    [Header("Debug")]
    public bool debugLogs = false;

    CanvasGroup _cg;
    List<Graphic> _graphics = new List<Graphic>();
    List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    Coroutine _fadeCoro;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = GetComponentInChildren<CanvasGroup>(true);
        GetComponentsInChildren(true, _graphics);
        _sprites.AddRange(GetComponentsInChildren<SpriteRenderer>(true));
    }

    public void Show(float duration) => FadeTo(1f, duration);
    public void Hide(float duration) => FadeTo(0f, duration);

    public void FadeTo(float targetAlpha, float duration)
    {
        //
        bool needActivate = toggleGameObjectActive && targetAlpha > 0f && !gameObject.activeSelf;
        if (needActivate)
        {
            gameObject.SetActive(true);
            if (duration > 0f) SetAlpha(0f);
        }

        if (_fadeCoro != null) StopCoroutine(_fadeCoro);
        _fadeCoro = StartCoroutine(CoFade(targetAlpha, duration));
    }

    IEnumerator CoFade(float target, float dur)
    {
        float start = GetCurrentAlpha();
        if (Mathf.Approximately(dur, 0f))
        {
            SetAlpha(target);
            AfterFade(target);
            yield break;
        }

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
        AfterFade(target);
    }

    float GetCurrentAlpha()
    {
        if (_cg) return _cg.alpha;
        if (_graphics.Count > 0) return _graphics[0].color.a;
        if (_sprites.Count > 0) return _sprites[0].color.a;
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

    void AfterFade(float a)
    {
        if (toggleGameObjectActive && a <= 0f && gameObject.activeSelf)
            gameObject.SetActive(false);
        if (debugLogs) Debug.Log($"[Fadable] '{name}' alpha={a:0.00}");
    }
}
