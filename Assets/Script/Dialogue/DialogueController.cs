using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class DialogueController : MonoBehaviour
{
    public enum PlayMode
    {
        Flatten,
        Flow
    }

    [Header("UI Style Roots")]
    [Tooltip("方案1：你现有对话框 UI 的根节点（或面板节点）")]
    public GameObject style1Root;

    [Tooltip("方案2：气泡UI控制器")]
    public BubbleDialogueUI bubbleUI;

    [Header("Play Mode")]
    public PlayMode playMode = PlayMode.Flatten;

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

    // [FormerlySerializedAs("bgmController")] [Header("Audio: BGM")]
    // public BGMControllerOld bgmControllerOld;
    
    [Header("Audio: BGM")]
    public BGMController bgmController;

    [Header("对话配置（旧模式 Flatten 使用）")]
    public DialogueStep[] steps;
    public DialogueSegment[] segments;

    [Header("Flow 主线段（仅 playMode=Flow 使用）")]
    [Tooltip("只放主线会按顺序播放的段。分支段不要放这里！")]
    public DialogueSegment[] mainlineSegments;

    [Header("Choice UI（选项）")]
    public GameObject choicePanel;
    public Transform choiceContainer;
    public Button choiceButtonPrefab;

    [Header("Score（积分）")]
    public string defaultScoreKey = "affection";
    public bool logScore = true;
    readonly Dictionary<string, int> _scores = new Dictionary<string, int>();

    // ===== Branch Arc 播放状态（Flow 模式）=====
    DialogueSegment[] _currentArcSegments = null;
    int _currentArcIndex = -1;
    
    // ====== Flatten
    DialogueStep[] _activeSteps;
    bool UseSegments => segments != null && segments.Length > 0;
    int StepCount => UseSegments ? (_activeSteps?.Length ?? 0) : (steps?.Length ?? 0);
    DialogueStep GetStep(int i) => UseSegments ? _activeSteps[i] : steps[i];

    int _index = -1;
    
    DialogueSegment _curSeg = null;
    int _curStepInSeg = -1;
    int _curMainlineIndex = -1;
    
    // int _choiceFeedbackBubblePos = 1;


    struct ReturnPoint
    {
        public DialogueSegment seg;
        public int stepInSeg;
    }
    readonly Stack<ReturnPoint> _returnStack = new Stack<ReturnPoint>();
    
    bool _isTyping = false;
    Coroutine _typingCoro, _fadeCoro;
    Action _pendingProceed;
    IFocusMinigame _activeFocus;
    
    bool _inChoiceFeedback = false;
    DialogueLine[] _feedbackLines;
    int _feedbackLineIndex;
    
    int _resumeAbsIndexAfterFeedback = -1;
    int _resumeStepInSegAfterFeedback = -1;
    
    DialogueStep _choiceStepWaitingToComplete = null;

    readonly List<Button> _spawnedChoiceButtons = new List<Button>();

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

        // if (bgmControllerOld != null) bgmControllerOld.BuildIndexFromSegments(segments);

        BuildActiveSteps();

        if (choicePanel) choicePanel.SetActive(false);
    }

    void Start()
    {
        if (playMode == PlayMode.Flow)
            StartFlowMainline(0, 0);
        else
            StartSequence(0);
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


    public void StartSequence(int startIndex = 0)
    {
        playMode = PlayMode.Flatten;
        _index = startIndex - 1;
        _inChoiceFeedback = false;
        _choiceStepWaitingToComplete = null;
        Proceed();
    }

    public void StartSequence(int segmentIndex, int stepInSegment)
    {
        playMode = PlayMode.Flatten;

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


    public void StartFlowMainline(int mainlineIndex, int stepInSeg)
    {
        playMode = PlayMode.Flow;

        if (mainlineSegments == null || mainlineSegments.Length == 0)
        {
            Debug.LogError("[Flow] mainlineSegments 为空，无法开始。");
            return;
        }

        mainlineIndex = Mathf.Clamp(mainlineIndex, 0, mainlineSegments.Length - 1);
        var seg = mainlineSegments[mainlineIndex];
        if (seg == null || seg.steps == null || seg.steps.Length == 0)
        {
            Debug.LogError("[Flow] 主线段为空或 steps 为空。");
            return;
        }

        _curSeg = seg;
        _curMainlineIndex = mainlineIndex;
        _curStepInSeg = stepInSeg - 1;

        _inChoiceFeedback = false;
        _choiceStepWaitingToComplete = null;
        HideChoiceUI();
        ClearAllGateBindings();
        Proceed();
    }

    void JumpToSegmentFlow(DialogueSegment seg, int stepInSeg, bool clearReturnStack = false)
    {
        playMode = PlayMode.Flow;

        if (clearReturnStack) _returnStack.Clear();

        if (seg == null || seg.steps == null || seg.steps.Length == 0)
        {
            Debug.LogError("[Flow] JumpToSegmentFlow: seg 或 steps 为空。");
            return;
        }

        _curSeg = seg;
        _curStepInSeg = stepInSeg - 1;
        _curMainlineIndex = FindMainlineIndex(seg);

        _inChoiceFeedback = false;
        _choiceStepWaitingToComplete = null;
        HideChoiceUI();
        ClearAllGateBindings();
        Proceed();
    }

    int FindMainlineIndex(DialogueSegment seg)
    {
        if (seg == null || mainlineSegments == null) return -1;
        for (int i = 0; i < mainlineSegments.Length; i++)
            if (mainlineSegments[i] == seg) return i;
        return -1;
    }


    public void Proceed()
    {
        _pendingProceed = null;

        // ✅反馈对白优先
        if (_inChoiceFeedback)
        {
            ProceedChoiceFeedback();
            return;
        }

        if (playMode == PlayMode.Flow)
            ProceedFlow();
        else
            ProceedFlatten();
    }


    void ProceedFlatten()
    {
        if (++_index >= StepCount)
        {
            SetUIVisible(true);
            return;
        }

        // if (bgmControllerOld != null) bgmControllerOld.ApplyForAbsoluteIndex(_index);

        var step = GetStep(_index);
        
        if (bgmController != null && TryGetFlattenPosition(_index, out var seg, out var stepInSeg))
        {
            // Flatten 下没有主/支之分，这里按 Any 处理；isMainlineSeg 可传 true 或者按是否在 mainlineSegments 判断
            bool isMainlineSeg = (mainlineSegments != null && Array.IndexOf(mainlineSegments, seg) >= 0);
            bgmController.Apply(seg, stepInSeg, isMainlineSeg);
        }


        Fire(step.onStepStart);

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


    void ProceedFlow()
    {
        if (_curSeg == null || _curSeg.steps == null)
        {
            Debug.LogError("[Flow] 当前段为空。");
            return;
        }

        _curStepInSeg++;

        if (_curStepInSeg >= _curSeg.steps.Length)
        {
            // if (_returnStack.Count > 0)
            // {
            //     var rp = _returnStack.Pop();
            //     JumpToSegmentFlow(rp.seg, rp.stepInSeg);
            //     return;
            // }
            
            if (_currentArcSegments != null)
            {
                int nextArc = _currentArcIndex + 1;
                if (nextArc < _currentArcSegments.Length && _currentArcSegments[nextArc] != null)
                {
                    _currentArcIndex = nextArc;
                    JumpToSegmentFlow(_currentArcSegments[_currentArcIndex], 0, clearReturnStack: false);
                    return;
                }
                else
                {
                    _currentArcSegments = null;
                    _currentArcIndex = -1;
                }
            }
            
            if (_returnStack.Count > 0)
            {
                var rp = _returnStack.Pop();
                JumpToSegmentFlow(rp.seg, rp.stepInSeg);
                return;
            }
            
            if (_curMainlineIndex >= 0)
            {
                int next = _curMainlineIndex + 1;
                if (mainlineSegments != null && next < mainlineSegments.Length)
                {
                    StartFlowMainline(next, 0);
                    return;
                }
            }

            SetUIVisible(true);
            return;
        }

        var step = _curSeg.steps[_curStepInSeg];
        
        if (bgmController != null)
        {
            bool isMainlineSeg = (_curMainlineIndex >= 0);
            bgmController.Apply(_curSeg, _curStepInSeg, isMainlineSeg);
        }


        Fire(step.onStepStart);

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

    // =========================
    // ShowStep
    void ShowStep(DialogueStep step)
    {
        HideChoiceUI();
        ClearAllGateBindings();
        ApplyDialogueUIMode(step);

        bool hideThisStep = step.hideDialogueUIThisStep || step.hideUIWhileGate;
        SetUIVisible(!hideThisStep, instant: true);

        switch (step.stepKind)
        {
            case DialogueStepKind.Normal:
                _choiceStepWaitingToComplete = null;
                ShowNormalLine(step);
                SetupGateFor(step);
                break;

            case DialogueStepKind.Choice:
                _choiceStepWaitingToComplete = null;
                ShowChoiceStep(step);
                break;

            case DialogueStepKind.BranchByScore:
                _choiceStepWaitingToComplete = null;
                ExecuteBranchByScore(step);
                break;

            default:
                _choiceStepWaitingToComplete = null;
                ShowNormalLine(step);
                SetupGateFor(step);
                break;
        }
    }
    
    void ApplyDialogueUIMode(DialogueStep step)
    {
        if (step.hideDialogueUIThisStep || step.uiMode == DialogueUIMode.Hidden)
        {
            if (style1Root) style1Root.SetActive(false);
            if (bubbleUI) bubbleUI.SetVisible(false);
            
            SetUIVisible(false, instant: true);
            return;
        }
        
        if (step.uiMode == DialogueUIMode.Bubble)
        {
            if (style1Root) style1Root.SetActive(false);
            
            SetUIVisible(true, instant: true);

            if (bubbleUI) bubbleUI.SetVisible(true);
            return;
        }

        if (bubbleUI) bubbleUI.SetVisible(false);
        if (style1Root) style1Root.SetActive(true);
        SetUIVisible(true, instant: true);
    }


    void ShowNormalLine(DialogueStep step)
    {
        if (step.uiMode == DialogueUIMode.Bubble)
        {
            if (bubbleUI != null)
            {
                bubbleUI.ShowAt(step.bubblePos, step.speaker, step.content ?? "");
            }
            else
            {
                Debug.LogError("[Dialogue] step.uiMode=Bubble but bubbleUI is null.");
            }
            
            return;
        }
        
        if (portraitImage != null) portraitImage.sprite = step.portrait;
        if (nameText != null) nameText.text = step.speaker ?? "";

        if (dialogueText != null)
        {
            if (_typingCoro != null) StopCoroutine(_typingCoro);
            _typingCoro = StartCoroutine(TypeText(step.content ?? ""));
        }
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

    void FinishTypingNow(string fullText)
    {
        if (_typingCoro != null) StopCoroutine(_typingCoro);
        if (dialogueText) dialogueText.text = fullText ?? "";
        _isTyping = false;
    }

    // =========================
    // Gate
    void SetupGateFor(DialogueStep step)
    {
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
                    Debug.LogWarning($"[Dialogue] ClickButton 未指定 Button，降级为任意点击。");
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
                    Debug.LogWarning($"[Dialogue] DragToZone 未指定 draggable/dropZone，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;

            case DialogueGateType.FocusMinigame:
            {
                IFocusMinigame game = null;

                if (step.focusMinigame != null)
                {
                    game = step.focusMinigame as IFocusMinigame;
                    if (game == null)
                        game = step.focusMinigame.GetComponent(typeof(IFocusMinigame)) as IFocusMinigame;
                }

                if (game != null)
                {
                    _activeFocus = game;
                    _activeFocus.StopGame();
                    _activeFocus.StartGame(() => EndOfStepThenProceed(step));
                }
                else
                {
                    Debug.LogWarning($"[Dialogue] FocusMinigame 未找到 IFocusMinigame，降级为任意点击。");
                    _pendingProceed = () => EndOfStepThenProceed(step);
                }
                break;
            }

            case DialogueGateType.TimelineComplete:
                Debug.LogWarning($"[Dialogue] TimelineComplete 未启用实现，降级为任意点击。");
                _pendingProceed = () => EndOfStepThenProceed(step);
                break;
        }
    }

    void EndOfStepThenProceed(DialogueStep step)
    {
        Fire(step.onGateSuccess);

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
        try
        {
            if (playMode == PlayMode.Flatten)
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
                    if (_activeFocus != null) { _activeFocus.StopGame(); _activeFocus = null; }
                }
            }
            else
            {
                if (_curSeg != null && _curSeg.steps != null && _curStepInSeg >= 0 && _curStepInSeg < _curSeg.steps.Length)
                {
                    var prev = _curSeg.steps[_curStepInSeg];
                    if (prev != null && prev.button) { prev.button.onClick.RemoveAllListeners(); prev.button.gameObject.SetActive(false); }
                    if (prev != null && prev.draggable)
                    {
                        var drag = prev.draggable.GetComponent<SimpleDragItem>();
                        if (drag) drag.enabled = false;
                    }
                    if (_activeFocus != null) { _activeFocus.StopGame(); _activeFocus = null; }
                }
            }
        }
        catch { }
    }

    void Update()
    {
        if (_pendingProceed != null && Input.GetMouseButtonDown(0))
        {
            if (choicePanel != null && choicePanel.activeSelf) return;

            if (_isTyping && allowSkipTyping)
            {
                string full = "";
                if (playMode == PlayMode.Flatten) full = GetStep(_index).content;
                else if (_curSeg != null && _curSeg.steps != null) full = _curSeg.steps[_curStepInSeg].content;
                FinishTypingNow(full);
            }
            else
            {
                _pendingProceed?.Invoke();
            }
        }
        
        if (_inChoiceFeedback && Input.GetMouseButtonDown(0))
        {
            if (_isTyping && allowSkipTyping)
            {
                var cur = _feedbackLines[Mathf.Clamp(_feedbackLineIndex, 0, _feedbackLines.Length - 1)];
                FinishTypingNow(cur.content);
            }
            else
            {
                ProceedChoiceFeedback();
            }
        }
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

    void Fire(FadeAction[] actions)
    {
        if (actions == null) return;
        for (int i = 0; i < actions.Length; i++)
            if (actions[i] != null) actions[i].Execute();
    }

    // =========================
    // Score
    public int GetScore(string key)
    {
        if (string.IsNullOrEmpty(key)) key = defaultScoreKey;
        return _scores.TryGetValue(key, out var v) ? v : 0;
    }

    public void AddScore(string key, int delta)
    {
        if (string.IsNullOrEmpty(key)) key = defaultScoreKey;
        int before = GetScore(key);
        _scores[key] = before + delta;
        if (logScore) Debug.Log($"[Score] {key}: {before} -> {_scores[key]} (delta {delta})");
    }

    // =========================
    // Choice
    void ShowChoiceStep(DialogueStep step)
    {
        if (portraitImage != null) portraitImage.sprite = step.portrait;
        if (nameText != null) nameText.text = step.speaker ?? "";

        if (dialogueText != null)
        {
            if (_typingCoro != null) StopCoroutine(_typingCoro);
            dialogueText.text = step.content ?? "";
            _isTyping = false;
        }

        if (choicePanel == null || choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError("[Choice] 缺少 choicePanel / choiceContainer / choiceButtonPrefab。");
            EndOfStepThenProceed(step);
            return;
        }

        choicePanel.SetActive(true);
        ClearSpawnedChoiceButtons();

        if (step.options == null || step.options.Length == 0)
        {
            Debug.LogWarning("[Choice] options 为空，直接完成本步。");
            HideChoiceUI();
            EndOfStepThenProceed(step);
            return;
        }

        for (int i = 0; i < step.options.Length; i++)
        {
            var opt = step.options[i];
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);
            _spawnedChoiceButtons.Add(btn);
            
            var rt = btn.transform as RectTransform;
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.anchoredPosition3D = Vector3.zero;
            }
            
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = opt.optionText;
            else
            {
                Text t = btn.GetComponentInChildren<Text>(true);
                if (t != null) t.text = opt.optionText;
                else Debug.LogError("[Choice] ButtonPrefab 没有 Text 或 TMP_Text 子节点，无法显示选项文本。");
            }

            btn.onClick.AddListener(() => OnPickChoice(step, opt));
        }
    }

    void OnPickChoice(DialogueStep currentChoiceStep, ChoiceOption opt)
    {
        HideChoiceUI();
        // _choiceFeedbackBubblePos = opt.feedbackBubblePos;
        
        if (opt.applyScore)
            AddScore(string.IsNullOrEmpty(opt.scoreKey) ? defaultScoreKey : opt.scoreKey, opt.scoreDelta);

        _choiceStepWaitingToComplete = currentChoiceStep;
        
        if (playMode == PlayMode.Flatten) _resumeAbsIndexAfterFeedback = _index + 1;
        else _resumeStepInSegAfterFeedback = _curStepInSeg + 1;
        
        if (opt.feedbackLines == null || opt.feedbackLines.Length == 0)
        {
            CompleteChoiceStepAndContinue();
            return;
        }

        _inChoiceFeedback = true;
        _feedbackLines = opt.feedbackLines;
        _feedbackLineIndex = 0;
        ShowFeedbackLine(_feedbackLines[0]);
    }

    void ShowFeedbackLine(DialogueLine line)
    {
        // if (portraitImage != null) portraitImage.sprite = line.portrait;
        // if (nameText != null) nameText.text = line.speaker ?? "";
        //
        // if (dialogueText != null)
        // {
        //     if (_typingCoro != null) StopCoroutine(_typingCoro);
        //     _typingCoro = StartCoroutine(TypeText(line.content ?? ""));
        // }
        //
        // _pendingProceed = null;
        
        if (style1Root) style1Root.SetActive(false);
        SetUIVisible(true, instant: true);

        if (bubbleUI == null)
        {
            Debug.LogError("[Choice Feedback] bubbleUI is null. Please assign BubbleDialogueUI in DialogueController.");
            return;
        }

        bubbleUI.SetVisible(true);
        
        int pos = Mathf.Clamp(line.bubblePos, 1, 3);
        bubbleUI.ShowAt(pos, line.speaker, line.content ?? "");
        
        _isTyping = false;
        _pendingProceed = null;
    }

    void ProceedChoiceFeedback()
    {
        if (!_inChoiceFeedback || _feedbackLines == null)
        {
            _inChoiceFeedback = false;
            return;
        }

        _feedbackLineIndex++;
        if (_feedbackLineIndex >= _feedbackLines.Length)
        {
            _inChoiceFeedback = false;
            _feedbackLines = null;

            CompleteChoiceStepAndContinue();
            return;
        }

        ShowFeedbackLine(_feedbackLines[_feedbackLineIndex]);
    }

    void CompleteChoiceStepAndContinue()
    {
        var step = _choiceStepWaitingToComplete;
        _choiceStepWaitingToComplete = null;

        if (step != null)
        {
            EndOfStepThenProceed(step);
            return;
        }
        
        ResumeAfterChoiceFallback();
    }

    void ResumeAfterChoiceFallback()
    {
        if (playMode == PlayMode.Flatten)
            _index = _resumeAbsIndexAfterFeedback - 1;
        else
            _curStepInSeg = _resumeStepInSegAfterFeedback - 1;

        Proceed();
    }

    void HideChoiceUI()
    {
        if (choicePanel) choicePanel.SetActive(false);
        ClearSpawnedChoiceButtons();
    }

    void ClearSpawnedChoiceButtons()
    {
        for (int i = 0; i < _spawnedChoiceButtons.Count; i++)
            if (_spawnedChoiceButtons[i] != null) Destroy(_spawnedChoiceButtons[i].gameObject);
        _spawnedChoiceButtons.Clear();
    }

    // =========================
    // BranchByScore
    void ExecuteBranchByScore(DialogueStep step)
    {
        if (playMode != PlayMode.Flow)
        {
            Debug.LogWarning("[BranchByScore] 当前是 Flatten 模式，建议切到 Flow。这里直接 Proceed().");
            Proceed();
            return;
        }

        string key = string.IsNullOrEmpty(step.branchScoreKey) ? defaultScoreKey : step.branchScoreKey;
        int v = GetScore(key);

        if (step.branchRanges == null || step.branchRanges.Length == 0)
        {
            Debug.LogWarning("[BranchByScore] 未配置 branchRanges，继续下一步。");
            Proceed();
            return;
        }

        for (int i = 0; i < step.branchRanges.Length; i++)
        {
            var r = step.branchRanges[i];
            if (r == null) continue;

            if (v >= r.minInclusive && v <= r.maxInclusive)
            {
                DialogueSegment returnSeg = r.returnToMainlineSegment != null ? r.returnToMainlineSegment : _curSeg;
                int returnStep = r.returnToMainlineSegment != null ? r.returnStepInSegment : (_curStepInSeg + 1);

                // _returnStack.Push(new ReturnPoint { seg = returnSeg, stepInSeg = Mathf.Max(0, returnStep) });
                //
                // if (r.branchSegment == null)
                // {
                //     Debug.LogError("[BranchByScore] branchSegment 为空，直接回返回点继续。");
                //     var rp = _returnStack.Pop();
                //     JumpToSegmentFlow(rp.seg, rp.stepInSeg);
                //     return;
                // }
                //
                // JumpToSegmentFlow(r.branchSegment, r.branchStartStepInSegment, clearReturnStack: false);
                // return;
                
                _returnStack.Push(new ReturnPoint { seg = returnSeg, stepInSeg = Mathf.Max(0, returnStep) });
                
                if (r.arcSegments != null && r.arcSegments.Length > 0 && r.arcSegments[0] != null)
                {
                    _currentArcSegments = r.arcSegments;
                    _currentArcIndex = 0;
                    JumpToSegmentFlow(_currentArcSegments[0], Mathf.Max(0, r.arcStartStepInFirstSegment), clearReturnStack: false);
                    return;
                }
                
                if (r.branchSegment == null)
                {
                    Debug.LogError("[BranchByScore] branchSegment 为空且 arcSegments 为空，直接回返回点继续。");
                    var rp = _returnStack.Pop();
                    JumpToSegmentFlow(rp.seg, rp.stepInSeg);
                    return;
                }

                _currentArcSegments = null;
                _currentArcIndex = -1;
                JumpToSegmentFlow(r.branchSegment, Mathf.Max(0, r.branchStartStepInSegment), clearReturnStack: false);
                return;
            }
        }

        Proceed();
    }
    
    bool TryGetFlattenPosition(int absIndex, out DialogueSegment seg, out int stepInSeg)
    {
        seg = null;
        stepInSeg = 0;

        if (segments == null || segments.Length == 0) return false;

        int cursor = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            var s = segments[i];
            if (s == null || s.steps == null) continue;

            int count = s.steps.Length;
            if (absIndex >= cursor && absIndex < cursor + count)
            {
                seg = s;
                stepInSeg = absIndex - cursor;
                return true;
            }
            cursor += count;
        }
        return false;
    }
    
}

