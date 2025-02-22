using UnityEngine;
using UnityEngine.Playables;

public class TimelineBehavior: MonoBehaviour
{
    public PlayableDirector director;
    public bool paused = false;

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
}
