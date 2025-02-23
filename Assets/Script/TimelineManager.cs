using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;

public class TimelineManager : MonoBehaviour
{
    public PlayableDirector[] timelines;
    public GameObject[] disableList;
    public GameObject cosmicPanel;
    public string nextSceneName;
    private int _currentTimeline = 0;
    private bool waitForInput = false;

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
            ++_currentTimeline;
        }
    }

    private void Update()
    {
        if (waitForInput && Input.GetKeyDown(KeyCode.Return))
        {
            waitForInput = false;
            PlayTimeLine(1);
        }
    }


    public void PlayTimeLine(int index)
    {
        if (index < 0 || index >= timelines.Length) return;
    
        if (timelines[index].state == PlayState.Playing)
        {
            timelines[index].Stop();
        }

        _currentTimeline = index;
        timelines[index].gameObject.SetActive(true);
        timelines[index].Play();
        timelines[index].stopped += OnTimelineFinished;
    }
    //
    void OnTimelineFinished(PlayableDirector director)
    {
        if (_currentTimeline == 0)
        {
            waitForInput = true;
        }
        else if (_currentTimeline == 1)
        {
           StartCoroutine(WaitCoroutine());
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

    IEnumerator WaitCoroutine()
    {
        if (cosmicPanel != null)
        {
            cosmicPanel.SetActive(false);
        }
        
        yield return new WaitForSeconds(3f);
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
