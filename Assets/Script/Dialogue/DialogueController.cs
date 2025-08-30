using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour
{
    [Header("UI Group (对话UI：底板/立绘/名字/文本)")]
    [Tooltip("对话UI的容器")]
    public CanvasGroup dialogueUIGroup;

    [Header("UI References (位于对话UI容器下)")]
    public Image portraitImage;
    public TMP_Text nameText;
    public TMP_Text dialogueText;

    [Header("Typing")]
    public float charsPerSecond = 30f;
    public bool allowSkipTyping = true;

    [Header("UI Fade")]
    // 显隐淡入淡出时长
    public float uiFadeDuration = 0.15f;

    [Header("Timeline Player (放TimelineManager")]
    public MonoBehaviour timelinePlayer;
    ITimelinePlayer _timeline;

    [Header("Sequence")]
    public DialogueStep[] steps;

    int _index = -1;
    bool _isTyping = false;
    Coroutine _typingCoro;
    Coroutine _fadeCoro;
    Action _pendingProceed;

    void Awake()
    {
        if (timelinePlayer != null) _timeline = timelinePlayer as ITimelinePlayer;
        if (_timeline == null)
            Debug.LogWarning("[DialogueController] timelinePlayer 未赋值或未实现 ITimelinePlayer。");
        
        if (dialogueUIGroup != null)
        {
            dialogueUIGroup.alpha = 1f;
            dialogueUIGroup.interactable = true;
            dialogueUIGroup.blocksRaycasts = true;
        }
    }

    void Start()
    {
        StartSequence();
    }

    public void StartSequence(int startIndex = 0)
    {
        _index = startIndex - 1;
        Proceed();
    }

    public void Proceed()
    {
        _pendingProceed = null;

        if (++_index >= steps.Length)
        {
            SetUIVisible(true);
            Debug.Log("Dialogue sequence finished.");
            return;
        }

        var step = steps[_index];

        // 如果选择在开始Timeline期间隐藏对话
        if (step.playTimelineOnStart && _timeline != null && !string.IsNullOrEmpty(step.startTimelineKey))
        {
            if (step.hideUIWhileStartTimeline) SetUIVisible(false);
            _timeline.Play(step.startTimelineKey, () => ShowStep(step));
        }
        else
        {
            ShowStep(step);
        }
    }

    void ShowStep(DialogueStep step)
    {
        if (!step.hideUIWhileGate)
            SetUIVisible(true);

        if (portraitImage) portraitImage.sprite = step.portrait;
        if (nameText) nameText.text = step.speaker;

        if (_typingCoro != null) StopCoroutine(_typingCoro);
        _typingCoro = StartCoroutine(TypeText(step.content));
        
        if (step.hideUIWhileGate)
            SetUIVisible(false);
        
        SetupGateFor(step);
    }

    IEnumerator TypeText(string content)
    {
        _isTyping = true;
        dialogueText.text = "";

        if (charsPerSecond <= 0f)
        {
            dialogueText.text = content;
            _isTyping = false;
            yield break;
        }

        float tPerChar = 1f / charsPerSecond;
        for (int i = 0; i < content.Length; i++)
        {
            dialogueText.text = content.Substring(0, i + 1);
            yield return new WaitForSeconds(tPerChar);
        }
        _isTyping = false;
    }

    void SetupGateFor(DialogueStep step)
    {
        ClearAllGateBindings();

        switch (step.gateType)
        {
            case DialogueGateType.ClickAnywhere:
                _pendingProceed = () => EndOfStepThenProceed(step);
                break;

            case DialogueGateType.ClickButton:
                if (step.button != null)
                {
                    step.button.gameObject.SetActive(true);
                    step.button.onClick.AddListener(() =>
                    {
                        if (_isTyping && allowSkipTyping) { FinishTypingNow(step.content); return; }
                        EndOfStepThenProceed(step);
                    });
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] Step {_index} 选择 ClickButton 但未指定 Button，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;

            case DialogueGateType.DragToZone:
                if (step.draggable != null && step.dropZone != null)
                {
                    var dragItem = step.draggable.GetComponent<SimpleDragItem>();
                    if (dragItem == null) dragItem = step.draggable.gameObject.AddComponent<SimpleDragItem>();
                    dragItem.Init(step.dropZone, () => EndOfStepThenProceed(step));
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] Step {_index} 选择 DragToZone 但未指定拖拽物/目标，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;
            
            case DialogueGateType.FocusMinigame:
                if (step.focusMinigame != null)
                {
                    step.focusMinigame.StartGame(() =>
                    {
                        EndOfStepThenProceed(step);
                    });
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] Step {_index} 是 FocusMinigame 但未指定 focusMinigame，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;
        }
    }

    void EndOfStepThenProceed(DialogueStep step)
    {
        // 如果选择在“结束Timeline期间隐藏对话”
        if (step.playTimelineOnEnd && _timeline != null && !string.IsNullOrEmpty(step.endTimelineKey))
        {
            if (step.hideUIWhileEndTimeline) SetUIVisible(false);
            _timeline.Play(step.endTimelineKey, Proceed);
        }
        else
        {
            Proceed();
        }
    }

    void ClearAllGateBindings()
    {
        if (_index >= 0 && _index < steps.Length)
        {
            var prev = steps[_index];
            if (prev.button) { prev.button.onClick.RemoveAllListeners(); prev.button.gameObject.SetActive(false); }
            if (prev.draggable)
            {
                var drag = prev.draggable.GetComponent<SimpleDragItem>();
                if (drag) drag.enabled = false;
            }
        }
    }

    void Update()
    {
        if (_pendingProceed != null && Input.GetMouseButtonDown(0))
        {
            if (_isTyping && allowSkipTyping)
                FinishTypingNow(steps[_index].content);
            else
                _pendingProceed?.Invoke();
        }
    }

    void FinishTypingNow(string fullText)
    {
        if (_typingCoro != null) StopCoroutine(_typingCoro);
        dialogueText.text = fullText;
        _isTyping = false;
    }

    // =========== 对话UI显隐 ============
    void SetUIVisible(bool visible, bool instant = false)
    {
        if (dialogueUIGroup == null)
            return;

        if (_fadeCoro != null) StopCoroutine(_fadeCoro);

        if (instant || uiFadeDuration <= 0f)
        {
            dialogueUIGroup.alpha = visible ? 1f : 0f;
            dialogueUIGroup.interactable = visible;
            dialogueUIGroup.blocksRaycasts = visible;
        }
        else
        {
            _fadeCoro = StartCoroutine(FadeCanvasGroup(dialogueUIGroup, visible ? 1f : 0f, uiFadeDuration));
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup g, float targetAlpha, float dur)
    {
        float from = g.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            g.alpha = Mathf.Lerp(from, targetAlpha, k);
            yield return null;
        }
        g.alpha = targetAlpha;
        bool visible = targetAlpha > 0.99f;
        g.interactable = visible;
        g.blocksRaycasts = visible;
    }
}

