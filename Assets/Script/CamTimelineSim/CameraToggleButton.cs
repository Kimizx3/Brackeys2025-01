using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    private Button _btn;
    private TimelineClickChallenge _controller;
    private bool _isRecording = false;

    void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClicked);
        ApplyVisual();
        Debug.Log($"[CamBtn:{name}] Awake cameraId={cameraId}", this);
    }
    void OnEnable()
    {
        ApplyVisual();
        Debug.Log($"[CamBtn:{name}] OnEnable (interactable={_btn.interactable}, active={gameObject.activeInHierarchy})", this);
    }

    public void SetController(TimelineClickChallenge c)
    {
        _controller = c;
        Debug.Log($"[CamBtn:{name}] SetController => {(_controller ? _controller.name : "null")}", this);
    }

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

    void OnClicked()
    {
        Debug.Log($"[CamBtn:{name}] Button.onClick (controller={(_controller? _controller.name : "null")})", this);
        _controller?.OnCameraButtonClicked(this);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[CamBtn:{name}] IPointerClickHandler received ({eventData.button})", this);
    }
}
