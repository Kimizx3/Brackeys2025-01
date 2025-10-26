using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TapToClose : MonoBehaviour, IPointerClickHandler
{
    public Action onClosed;

    public void OnPointerClick(PointerEventData eventData)
    {
        onClosed?.Invoke();
    }
}