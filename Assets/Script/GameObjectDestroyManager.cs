using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameObjectDestroyManager : MonoBehaviour
{
    [SerializeField] private string[] targetSceneNames; // List of target scene names

    private static Dictionary<string, GameObjectDestroyManager> activeManagers = new Dictionary<string, GameObjectDestroyManager>(); // Tracks active managers by scene name
    public static List<GameObject> dontDestroyOnLoadObjects = new List<GameObject>(); // List to track DontDestroyOnLoad objects

    private void Awake()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Check if this scene already has a registered GameObject with this script
        if (activeManagers.ContainsKey(currentSceneName))
        {
            // If a manager exists for this scene, destroy this GameObject
            Destroy(gameObject);
            return;
        }

        // Register this GameObject for the current scene
        activeManagers[currentSceneName] = this;

        // Prevent this GameObject from being destroyed in target scenes
        DontDestroyOnLoad(gameObject);
        dontDestroyOnLoadObjects.Add(gameObject); // Add to the list of DontDestroyOnLoad objects
    }

    private void OnEnable()
    {
        // Subscribe to the sceneLoaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from the sceneLoaded event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Remove this manager from the dictionary when destroyed
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (activeManagers.ContainsKey(currentSceneName) && activeManagers[currentSceneName] == this)
        {
            activeManagers.Remove(currentSceneName);
        }

        // Remove from the list of DontDestroyOnLoad objects
        dontDestroyOnLoadObjects.Remove(gameObject);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string newSceneName = scene.name;

        // Check if the new scene is a target scene
        if (IsTargetScene(newSceneName))
        {
            // Do nothing if it's a target scene
            return;
        }

        // If it's not a target scene, destroy this GameObject
        Destroy(gameObject);
    }

    private bool IsTargetScene(string sceneName)
    {
        foreach (string targetName in targetSceneNames)
        {
            if (sceneName == targetName)
            {
                return true;
            }
        }
        return false;
    }
}