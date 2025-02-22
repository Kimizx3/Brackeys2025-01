using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UI_RaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left Click
        {
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, results);

            foreach (var result in results)
            {
                Debug.Log("Hit UI: " + result.gameObject.name);
            }

            if (results.Count == 0)
            {
                Debug.Log("No UI detected. Raycast might be blocked.");
            }
        }
    }
}
