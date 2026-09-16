using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuInfoPanels : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TextAsset howToPlayText;
    [SerializeField] private TextAsset gddText;

    private void Awake()
    {
        Close();
    }

    public void OpenHowToPlay()
    {
        Open("How to Play", howToPlayText);
    }

    public void OpenGdd()
    {
        Open("GDD", gddText);
    }

    public void Close()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void Open(string heading, TextAsset document)
    {
        if (overlay == null || title == null || body == null || scrollRect == null)
            return;

        title.text = heading;
        body.text = document != null ? document.text : string.Empty;
        overlay.SetActive(true);

        Canvas.ForceUpdateCanvases();
        body.ForceMeshUpdate();
        var bodyRect = body.rectTransform;
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, body.preferredHeight + 32f);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
