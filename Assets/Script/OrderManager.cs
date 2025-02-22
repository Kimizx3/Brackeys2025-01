using System.Collections.Generic;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    // Click to accept order
    // Order will be displayed on the board
    // Serve to customer
    // Order resolved
    // Order not resolved
    // Sad emoji by customer
    public List<DrinkData> allDrinks;
    private List<DrinkData> _availableDrinks;

    private void Start()
    {
        _availableDrinks = new List<DrinkData>(allDrinks);
    }
    
    public DrinkData GetUniqueCoffeeOrder()
    {
        if (_availableDrinks.Count == 0)
        {
            _availableDrinks = new List<DrinkData>(allDrinks); // Reset list if exhausted
        }

        int randomIndex = Random.Range(0, _availableDrinks.Count);
        DrinkData selectedCoffee = _availableDrinks[randomIndex];
        _availableDrinks.RemoveAt(randomIndex); // Remove to ensure uniqueness
        return selectedCoffee;
    }
}
