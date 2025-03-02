using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;


public class TimelineBehavior: MonoBehaviour
{
    public PlayableDirector director;
    public bool paused = false;
    public List<GameObject> sfxObjects;
    public List<GameObject> sfxObjects_1;
    public List<GameObject> sfxObjects_2;
    public List<GameObject> sfxObjects_3;

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

    public void PlaySFX1()
    {
        foreach (var gameObject in sfxObjects_1)
        {
            var audioSource1 = gameObject.GetComponent<AudioSource>();
            if (audioSource1 != null)
            {
                audioSource1.Play();
            }
            else
            {
                Debug.LogError("AudioSource component missing on " + gameObject.name);
            }
        }
    }

    public void PlaySFX2()
    {
        foreach (var gameObject in sfxObjects_2)
        {
            var audioSource2 = gameObject.GetComponent<AudioSource>();
            if (audioSource2 != null)
            {
                audioSource2.Play();
            }
            else
            {
                Debug.LogError("AudioSource component missing on " + gameObject.name);
            }
        }
    }

    public void PlaySFX3()
    {
        foreach (var gameObject in sfxObjects_3)
        {
            var audioSource3 = gameObject.GetComponent<AudioSource>();
            if (audioSource3 != null)
            {
                audioSource3.Play();
            }
            else
            {
                Debug.LogError("AudioSource component missing on " + gameObject.name);
            }
        }
    }

    
}