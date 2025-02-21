using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DrinkManager : MonoBehaviour
{
    // Public:
    [Header("UI")]
    public GameObject hoverEvent;
    public GameObject coffeeMachine;
    public GameObject menuSelect;
    public Material defaultMat;
    public Material renderMat;
    
    [Header("Drink Spawner")]
    public List<DrinkData> drinkList;
    public Transform spawnPoint;
    
    [Header("Making Coffee")]
    public Slider coffeeLoadingBar;
    public float coffeeMakingTime = 3.0f;
    
    
    
    
    // Private:
    private Renderer _objRenderer;
    private Dictionary<string, DrinkData> _drinkDictionary;
    private bool _isMakingCoffee = false;
    private string _selectedDrink;
    
    // private void OnEnable();
    // private void OnDisable();
    // private void SetupRayCast();
    // private void OnMouseEnter();
    // private void OnMouseExit();
    // private static bool IsClickingOnUI();
    // private void SpawnDrink(string DrinkName);
    // private void SpawnAmericano();
    // private void SpawnEspresso();
    // private void SpawnSteamer();
    

    private void OnEnable()
    {
        SpawnManager.americanoClickSpawner += SpawnAmericano;
        SpawnManager.espressoClickSpawner += SpawnEspresso;
        SpawnManager.steamerClickSpawner += SpawnSteamer;
    }

    private void OnDisable()
    {
        SpawnManager.americanoClickSpawner -= SpawnAmericano;
        SpawnManager.espressoClickSpawner -= SpawnEspresso;
        SpawnManager.steamerClickSpawner -= SpawnSteamer;
    }

    void Start()
    {
        _drinkDictionary = new Dictionary<string, DrinkData>();
        foreach (var drink in drinkList)
        {
            _drinkDictionary[drink.drinkName] = drink;
        }
        
        if (coffeeMachine == null) { return; }

        _objRenderer = coffeeMachine.GetComponent<Renderer>();

        if (_objRenderer == null) { return; }

        _objRenderer.material = defaultMat;

        if (hoverEvent != null) { hoverEvent.SetActive(false); }
        
        if (menuSelect != null) { menuSelect.SetActive(false); }
    }

    void Update()
    {
        SetUpRayCast();
    }

    private void SetUpRayCast()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == coffeeMachine)
            {
                OnMouseEnter();
            }
            else
            {
                OnMouseExit();
            }
        }
        else
        {
            OnMouseExit();
        }
    }

    private void OnMouseEnter()
    {
        _objRenderer.material = renderMat;
        if (hoverEvent != null)
        {
            hoverEvent.SetActive(true);
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                menuSelect.SetActive(true);
            }
        }
    }

    private void OnMouseExit()
    {
        _objRenderer.material = defaultMat;
        if (hoverEvent != null)
        {
            hoverEvent.SetActive(false);
            if (Input.GetKeyDown(KeyCode.Mouse0) && !IsClickingOnUI())
            {
                menuSelect.SetActive(false);
            }
        }
    }
    
    private static bool IsClickingOnUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
    
    private void SpawnDrink(string drinkName)
    {
        if (_drinkDictionary.TryGetValue(drinkName, out DrinkData drinkData))
        {
            GameObject spawnedDrink = Instantiate(drinkData.drinkPrefab, spawnPoint.position, Quaternion.identity);
            
            // Attach DraggableObject script dynamically
            if (!spawnedDrink.GetComponent<Dragable>())
            {
                spawnedDrink.AddComponent<Dragable>();
            }
        }
        else
        {
            Debug.LogError($"Drink '{drinkName}' not found in dictionary!");
            return;
        }
    }

    private void SpawnAmericano()
    {
        StartMakingCoffee("americano");
    }

    private void SpawnEspresso()
    {
        StartMakingCoffee("espresso");
    }

    private void SpawnSteamer()
    {
        StartMakingCoffee("steamer");
    }
    
    public void StartMakingCoffee(string drinkName)
    {
        if (!_isMakingCoffee)
        {
            _selectedDrink = drinkName;
            StartCoroutine(FillCoffeeBar());
        }
    }

    private IEnumerator FillCoffeeBar()
    {
        _isMakingCoffee = true;
        coffeeLoadingBar.gameObject.SetActive(true);
        coffeeLoadingBar.value = 0;

        float elapsedTime = 0;
        while (elapsedTime < coffeeMakingTime)
        {
            elapsedTime += Time.deltaTime;
            coffeeLoadingBar.value = elapsedTime / coffeeMakingTime;
            yield return null;
        }

        coffeeLoadingBar.value = 1;
        coffeeLoadingBar.gameObject.SetActive(false);
        _isMakingCoffee = false;
    }
    
    
    // Hover mouse to the coffee cup/machine
    // Pop up bubble with different kind of coffee
    // Click to make the coffee
    // Initiated selected coffee
    // Pick up the coffee serve to customer
}
