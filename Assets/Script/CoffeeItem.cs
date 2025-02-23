using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CoffeeItem : MonoBehaviour
{
    public DrinkData drinkData;
    private bool _hasReceivedCoffee = false;

    public string getDrinkName()
    {
        return drinkData != null ? drinkData.drinkName : "";
    }
    private void OnTriggerEnter(Collider other)
    {
        Customer customer = other.GetComponent<Customer>();

        if (customer != null && customer.orderedDrink != null && drinkData != null)
        {
            // ✅ Compare coffee's name with customer's order
            if (drinkData.drinkName == customer.orderedDrink.drinkName)
            {
                Destroy(gameObject); // ✅ Destroy coffee if correct
                customer.MarkOrderCompleted(); // ✅ Mark the order as completed
            }
        }
    }
    
}
