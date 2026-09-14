using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EvidenceSelectionUI : MonoBehaviour
{
    [Header("Case")]
    public CaseData caseData;

    [Header("Evidence Display")]
    public Transform evidenceContainer;
    public GameObject evidenceCardPrefab;

    [Header("Selection Settings")]
    public int maxSelections = 3;

    [Header("Optional UI")]
    public TMP_Text selectionCountText;

    [Header("Selected Evidence")]
    public List<EvidenceData> selectedEvidence =
        new List<EvidenceData>();

    private void Start()
    {
        DisplayEvidence();
        UpdateSelectionCount();
    }

    private void DisplayEvidence()
    {
        if (caseData == null)
        {
            Debug.LogError("CaseData has not been assigned.");
            return;
        }

        if (evidenceContainer == null)
        {
            Debug.LogError("Evidence Container has not been assigned.");
            return;
        }

        if (evidenceCardPrefab == null)
        {
            Debug.LogError("Evidence Card Prefab has not been assigned.");
            return;
        }

        for (int i = 0; i < caseData.evidence.Count; i++)
        {
            EvidenceData evidence = caseData.evidence[i];

            GameObject newCard = Instantiate(
                evidenceCardPrefab,
                evidenceContainer
            );

            EvidenceCardUI cardUI =
                newCard.GetComponent<EvidenceCardUI>();

            if (cardUI == null)
            {
                Debug.LogError(
                    "EvidenceCard prefab is missing EvidenceCardUI."
                );

                continue;
            }

            cardUI.Setup(
                evidence,
                i + 1,
                this
            );
        }
    }

    public bool TrySelectEvidence(EvidenceData evidence)
    {
        // Do not select the same evidence twice
        if (selectedEvidence.Contains(evidence))
        {
            return false;
        }

        // Stop if player already reached the maximum
        if (selectedEvidence.Count >= maxSelections)
        {
            Debug.Log(
                $"You can only select {maxSelections} evidence pieces."
            );

            return false;
        }

        selectedEvidence.Add(evidence);

        UpdateSelectionCount();

        Debug.Log(
            $"Selected: {evidence.evidenceName}"
        );

        return true;
    }

    public void DeselectEvidence(EvidenceData evidence)
    {
        if (selectedEvidence.Contains(evidence))
        {
            selectedEvidence.Remove(evidence);

            UpdateSelectionCount();

            Debug.Log(
                $"Deselected: {evidence.evidenceName}"
            );
        }
    }

    private void UpdateSelectionCount()
    {
        if (selectionCountText != null)
        {
            selectionCountText.text =
                $"{selectedEvidence.Count} / {maxSelections} selected";
        }
    }

    public void ConfirmSelection()
    {
        Debug.Log(
            $"Confirmed {selectedEvidence.Count} evidence pieces."
        );

        foreach (EvidenceData evidence in selectedEvidence)
        {
            Debug.Log(
                $"Confirmed evidence: {evidence.evidenceName}"
            );
        }
    }
}