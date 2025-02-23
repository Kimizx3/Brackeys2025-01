using UnityEngine;

public class Dragable : MonoBehaviour
{
    private bool isDragging = false;
    private Camera mainCamera;
    private Vector3 offset;
    private Vector3 originalPosition;
    private CoffeeItem _coffeeItem;
    

    void Start()
    {
        mainCamera = Camera.main; // Get main camera
        _coffeeItem = GetComponent<CoffeeItem>();
    }

    void OnMouseDown()
    {
        originalPosition = transform.position;
        Vector3 worldMousePos = GetMouseWorldPosition();
        offset = transform.position - GetMouseWorldPosition();
        isDragging = true;
        
    }

    void OnMouseUp()
    {
        isDragging = false;

        Customer[] customers = FindObjectsOfType<Customer>();

        foreach (Customer customer in customers)
        {
            // ✅ Check if the coffee is at the same X & Y but in front of the customer in Z
            if (Mathf.Abs(transform.position.x - customer.transform.position.x) < 0.5f &&
                Mathf.Abs(transform.position.y - customer.transform.position.y) < 0.5f &&
                transform.position.z < customer.transform.position.z) // ✅ Coffee is in front
            {
                Destroy(gameObject); // ✅ Destroy coffee if it is in front of the customer
                return;
            }
        }

        transform.position = originalPosition;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            Vector3 worldMousePos = GetMouseWorldPosition();
            transform.position = new Vector3(worldMousePos.x + offset.x, worldMousePos.y + offset.y, transform.position.z);
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = mainCamera.WorldToScreenPoint(transform.position).z; // Preserve depth
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }
}