[Serializable]
public class DialogueStep
{
    [Header("显示")]
    public string speaker;
    public Sprite portrait;
    [TextArea(2, 5)] public string content;

    [Header("完成条件（Gate）")]
    public DialogueGateType gateType = DialogueGateType.ClickAnywhere;
    // ClickButton 时指定
    public Button button;
    // DragToZone 时指定
    public RectTransform draggable;
    // DragToZone 时指定
    public RectTransform dropZone;
    
    [Tooltip("拍照交互拖这里")]
    public FocusMinigame focusMinigame;

    [Header("衔接Timeline")]
    // 句首
    public bool playTimelineOnStart = false;
    public string startTimelineKey;
    // 句末
    public bool playTimelineOnEnd = false;
    public string endTimelineKey;

    [Header("对话隐藏选项")]
    [Tooltip("在Timeline播放期间隐藏对话UI")]
    public bool hideUIWhileStartTimeline = false;

    [Tooltip("在Gate交互进行期间隐藏对话UI")]
    public bool hideUIWhileGate = false;

    [Tooltip("在结束Timeline播放期间隐藏对话UI")]
    public bool hideUIWhileEndTimeline = false;
}

public enum DialogueGateType
{
    ClickAnywhere,
    ClickButton,
    DragToZone,
    FocusMinigame
}
