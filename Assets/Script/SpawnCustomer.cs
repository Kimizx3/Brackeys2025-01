using UnityEngine;

public class SpawnCustomer : MonoBehaviour
{
    [System.Serializable]
    public class CustomerSpawnPoint
    {
        public Transform spawnPoint; // Assign in Inspector
        public CustomerData customerData; // Assign specific customer prefab in Inspector
    }

    public CustomerSpawnPoint[] customerSpawnPoints; // Fixed list of spawn points & customers
    public Canvas uiCanvas; // Assign Canvas

    void Start()
    {
        SpawnCustomers();
    }

    void SpawnCustomers()
    {
        foreach (CustomerSpawnPoint spawnData in customerSpawnPoints)
        {
            if (spawnData.spawnPoint == null || spawnData.customerData == null || spawnData.customerData.customerPrefab == null)
                continue;
        
            GameObject customerObj = Instantiate(spawnData.customerData.customerPrefab, spawnData.spawnPoint.position, Quaternion.identity);
            Transform customerHead = customerObj.transform.Find("Head");
        
            if (customerHead == null)
                continue;
        
            Customer customerScript = customerObj.GetComponent<Customer>();
            if (customerScript != null)
            {
                customerScript.SetOrder(spawnData.customerData, spawnData.customerData.drinkUI);
            }
        
            
            GameObject orderUI = Instantiate(spawnData.customerData.drinkUI, customerHead.position, Quaternion.Euler(0,180,0));
            orderUI.transform.SetParent(customerHead, false);
            
            Customer customerUI = orderUI.GetComponent<Customer>();
            if (customerUI != null)
            {
                customerUI.customerHead = customerHead;
            }
        }
        
        // foreach (CustomerSpawnPoint spawnData in customerSpawnPoints)
        // {
        //     if (spawnData.spawnPoint == null || spawnData.customerData == null || spawnData.customerData.customerPrefab == null)
        //         continue;
        //
        //     // ✅ Spawn customer
        //     GameObject customerObj = Instantiate(spawnData.customerData.customerPrefab, spawnData.spawnPoint.position, Quaternion.identity);
        //     Customer customerScript = customerObj.GetComponent<Customer>();
        //
        //     if (customerScript != null)
        //     {
        //         Transform customerHead = customerObj.transform.Find("Head");
        //         if (customerHead != null)
        //         {
        //             customerScript.customerHead = customerHead;
        //         }
        //
        //         // ✅ Set order and spawn UI
        //         customerScript.SetOrder(spawnData.customerData, spawnData.customerData.drinkUI);
        //     }
        // }
    }
}