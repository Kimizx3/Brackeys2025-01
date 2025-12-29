using UnityEngine;
using UnityEngine.UI;

public class BubbleDialogueUI : MonoBehaviour
{
    public enum ResizePivot
    {
        Center,
        LeftTop,
        LeftBottom,
        RightTop,
        RightBottom
    }

    [Header("Root")]
    public GameObject root;
    public RectTransform bubbleRect;
    public Text bubbleText;

    [Header("Preset Positions (1/2/3)")]
    public Transform pos1;
    public Transform pos2;
    public Transform pos3;

    [Header("Auto Size")]
    public float maxWidth = 900f;
    public Vector2 padding = new Vector2(40f, 28f);
    public Vector2 extraOffset = Vector2.zero;

    [Header("Speaker Style")]
    public bool styleSpeaker = true;
    [Tooltip("默认#8B0000")]
    public Color speakerColor = new Color(1f, 0.82f, 0.34f, 1f); // #8B0000
    public bool speakerBold = true;

    [Header("Resize Pivot Per Position")]
    public ResizePivot pivotForPos1 = ResizePivot.LeftTop;
    public ResizePivot pivotForPos2 = ResizePivot.Center;
    public ResizePivot pivotForPos3 = ResizePivot.RightTop;

    void Reset()
    {
        root = gameObject;
        bubbleRect = GetComponent<RectTransform>();
        bubbleText = GetComponentInChildren<Text>(true);
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }
    
    public void ShowAt(int positionIndex, string speaker, string content)
    {
        if (string.IsNullOrWhiteSpace(speaker))
        {
            ShowAt(positionIndex, content);
            return;
        }

        if (bubbleText != null)
        {
            bubbleText.supportRichText = true;

            if (styleSpeaker)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(speakerColor);
                string namePart = speakerBold
                    ? $"<b><color=#{colorHex}>{speaker}</color></b>"
                    : $"<color=#{colorHex}>{speaker}</color>";
                
                string merged = $"{namePart}\n{content}";
                ShowAt(positionIndex, merged);
                return;
            }
        }

        ShowAt(positionIndex, $"{speaker}: {content}");
    }

    public void ShowAt(int positionIndex, string content)
    {
        if (root == null || bubbleRect == null || bubbleText == null)
        {
            Debug.LogError("[BubbleDialogueUI] missing references.");
            return;
        }

        root.SetActive(true);
        
        Transform anchor = GetAnchor(positionIndex);
        if (anchor != null)
        {
            bubbleRect.position = anchor.position;
            bubbleRect.anchoredPosition += extraOffset;
        }
        
        bubbleRect.pivot = GetPivot(GetPivotForPosition(positionIndex));
        
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        bubbleText.text = content ?? "";
        
        Canvas.ForceUpdateCanvases();

        float preferredWidth = bubbleText.preferredWidth;
        float w = Mathf.Min(preferredWidth, maxWidth);

        RectTransform textRT = bubbleText.rectTransform;
        textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);

        Canvas.ForceUpdateCanvases();

        float h = bubbleText.preferredHeight;

        float bgW = w + padding.x * 2f;
        float bgH = h + padding.y * 2f;

        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgW);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bgH);
    }

    ResizePivot GetPivotForPosition(int idx)
    {
        switch (idx)
        {
            case 1: return pivotForPos1;
            case 2: return pivotForPos2;
            case 3: return pivotForPos3;
            default: return pivotForPos1;
        }
    }

    Transform GetAnchor(int idx)
    {
        switch (idx)
        {
            case 1: return pos1;
            case 2: return pos2;
            case 3: return pos3;
            default: return pos1;
        }
    }

    static Vector2 GetPivot(ResizePivot p)
    {
        switch (p)
        {
            case ResizePivot.LeftTop: return new Vector2(0f, 1f);
            case ResizePivot.LeftBottom: return new Vector2(0f, 0f);
            case ResizePivot.RightTop: return new Vector2(1f, 1f);
            case ResizePivot.RightBottom: return new Vector2(1f, 0f);
            default: return new Vector2(0.5f, 0.5f);
        }
    }
}
