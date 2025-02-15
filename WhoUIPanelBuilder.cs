using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WhoUIPanelBuilder : MonoBehaviour
{
    public GameObject playerNamePrefab; // Assign a prefab to use for player names in the inspector

    [ContextMenu("Build WhoUIPanel")]
    public void BuildWhoUIPanel()
    {
        // Create the WhoUIPanel
        GameObject whoUIPanel = new GameObject("WhoUIPanel");

        // Get or add RectTransform
        RectTransform whoUIPanelRect = whoUIPanel.GetComponent<RectTransform>();
        if (whoUIPanelRect == null)
        {
            whoUIPanelRect = whoUIPanel.AddComponent<RectTransform>();
        }

        whoUIPanel.AddComponent<CanvasRenderer>();
        Image panelImage = whoUIPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.5f); // Background with transparency
        whoUIPanelRect.sizeDelta = new Vector2(200, 300);
        whoUIPanel.transform.SetParent(transform, false);

        // Create Scroll View
        GameObject scrollView = CreateScrollView(whoUIPanel.transform);

        // Add a sample prefab to test
        if (playerNamePrefab != null)
        {
            Transform contentTransform = scrollView.transform.Find("Viewport/Content");
            if (contentTransform != null)
            {
                GameObject samplePrefab = Instantiate(playerNamePrefab, contentTransform);
                samplePrefab.name = "Sample Player Name";
                TextMeshProUGUI textComponent = samplePrefab.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = "Sample Player Name";
                }
                else
                {
                    Debug.LogError("TextMeshProUGUI component not found on PlayerNamePrefab!");
                }
            }
            else
            {
                Debug.LogError("Content object not found under Viewport.");
            }
        }
        else
        {
            Debug.LogError("PlayerNamePrefab is not assigned in the Inspector.");
        }

#if UNITY_EDITOR
        // Mark the new GameObjects as part of the scene so they persist after Play mode
        Undo.RegisterCreatedObjectUndo(whoUIPanel, "Create WhoUIPanel");
        EditorUtility.SetDirty(whoUIPanel);
#endif

        Debug.Log("WhoUIPanel created and added to the scene.");
    }

    GameObject CreateScrollView(Transform parent)
    {
        // Create Scroll View
        GameObject scrollView = new GameObject("Scroll View");
        scrollView.AddComponent<ScrollRect>();
        RectTransform scrollViewRect = scrollView.GetComponent<RectTransform>();
        if (scrollViewRect == null)
        {
            scrollViewRect = scrollView.AddComponent<RectTransform>();
        }
        scrollViewRect.sizeDelta = new Vector2(200, 300);
        scrollView.transform.SetParent(parent, false);

        // Add Viewport
        GameObject viewport = new GameObject("Viewport");
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        if (viewportRect == null)
        {
            viewportRect = viewport.AddComponent<RectTransform>();
        }
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.transform.SetParent(scrollView.transform, false);

        // Add Content
        GameObject content = new GameObject("Content");
        RectTransform contentRect = content.GetComponent<RectTransform>();
        if (contentRect == null)
        {
            contentRect = content.AddComponent<RectTransform>();
        }
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.spacing = 5;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;

        ContentSizeFitter sizeFitter = content.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        content.transform.SetParent(viewport.transform, false);

        // Assign components to Scroll Rect
        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;

        return scrollView;
    }
}
