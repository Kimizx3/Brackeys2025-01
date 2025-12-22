using UnityEngine;
using UnityEngine.UI;

public class BubbleDialogueUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public RectTransform bubbleRect;
    public Text bubbleText;

    [Header("Preset Positions (1/2/3)")]
    public Transform pos1;
    public Transform pos2;
    public Transform pos3;

    [Header("Auto Size")]
    [Tooltip("气泡最大宽度（超过会自动换行）")]
    public float maxWidth = 900f;

    [Tooltip("气泡内边距（左右，上下）")]
    public Vector2 padding = new Vector2(40f, 28f);

    [Tooltip("额外偏移")]
    public Vector2 extraOffset = Vector2.zero;

    void Reset()
    {
        root = gameObject;
        bubbleRect = GetComponent<RectTransform>();
        bubbleText = GetComponentInChildren<Text>(true);
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    public void ShowAt(int positionIndex, string speaker, string content)
    {
        if (string.IsNullOrWhiteSpace(speaker))
        {
            ShowAt(positionIndex, content);
            return;
        }
        
        string merged = $"{speaker}\n{content}";
        ShowAt(positionIndex, merged);
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
}
