using System;
using UnityEngine;
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
    
    
    
    // Private:
    private Renderer _objRenderer;

    void Start()
    {
        if (coffeeMachine == null)
        {
            return;
        }

        _objRenderer = coffeeMachine.GetComponent<Renderer>();

        if (_objRenderer == null)
        {
            return;
        }

        _objRenderer.material = defaultMat;

        if (hoverEvent != null)
        {
            hoverEvent.SetActive(false);
        }
        
        if (menuSelect != null)
        {
            menuSelect.SetActive(false);
        }
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
    
    private bool IsClickingOnUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
    
    public void SpawnDrink(string drinkName)
    {
        DrinkData drink = drinkList.Find(d => d.drinkName == drinkName);
        if (drink != null)
        {
            Instantiate(drink.drinkPrefab, spawnPoint.position, Quaternion.identity);
        }
        else
        {
            return;
        }
    }

    public void OnAmericanoClicked() { SpawnDrink("americano"); }
    
    public void OnEspressoClicked() { SpawnDrink("espresso"); }
    
    public void OnSteamerClicked() { SpawnDrink("steamer"); }
    

    // private void HoverChecker();
    // private void OnMouseEnter();
    // private void OnMouseExit();
    // public void MakeCoffee();
    
    
    // Hover mouse to the coffee cup/machine




    // Pop up bubble with different kind of coffee
    // Click to make the coffee
    // Initiated selected coffee
    // Pick up the coffee serve to customer
}
