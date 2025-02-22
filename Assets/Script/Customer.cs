using UnityEngine;

public class Customer : MonoBehaviour
{
    public Transform customerHead; // Assign customer's head transform in Inspector
    public Vector3 offset = new Vector3(0, 100, 0); // UI offset from the head
    private RectTransform uiElement;
    private Camera mainCamera;
    private DrinkData myOrder;
    
    void Start()
    {
        uiElement = GetComponent<RectTransform>();

        if (uiElement == null)
        {
            this.enabled = false; // Disable script to prevent errors
            return;
        }

        mainCamera = Camera.main;
    }
    void Update()
    {
        PlaceUIOnHead();
    }

    public void SetOrder(DrinkData order)
    {
        myOrder = order;
    }

    public DrinkData GetOrder()
    {
        return myOrder;
    }

    private void PlaceUIOnHead()
    {
        if (customerHead == null || uiElement == null) return;

        // Convert world position to screen space
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(customerHead.position);

        // Set UI position in screen space
        uiElement.position = screenPosition + offset;
        
        // Hide UI if the character is off-screen
        if (screenPosition.z < 0) 
            uiElement.gameObject.SetActive(false);
        else 
            uiElement.gameObject.SetActive(true);
    }
}
