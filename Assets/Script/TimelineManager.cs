using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class TimelineManager : MonoBehaviour
{
    public PlayableDirector[] timelines;
    public GameObject[] disableList;
    public GameObject cosmicPanel;
    public string nextSceneName;
    private int _currentTimeline = 0;

    private void Awake()
    {
        int i = disableList.Length - 1;
        while (i >= 0)
        {
            disableList[i].SetActive(false);
            i--;
        }
    }

    private void Start()
    {
        if (timelines.Length > 0)
        {
            EnableSetter(0);
            PlayTimeLine(0);
            DisableSetter(0);
        }
    }

    
    

    public void PlayTimeLine(int index)
    {
        if (index < 0 || index >= timelines.Length) return;
        
        
        if (timelines[_currentTimeline].state == PlayState.Playing)
        {
            timelines[_currentTimeline].Stop();
        }
        _currentTimeline = index;
        timelines[_currentTimeline].Play();
        timelines[_currentTimeline].stopped += OnTimelineFinished;
    }
    
    void OnTimelineFinished(PlayableDirector director)
    {
        if (_currentTimeline == 0)
        {
            EnableSetter(1);
            PlayTimeLine(1);
            DisableSetter(1);
            cosmicPanel.SetActive(false);
        }

        if (_currentTimeline == 2)
        {
            LoadNextScene();
        }
    }

    public void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    public void PlayTimelineFromButton(int index)
    {
        PlayTimeLine(index);
    }
    
    private void DisableSetter(int setter)
    {
        disableList[setter].SetActive(false);
    }
    
    private void EnableSetter(int setter)
    {
        disableList[setter].SetActive(true);
    }
}
