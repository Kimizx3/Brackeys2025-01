using UnityEngine;

[CreateAssetMenu(fileName = "NewDrink", menuName = "Drink/DrinkData")]
public class DrinkData : ScriptableObject
{
    public string drinkName;
    public GameObject drinkPrefab;
}
