using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class DialogueController : MonoBehaviour
{
    [Header("UI Group")]
    public CanvasGroup dialogueUIGroup;

    [Header("UI References")]
    public Image portraitImage;
    public Text nameText;
    public TMP_Text dialogueText;

    [Header("Typing")]
    public float charsPerSecond = 30f;
    public bool allowSkipTyping = true;

    [Header("UI Fade")]
    public float uiFadeDuration = 0.15f;

    [Header("拖TimelineManager")]
    public MonoBehaviour timelinePlayer;
    ITimelinePlayer _timeline;
    
    [Header("Audio: BGM")]
    public BGMController bgmController;

    [Header("对话配置")]
    [Tooltip("Way1：直接在控制器里配置所有步骤。若 Segments 非空，将被忽略。")]
    public DialogueStep[] steps;

    [Tooltip("Way2：将一段段的 DialogueSegment 按顺序拖进来（推荐）。运行时会自动拼接所有段的 steps。")]
    public DialogueSegment[] segments;

    // segments非空,运行时拼接后的步骤表
    DialogueStep[] _activeSteps;
    bool UseSegments => segments != null && segments.Length > 0;
    int StepCount => UseSegments ? (_activeSteps?.Length ?? 0) : (steps?.Length ?? 0);
    DialogueStep GetStep(int i) => UseSegments ? _activeSteps[i] : steps[i];

    int _index = -1;
    bool _isTyping = false;
    Coroutine _typingCoro, _fadeCoro;
    Action _pendingProceed;
    FocusMinigame _activeFocus;

    void Awake()
    {
        if (timelinePlayer != null) _timeline = timelinePlayer as ITimelinePlayer;
        if (_timeline == null) Debug.LogWarning("[DialogueController] timelinePlayer 未赋值或未实现 ITimelinePlayer。");

        if (dialogueUIGroup != null)
        {
            dialogueUIGroup.alpha = 1f;
            dialogueUIGroup.interactable = true;
            dialogueUIGroup.blocksRaycasts = true;
        }

        BuildActiveSteps();
        
        if (bgmController != null) bgmController.BuildIndexFromSegments(segments);
    }

    void BuildActiveSteps()
    {
        if (!UseSegments)
        {
            _activeSteps = null;
            return;
        }

        var list = new List<DialogueStep>(256);
        foreach (var seg in segments)
        {
            if (!seg || seg.steps == null) continue;
            foreach (var s in seg.steps)
                if (s != null) list.Add(s);
        }
        _activeSteps = list.ToArray();
    }

    void Start() => StartSequence();
    
    public void StartSequence(int startIndex = 0)
    {
        _index = startIndex - 1;
        Proceed();
    }

    //从指定段和段内步骤开始（仅当Segments非空时生效）
    public void StartSequence(int segmentIndex, int stepInSegment)
    {
        if (!UseSegments) { StartSequence(stepInSegment); return; }
        int abs = 0;
        for (int s = 0; s < segments.Length; s++)
        {
            int count = segments[s]?.steps?.Length ?? 0;
            if (s == segmentIndex)
            {
                abs += Mathf.Clamp(stepInSegment, 0, Mathf.Max(0, count - 1));
                StartSequence(abs);
                return;
            }
            abs += count;
        }
        StartSequence(0);
    }

    public void Proceed()
    {
        _pendingProceed = null;

        if (++_index >= StepCount)
        {
            SetUIVisible(true);
            return;
        }
        
        if (bgmController != null) bgmController.ApplyForAbsoluteIndex(_index);

        var step = GetStep(_index);

        // 触发对话开始
        Fire(step.onStepStart);

        // 句首Timeline
        if (step.playTimelineOnStart && _timeline != null && !string.IsNullOrEmpty(step.startTimelineKey))
        {
            if (step.hideDialogueUIThisStep || step.hideUIWhileStartTimeline)
                SetUIVisible(false, instant: true);

            Fire(step.onStartTimelineStart);
            _timeline.Play(step.startTimelineKey, () =>
            {
                Fire(step.onStartTimelineEnd);
                ShowStep(step);
            });
        }
        else
        {
            ShowStep(step);
        }
    }

    void ShowStep(DialogueStep step)
    {
        bool hideThisStep = ShouldHideDialogueUI(step);
        SetUIVisible(!hideThisStep, instant: true);
        
        if (portraitImage != null) portraitImage.sprite = step.portrait;
        if (nameText != null) nameText.text = step.speaker ?? "";

        if (dialogueText != null)
        {
            if (_typingCoro != null) StopCoroutine(_typingCoro);
            _typingCoro = StartCoroutine(TypeText(step.content ?? ""));
        }
        else
        {
            Debug.LogWarning("[Dialogue] dialogueText 未赋值，无法显示台词。");
        }
        
        SetupGateFor(step);
    }

    bool ShouldHideDialogueUI(DialogueStep step)
    {
        // 强制隐藏优先；否则按“Gate 期间隐藏”的旧开关
        return step.hideDialogueUIThisStep || step.hideUIWhileGate;
    }

    IEnumerator TypeText(string content)
    {
        _isTyping = true;
        if (dialogueText) dialogueText.text = "";

        if (charsPerSecond <= 0f)
        {
            if (dialogueText) dialogueText.text = content;
            _isTyping = false;
            yield break;
        }

        float tPerChar = 1f / charsPerSecond;
        for (int i = 0; i < content.Length; i++)
        {
            if (dialogueText) dialogueText.text = content.Substring(0, i + 1);
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
                    _activeFocus = step.focusMinigame;
                    _activeFocus.StopGame();
                    _activeFocus.StartGame(() => EndOfStepThenProceed(step));
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] Step {_index} 是 FocusMinigame 但未指定组件，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;

            case DialogueGateType.TimelineComplete:
                if (_timeline != null && !string.IsNullOrEmpty(step.gateTimelineKey))
                {
                    _timeline.Play(step.gateTimelineKey, () => EndOfStepThenProceed(step));
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] Step {_index} 设为 TimelineComplete 但未配置 timelinePlayer 或 gateTimelineKey，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;
        }
    }

    void EndOfStepThenProceed(DialogueStep step)
    {
        Fire(step.onGateSuccess);

        // 句末 Timeline（可选）
        if (step.playTimelineOnEnd && _timeline != null && !string.IsNullOrEmpty(step.endTimelineKey))
        {
            if (step.hideDialogueUIThisStep || step.hideUIWhileEndTimeline)
                SetUIVisible(false, instant: true);

            Fire(step.onEndTimelineStart);
            _timeline.Play(step.endTimelineKey, () =>
            {
                Fire(step.onEndTimelineEnd);
                Fire(step.onStepEnd);
                Proceed();
            });
        }
        else
        {
            Fire(step.onStepEnd);
            Proceed();
        }
    }

    void ClearAllGateBindings()
    {
        if (_index >= 0 && _index < StepCount)
        {
            var prev = GetStep(_index);
            if (prev.button) { prev.button.onClick.RemoveAllListeners(); prev.button.gameObject.SetActive(false); }
            if (prev.draggable)
            {
                var drag = prev.draggable.GetComponent<SimpleDragItem>();
                if (drag) drag.enabled = false;
            }
            if (_activeFocus) { _activeFocus.StopGame(); _activeFocus = null; }
        }
    }

    void Update()
    {
        if (_pendingProceed != null && Input.GetMouseButtonDown(0))
        {
            if (_isTyping && allowSkipTyping)
                FinishTypingNow(GetStep(_index).content);
            else
                _pendingProceed?.Invoke();
        }
    }

    void FinishTypingNow(string fullText)
    {
        if (_typingCoro != null) StopCoroutine(_typingCoro);
        if (dialogueText) dialogueText.text = fullText ?? "";
        _isTyping = false;
    }

    void SetUIVisible(bool visible, bool instant = false)
    {
        if (dialogueUIGroup == null) return;
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

    // 执行动作数组
    void Fire(FadeAction[] actions)
    {
        if (actions == null) return;
        for (int i = 0; i < actions.Length; i++)
            if (actions[i] != null) actions[i].Execute();
    }
}

