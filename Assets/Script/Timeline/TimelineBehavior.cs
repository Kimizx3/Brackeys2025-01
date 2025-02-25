using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;


public class TimelineBehavior: MonoBehaviour
{
    public PlayableDirector director;
    public bool paused = false;
    public List<GameObject> sfxObjects;

    void Update()
    {
        if(paused && Input.GetKeyDown(KeyCode.Mouse0))
        {
            resumeTimeline();
        }
    }

    public void pauseTimeline()
    {
        director.playableGraph.GetRootPlayable(0).SetSpeed(0);
        paused = true;
    }

    public void resumeTimeline()
    {
        director.playableGraph.GetRootPlayable(0).SetSpeed(1);
        paused = false;
    }

    public void PlayMusic()
    {
        foreach (var gameObject in sfxObjects)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Play();
            }
            else
            {
                Debug.LogError("AudioSource component missing on " + gameObject.name);
            }
        }
    }
}