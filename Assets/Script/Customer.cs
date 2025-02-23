using System;
using Unity.Mathematics;
using UnityEngine;


public class Customer : MonoBehaviour
{
    public Transform customerHead; // Assign customer's head transform in Inspector
    public Vector3 offset = new Vector3(0, 100, 0); // UI offset from the head
    
    private RectTransform uiElement;
    private Camera mainCamera;
    
    public DrinkData orderedDrink;
    private GameObject orderUI; // The UI element above the customer
    private bool _hasReceivedCoffee = false;
    
    
    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        PlaceUIOnHead();
    }

    public void SetOrder(CustomerData customerData, GameObject uiPrefab)
    {
        orderedDrink = customerData.orderedDrink;
        
        
        if (orderUI != null)
        {
            orderUI = customerData.drinkUI;
            orderUI = Instantiate(uiPrefab, customerHead.position, Quaternion.Euler(0,180,0));
            orderUI.transform.SetParent(customerHead, false);
        }
    }

    public DrinkData GetOrder()
    {
        return orderedDrink;
    }

    private void PlaceUIOnHead()
    {
        if (customerHead == null || uiElement == null) return;

        // Convert world position to screen space
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(customerHead.position);
        
        transform.position = screenPosition;
        orderUI.transform.position = customerHead.position;
    }
    
    public void MarkOrderCompleted()
    {
        if (!_hasReceivedCoffee)
        {
            _hasReceivedCoffee = true;
            if (orderUI != null)
            {
                orderUI.SetActive(false);
            }
            // ✅ Add any completion logic here (e.g., animations, score increase)
        }
    }

    private void CompleteOrder()
    {
        _hasReceivedCoffee = true;
        // TODO: Add animations, increase score, or play effects
    }
}
