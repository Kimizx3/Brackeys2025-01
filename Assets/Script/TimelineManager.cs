using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    public List<PlayableDirector> timelines;
    private PlayableDirector _currentTimeline;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            PlayTimeLine(0);
        }
    }


    public void PlayTimeLine(int index)
    {
        if (index < 0 || index >= timelines.Count)
        {
            return;
        }

        if (_currentTimeline != null && _currentTimeline.state == PlayState.Playing)
        {
            _currentTimeline.Stop();
        }

        _currentTimeline = timelines[index];
        _currentTimeline.Play();
    }

    public void PauseTimeline()
    {
        if (_currentTimeline != null && _currentTimeline.state == PlayState.Playing)
        {
            _currentTimeline.Pause();
        }
    }

    public void StopTimeline()
    {
        if (_currentTimeline != null)
        {
            _currentTimeline.Stop();
        }
    }
}
