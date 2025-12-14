using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 机位按钮：外观切换 + 把点击上报给 BeatTimelineChallenge。
/// </summary>
[RequireComponent(typeof(Button))]
public class CameraToggleButton : MonoBehaviour, IPointerClickHandler
{
    public string cameraId = "A";

    [Header("视觉")]
    public Image background;
    public Color stoppedColor = Color.white;
    public Color recordingColor = new Color(1f, 0.3f, 0.3f, 1f);
    public Text  label;
    public string stoppedText = "开始";
    public string recordingText = "停止";

    Button _btn;
    BeatTimelineChallenge _controller;
    bool _isRecording = false;

    void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(() => _controller?.OnCameraButtonClicked(this));
        ApplyVisual();
    }

    public void SetController(BeatTimelineChallenge c) => _controller = c;

    public void ResetVisualToStopped()
    {
        _isRecording = false;
        ApplyVisual();
    }

    public void ToggleState()
    {
        _isRecording = !_isRecording;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (background) background.color = _isRecording ? recordingColor : stoppedColor;
        if (label) label.text = _isRecording ? recordingText : stoppedText;
    }

    // 作为辅助日志：保证点击确实到达了按钮
    public void OnPointerClick(PointerEventData eventData)
    {
        // 这里不做别的，仅作为“点击事件抵达 UI”的确认信号
        // Debug.Log($"[CamBtn:{name}] pointer click");
    }
}