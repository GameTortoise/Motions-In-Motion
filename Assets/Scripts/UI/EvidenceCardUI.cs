using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EvidenceCardUI : MonoBehaviour
{
    [Header("Card Content")]
    public Image noteBackground;
    public Sprite[] noteVariants;
    public TMP_Text evidenceNameText;
    public TMP_Text descriptionText;
    public Image evidenceImage;

    [Header("Selection")]
    public GameObject selectedBorder;

    private EvidenceData evidenceData;
    private EvidenceSelectionUI selectionManager;

    private bool isSelected = false;

    public void Setup(
        EvidenceData data,
        int evidenceNumber,
        EvidenceSelectionUI manager,
        Sprite stickyNoteSprite,
        float uniformScale,
        int noteStyleIndex
    )
    {
        evidenceData = data;
        selectionManager = manager;

        Sprite noteToDisplay = stickyNoteSprite;
        if (noteToDisplay == null && noteVariants != null && noteVariants.Length > 0)
        {
            noteToDisplay = noteVariants[(evidenceNumber - 1) % noteVariants.Length];
        }

        ApplyStickyNote(noteToDisplay, uniformScale, noteStyleIndex);

        // Set evidence title
        if (evidenceNameText != null)
        {
            if (!string.IsNullOrEmpty(data.evidenceName))
            {
                evidenceNameText.text = data.evidenceName;
            }
            else
            {
                evidenceNameText.text = $"Evidence {evidenceNumber}";
            }
        }

        // Set evidence description
        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        // Handle image evidence
        if (evidenceImage != null)
        {
            if (data.image != null)
            {
                evidenceImage.sprite = data.image;
                evidenceImage.gameObject.SetActive(true);

                if (descriptionText != null)
                {
                    descriptionText.gameObject.SetActive(false);
                }
            }
            else
            {
                evidenceImage.gameObject.SetActive(false);

                if (descriptionText != null)
                {
                    descriptionText.gameObject.SetActive(true);
                }
            }
        }

        SetSelected(false);
    }

    private void ApplyStickyNote(
        Sprite stickyNoteSprite,
        float uniformScale,
        int noteStyleIndex
    )
    {
        if (noteBackground == null)
        {
            noteBackground = GetComponent<Image>();
        }

        if (noteBackground != null)
        {
            if (stickyNoteSprite != null)
            {
                noteBackground.sprite = stickyNoteSprite;
            }

            if (noteBackground.sprite != null)
            {
                noteBackground.color = Color.white;
                noteBackground.type = Image.Type.Simple;
                noteBackground.preserveAspect = true;
            }
            else
            {
                Debug.LogError("EvidenceCard has no sticky note background assigned.", this);
            }
        }

        // Keep X and Y identical so none of the note artwork is distorted.
        float scale = Mathf.Clamp(uniformScale, 0.8f, 1.1f);
        transform.localScale = Vector3.one * scale;

        Image selectedImage = selectedBorder != null
            ? selectedBorder.GetComponent<Image>()
            : null;

        Sprite displayedNote = stickyNoteSprite != null
            ? stickyNoteSprite
            : noteBackground != null ? noteBackground.sprite : null;

        if (selectedImage != null && displayedNote != null)
        {
            selectedImage.sprite = displayedNote;
            selectedImage.type = Image.Type.Simple;
            selectedImage.preserveAspect = true;
        }

        ApplyTextLayout(noteStyleIndex);
    }

    private void ApplyTextLayout(int noteStyleIndex)
    {
        // The pinned notes reserve more space above the title. Values are normalized
        // so the text remains inset correctly when the grid or note scale changes.
        Vector2 titleMin;
        Vector2 titleMax;
        Vector2 descriptionMin;
        Vector2 descriptionMax;

        switch (noteStyleIndex)
        {
            case 0: // Yellow note with red pin
                titleMin = new Vector2(0.16f, 0.62f);
                titleMax = new Vector2(0.84f, 0.77f);
                descriptionMin = new Vector2(0.16f, 0.17f);
                descriptionMax = new Vector2(0.84f, 0.61f);
                break;
            case 2: // Green note with push pin
                titleMin = new Vector2(0.18f, 0.58f);
                titleMax = new Vector2(0.82f, 0.72f);
                descriptionMin = new Vector2(0.18f, 0.20f);
                descriptionMax = new Vector2(0.82f, 0.57f);
                break;
            default:
                titleMin = new Vector2(0.16f, 0.66f);
                titleMax = new Vector2(0.84f, 0.82f);
                descriptionMin = new Vector2(0.16f, 0.18f);
                descriptionMax = new Vector2(0.84f, 0.65f);
                break;
        }

        ConfigureText(evidenceNameText, titleMin, titleMax, 11f, 18f, FontStyles.Bold);
        ConfigureText(descriptionText, descriptionMin, descriptionMax, 8f, 13f, FontStyles.Normal);

        if (evidenceImage != null)
        {
            RectTransform imageRect = evidenceImage.rectTransform;
            SetNormalizedRect(
                imageRect,
                new Vector2(0.22f, 0.18f),
                new Vector2(0.78f, 0.62f)
            );
            evidenceImage.preserveAspect = true;
        }
    }

    private static void ConfigureText(
        TMP_Text text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float minimumSize,
        float maximumSize,
        FontStyles style
    )
    {
        if (text == null)
        {
            return;
        }

        SetNormalizedRect(text.rectTransform, anchorMin, anchorMax);
        text.color = new Color32(38, 35, 31, 255);
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = new Vector4(2f, 2f, 2f, 2f);
    }

    private static void SetNormalizedRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax
    )
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    public void OnCardClicked()
    {
        if (selectionManager == null)
        {
            Debug.LogError("EvidenceCardUI has no EvidenceSelectionUI manager.");
            return;
        }

        // If already selected, deselect it
        if (isSelected)
        {
            selectionManager.DeselectEvidence(evidenceData);

            SetSelected(false);
        }

        // Otherwise try selecting it
        else
        {
            bool allowed =
                selectionManager.TrySelectEvidence(evidenceData);

            if (allowed)
            {
                SetSelected(true);
            }
        }
    }

    private void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(selected);
        }
    }
}