[Serializable]
public class DialogueStep
{
    [Header("显示")]
    public string speaker;
    public Sprite portrait;
    [TextArea(2, 5)] public string content;

    [Header("Gate通过条件")]
    public DialogueGateType gateType = DialogueGateType.ClickAnywhere;
    public Button button;                  // ClickButton
    public RectTransform draggable;        // DragToZone
    public RectTransform dropZone;         // DragToZone
    public FocusMinigame focusMinigame;    // FocusMinigame

    //TimelineComplete
    [Tooltip("TimelineComplet要等待完成的Timeline名")]
    public string gateTimelineKey;

    [Header("Timeline衔接")]
    public bool playTimelineOnStart = false;
    public string startTimelineKey;
    public bool playTimelineOnEnd = false;
    public string endTimelineKey;

    [Header("对话UI强制隐藏")]
    [Tooltip("勾选后：整步对话UI都隐藏")]
    public bool hideDialogueUIThisStep = false;

    [Header("UI隐藏选项")]
    public bool hideUIWhileStartTimeline = false;
    public bool hideUIWhileGate = false;
    public bool hideUIWhileEndTimeline = false;

    [Header("显隐对象")]
    public FadeAction[] onStepStart;           // 进入本句
    public FadeAction[] onStartTimelineStart;  // 句首TL开始
    public FadeAction[] onStartTimelineEnd;    // 句首TL结束
    public FadeAction[] onGateSuccess;         // Gate通过
    public FadeAction[] onEndTimelineStart;    // 句末TL开始
    public FadeAction[] onEndTimelineEnd;      // 句末TL结束
    public FadeAction[] onStepEnd;             // 离开本句
}

public enum DialogueGateType
{
    ClickAnywhere,
    ClickButton,
    DragToZone,
    FocusMinigame,
    TimelineComplete
}