public enum DialogueUIMode
{
    Style1 = 0,
    Bubble = 1,
    Hidden = 2
}

#region Data Types

[Serializable]
public class DialogueStep
{
    [Header("Editor Only（仅用于Inspector显示）")]
    public string stepTitle;
    
    [Header("对话框UI方案（每步选择）")]
    public DialogueUIMode uiMode = DialogueUIMode.Style1;

    [Tooltip("仅方案2(Bubble)有效：选择气泡显示位置编号 1/2/3")]
    [Range(1, 3)] public int bubblePos = 1;
    
    [Header("Step类型")]
    public DialogueStepKind stepKind = DialogueStepKind.Normal;

    [Header("显示")]
    public string speaker;
    public Sprite portrait;
    [TextArea(2, 5)] public string content;

    [Header("Gate通过条件")]
    public DialogueGateType gateType = DialogueGateType.ClickAnywhere;
    public Button button;
    public RectTransform draggable;
    public RectTransform dropZone;
    public MonoBehaviour focusMinigame;

    [Header("Timeline衔接")]
    public bool playTimelineOnStart = false;
    public string startTimelineKey;
    public bool playTimelineOnEnd = false;
    public string endTimelineKey;

    [Header("对话UI强制隐藏")]
    public bool hideDialogueUIThisStep = false;

