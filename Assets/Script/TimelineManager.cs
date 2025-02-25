using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;
using NUnit.Framework.Constraints;
using Unity.VisualScripting;

public class TimelineManager : MonoBehaviour
{
    public List<PlayableDirector> timelines = new List<PlayableDirector>();
    public List<GameObject> canvases = new List<GameObject>();
    private int _currentIndex = 0;
    public bool playOnAwake = true;
    public bool finishPlayedSceneTransition = true;
    public GameObject UIpanel;

    private void Start()
    {
        if (timelines.Count == 0 || canvases.Count == 0 || timelines.Count != canvases.Count)
        {
            return;
        }

        foreach (GameObject canvas in canvases)
        {
            canvas.SetActive(false);
        }

        foreach (PlayableDirector timeline in timelines)
        {
            timeline.Stop();
        }

        for (int i = 1; i < canvases.Count; i++)
        {
            canvases[i].SetActive(false);
        }

        if(playOnAwake)
        {
            PlayCurrentTimeline();
        }
        
    }

    public void PlayCurrentTimeline()
    {
        foreach (PlayableDirector timeline in timelines)
        {
            timeline.Stop();
        }
        
        if (_currentIndex >= timelines.Count)
        {
            return;
        }

        canvases[_currentIndex].SetActive(true);

        timelines[_currentIndex].stopped += OnCurrentTimelineStopped;
        timelines[_currentIndex].Play();
    }

    private void OnCurrentTimelineStopped(PlayableDirector director)
    {
        director.stopped -= OnCurrentTimelineStopped;


        canvases[_currentIndex].SetActive(false);
        _currentIndex++;

        if (_currentIndex < canvases.Count)
        {
            canvases[_currentIndex].SetActive(true);
            PlayCurrentTimeline();
        }
        else
        {
            if(UIpanel != null)
            {
                UIpanel.SetActive(true);
            }

            if(finishPlayedSceneTransition)
            {
                LoadToNextScene();
            }
        }
    }

    public void LoadToNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
    }
}
