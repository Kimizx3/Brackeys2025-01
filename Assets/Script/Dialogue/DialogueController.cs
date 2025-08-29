using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour
{
    [Header("UI References")]
    public Image portraitImage;
    public TMP_Text nameText;
    public TMP_Text dialogueText;

    [Header("Typing")]
    public float charsPerSecond = 30f;
    public bool allowSkipTyping = true;

    [Header("Timeline Player (拖 TimelineManager 即可)")]
    public MonoBehaviour timelinePlayer;
    ITimelinePlayer _timeline;

    [Header("Sequence")]
    public DialogueStep[] steps;

    int _index = -1;
    bool _isTyping = false;
    Coroutine _typingCoro;
    Action _pendingProceed;

    void Awake()
    {
        if (timelinePlayer != null) _timeline = timelinePlayer as ITimelinePlayer;
        if (_timeline == null)
            Debug.LogWarning("[DialogueController] timelinePlayer 未赋值或未实现 ITimelinePlayer。");
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
            Debug.Log("Dialogue sequence finished.");
            return;
        }

        var step = steps[_index];

        // 句首点播（可选）
        if (step.playTimelineOnStart && _timeline != null && !string.IsNullOrEmpty(step.startTimelineKey))
        {
            _timeline.Play(step.startTimelineKey, () => ShowStep(step));
        }
        else
        {
            ShowStep(step);
        }
    }

    void ShowStep(DialogueStep step)
    {
        if (portraitImage) portraitImage.sprite = step.portrait;
        if (nameText) nameText.text = step.speaker;

        if (_typingCoro != null) StopCoroutine(_typingCoro);
        _typingCoro = StartCoroutine(TypeText(step.content));

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
        }
    }

    void EndOfStepThenProceed(DialogueStep step)
    {
        if (step.playTimelineOnEnd && _timeline != null && !string.IsNullOrEmpty(step.endTimelineKey))
            _timeline.Play(step.endTimelineKey, Proceed);
        else
            Proceed();
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
}

[Serializable]
public class DialogueStep
{
    [Header("显示")]
    public string speaker;
    public Sprite portrait;
    [TextArea(2, 5)] public string content;

    [Header("完成条件")]
    public DialogueGateType gateType = DialogueGateType.ClickAnywhere;
    public Button button;
    public RectTransform draggable;
    public RectTransform dropZone;

    [Header("Timeline衔接")]
    public bool playTimelineOnStart = false;
    public string startTimelineKey;
    public bool playTimelineOnEnd = false;
    public string endTimelineKey;
}

public enum DialogueGateType
{
    ClickAnywhere,
    ClickButton,
    DragToZone
}
