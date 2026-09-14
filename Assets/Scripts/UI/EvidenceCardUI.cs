using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EvidenceCardUI : MonoBehaviour
{
    [Header("Card Content")]
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
        EvidenceSelectionUI manager
    )
    {
        evidenceData = data;
        selectionManager = manager;

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