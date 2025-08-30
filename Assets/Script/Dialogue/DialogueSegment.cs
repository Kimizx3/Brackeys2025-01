using UnityEngine;

public class DialogueSegment : MonoBehaviour
{
    [Header("段名（仅标识用）")]
    public string segmentTitle = "Segment";

    [Header("本段的步骤列表")]
    public DialogueStep[] steps;
}