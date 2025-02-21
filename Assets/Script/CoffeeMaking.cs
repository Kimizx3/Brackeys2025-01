using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CoffeeMaking : MonoBehaviour
{
    public Slider coffeeLoadingBar; // Assign in Inspector
    public float coffeeMakingTime = 3.0f; // Time to complete coffee

    private bool isMakingCoffee = false;

    // public static event System.Action OnCoffeeReady; // Notify when coffee is done

    public void StartMakingCoffee()
    {
        if (!isMakingCoffee)
        {
            StartCoroutine(FillCoffeeBar());
        }
    }

    private IEnumerator FillCoffeeBar()
    {
        isMakingCoffee = true;
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
        isMakingCoffee = false;

        // OnCoffeeReady?.Invoke(); // Notify listeners that coffee is ready
    }
}


    
