using UnityEngine;
using UnityEngine.EventSystems;


public class PanDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PanCaptureMinigame _game;
    private RectTransform _viewport;
    private Camera _uiCamera;
    private Vector2 _lastLocalPos;

    public void Init(PanCaptureMinigame game, RectTransform viewport, Camera uiCamera)
    {
        _game = game;
        _viewport = viewport;
        _uiCamera = uiCamera;
        enabled = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, _uiCamera, out _lastLocalPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 curLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, _uiCamera, out curLocal);
        Vector2 deltaLocal = curLocal - _lastLocalPos;
        _lastLocalPos = curLocal;

        _game?.OnDragLocalDelta(deltaLocal);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _game?.NotifyPointerUp();
    }
}