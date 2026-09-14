using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EvidenceSelectionUI : MonoBehaviour
{
    public CaseData caseData;
    public Transform evidenceContainer;
    public GameObject evidenceCardPrefab;

    private void Start()
    {
        DisplayEvidence();
    }

    private void DisplayEvidence()
    {
        for (int i = 0; i < caseData.evidence.Count; i++)
        {
            EvidenceData evidence = caseData.evidence[i];

            GameObject card = Instantiate(
                evidenceCardPrefab,
                evidenceContainer
            );

            TMP_Text nameText =
                card.transform.Find("EvidenceName")
                    .GetComponent<TMP_Text>();

            TMP_Text descriptionText =
                card.transform.Find("Description")
                    .GetComponent<TMP_Text>();

            Image image =
                card.transform.Find("EvidenceImage")
                    .GetComponent<Image>();

            nameText.text = $"Evidence {i + 1}";
            descriptionText.text = evidence.description;

            if (evidence.image != null)
            {
                image.sprite = evidence.image;
                image.gameObject.SetActive(true);
            }
            else
            {
                image.gameObject.SetActive(false);
            }
        }
    }
}