using UnityEngine;

[CreateAssetMenu(fileName = "NewCustomer", menuName = "Customer/CustomerData")]
public class CustomerData : ScriptableObject
{
    public GameObject customerPrefab;
    public DrinkData orderedDrink;
    public GameObject drinkUI;
}