    [Header("UI隐藏选项")]
    public bool hideUIWhileStartTimeline = false;
    public bool hideUIWhileGate = false;
    public bool hideUIWhileEndTimeline = false;

    [Header("显隐对象")]
    public FadeAction[] onStepStart;
    public FadeAction[] onStartTimelineStart;
    public FadeAction[] onStartTimelineEnd;
    public FadeAction[] onGateSuccess;
    public FadeAction[] onEndTimelineStart;
    public FadeAction[] onEndTimelineEnd;
    public FadeAction[] onStepEnd;

    [Header("Choice（stepKind=Choice）")]
    public ChoiceOption[] options;

    [Header("BranchByScore（stepKind=BranchByScore）")]
    public string branchScoreKey = "affection";
    public BranchRange[] branchRanges;
}

public enum DialogueGateType
{
    ClickAnywhere,
    ClickButton,
    DragToZone,
    FocusMinigame,
    TimelineComplete
}

public enum DialogueStepKind
{
    Normal,
    Choice,
    BranchByScore
}

[Serializable]
public class DialogueLine
{
    public string speaker;
    public Sprite portrait;
    
    [Header("Bubble UI (1/2/3)")]
    [Range(1, 3)] public int bubblePos = 1;
    
    [TextArea(2, 5)] public string content;
}

[Serializable]
public class ChoiceOption
{
    [Header("按钮文本")]
    public string optionText = "选项";

    [Header("积分变化")]
    public bool applyScore = true;
    public string scoreKey = "affection";
    public int scoreDelta = 0;
    
    // [Header("反馈对白使用气泡位置（1/2/3）")]
    // [Range(1,3)] public int feedbackBubblePos = 1;

    [Header("即时反馈对白（选中后播放，播完才算完成本Choice步骤）")]
    public DialogueLine[] feedbackLines;
}

[Serializable]
public class BranchRange
{
    public int minInclusive = 0;
    public int maxInclusive = 999;

    [Header("进入分支段")]
    [Tooltip("单段分支：只填 branchSegment\n分支串：填 arcSegments（优先使用 arcSegments）")]
    public DialogueSegment branchSegment;
    public int branchStartStepInSegment = 0;

    [Tooltip("分支串（按顺序播放）")]
    public DialogueSegment[] arcSegments;
    public int arcStartStepInFirstSegment = 0;

    [Header("分支播放结束后的返回点")]
    public DialogueSegment returnToMainlineSegment;
    public int returnStepInSegment = 0;
}

#endregion
