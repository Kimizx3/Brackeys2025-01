using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; 
#endif

public class QuitGameButton : MonoBehaviour
{
    [SerializeField] bool stopInEditor = true;
    
    public void QuitGame()
    {
#if UNITY_EDITOR
        if (stopInEditor)
        {
            EditorApplication.isPlaying = false; 
            return;
        }
#endif

#if UNITY_WEBGL
     
#else
        Application.Quit(); 
#endif
    }
}