using UnityEngine;

public class ChallengeAutoStarter : MonoBehaviour
{
    public TimelineClickChallenge challenge;
    public bool runOnStart = true;

    void Reset()
    {
        if (!challenge) challenge = GetComponent<TimelineClickChallenge>();
    }

    void Start()
    {
        if (runOnStart && challenge)
        {
            Debug.Log("[AutoStarter] StartGame()");
            challenge.autoStartForDebug = true;   // 确保会自启动
            challenge.verboseAlways = true;       // 强制日志
            challenge.StartGame(() => Debug.Log("[AutoStarter] Challenge completed!"));
        }
        else
        {
            Debug.LogWarning("[AutoStarter] 未找到 TimelineClickChallenge 或 runOnStart 未勾选。");
        }
    }
}