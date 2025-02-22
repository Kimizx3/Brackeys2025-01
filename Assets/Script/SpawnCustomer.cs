using UnityEngine;

public class SpawnCustomer : MonoBehaviour
{
    [System.Serializable]
    public class CustomerSpawnPoint
    {
        public Transform spawnPoint;  // Assign in Inspector
        public CustomerData customerData;  // Assign specific customer prefab in Inspector
    }

    public CustomerSpawnPoint[] customerSpawnPoints;  // Fixed list of spawn points & customers
    public GameObject orderUI_Prefab;  // Assign UI Prefab
    public Canvas uiCanvas;  // Assign Canvas

    void Start()
    {
        SpawnCustomers();
    }

    void SpawnCustomers()
    {
        foreach (CustomerSpawnPoint spawnData in customerSpawnPoints)
        {
            if (spawnData.spawnPoint == null || spawnData.customerData == null || spawnData.customerData.customerPrefab == null)
            {
                Debug.LogWarning("Spawn Point or Customer Data is missing!", this);
                continue;
            }

            // 🟢 STEP 1: Spawn the customer at the fixed position
            GameObject customerObj = Instantiate(spawnData.customerData.customerPrefab, spawnData.spawnPoint.position, Quaternion.identity);

            // Get the customer's head transform dynamically
            Transform customerHead = customerObj.transform.Find("Head"); 

            if (customerHead == null)
            {
                continue;
            }

            // 🟢 STEP 2: Set customer’s order
            Customer customerScript = customerObj.GetComponent<Customer>();
            if (customerScript != null)
            {
                customerScript.SetOrder(spawnData.customerData.orderedDrink);
            }

            // 🟢 STEP 3: Spawn UI element for the customer
            GameObject orderUI = Instantiate(orderUI_Prefab, uiCanvas.transform);
            Customer customerUI = orderUI.GetComponent<Customer>();

            if (customerUI != null)
            {
                customerUI.customerHead = customerHead; // Attach dynamically found head transform
            }
            else
            {
                Debug.LogError("CustomerUI script is missing from orderUI_Prefab!");
            }
        }
    }
}
