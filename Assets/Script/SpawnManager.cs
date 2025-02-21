using UnityEngine;
using UnityEngine.UI;
using System;

public class SpawnManager : MonoBehaviour
{
    [Header("Buttons")] 
    public Button americanoButton;
    public Button espressoButton;
    public Button steamerButton;

    public static event Action americanoClickSpawner;
    public static event Action espressoClickSpawner;
    public static event Action steamerClickSpawner;

    private void Start()
    {
        americanoButton.onClick.AddListener(() => americanoClickSpawner?.Invoke());
        espressoButton.onClick.AddListener(() => espressoClickSpawner?.Invoke());
        steamerButton.onClick.AddListener(() => steamerClickSpawner?.Invoke());
    }
}
