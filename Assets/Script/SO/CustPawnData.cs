using UnityEngine;

[CreateAssetMenu(fileName = "CustomerSpawnData", menuName = "Customer/SpawnData")]
public class CustomerSpawnData : ScriptableObject
{
    public CustomerData[] customers;
}
