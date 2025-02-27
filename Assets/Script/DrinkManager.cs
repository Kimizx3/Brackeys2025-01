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
    private DrinkData _selectedDrink;
    
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
    // private void StartMakingDrinks();
    // private void FillCoffeeBar();
    

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
    
    private void SpawnDrink(DrinkData drinkName)
    {
        GameObject spawnedDrink = Instantiate(drinkName.drinkPrefab, spawnPoint.position, Quaternion.Euler(0, 180, 0));
        CoffeeItem coffeeItem = spawnedDrink.GetComponent<CoffeeItem>();
        coffeeItem.drinkData = drinkName;

        if (!spawnedDrink.GetComponent<Dragable>())
        {
            spawnedDrink.AddComponent<Dragable>();
        }
    }

    private void SpawnAmericano()
    {
        if (_drinkDictionary.TryGetValue("americano", out DrinkData drinkData))
        {
            StartMakingCoffee(drinkData);
        }
        
    }

    private void SpawnEspresso()
    {
        if (_drinkDictionary.TryGetValue("espresso", out DrinkData drinkData))
        {
            StartMakingCoffee(drinkData);
        }
    }

    private void SpawnSteamer()
    {
        if (_drinkDictionary.TryGetValue("steamer", out DrinkData drinkData))
        {
            StartMakingCoffee(drinkData);
        }
    }
    
    public void StartMakingCoffee(DrinkData drinkName)
    {
        if (!_isMakingCoffee && drinkName != null)
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

        if (_selectedDrink != null)
        {
            SpawnDrink(_selectedDrink);
        }
    }
}
