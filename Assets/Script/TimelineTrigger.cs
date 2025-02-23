using System;
using UnityEngine;

public class TimelineTrigger : MonoBehaviour
{
    public TimelineManager timelineManager;
    public int timelineIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            timelineManager.PlayTimeLine(timelineIndex);
        }
    }
}
