using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EvidenceSelectionUI : MonoBehaviour
{
    [Header("Case")]
    public CaseData caseData;

    [Header("Evidence Display")]
    public Transform evidenceContainer;
    public GameObject evidenceCardPrefab;

    [Header("Sticky Notes")]
    [Tooltip("Sticky note backgrounds are distributed evenly, then shuffled for each screen.")]
    public Sprite[] stickyNoteSprites;

    [Tooltip("Random uniform scale range used for each sticky note.")]
    public Vector2 stickyNoteScaleRange = new Vector2(0.86f, 1.08f);

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

        ConfigureEvidenceGrid(caseData.evidence.Count);

        List<int> randomizedNoteStyles = BuildRandomizedNoteStyles(
            caseData.evidence.Count,
            stickyNoteSprites != null && stickyNoteSprites.Length > 0
                ? stickyNoteSprites.Length
                : 4
        );

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

            int noteStyleIndex = randomizedNoteStyles[i];
            Sprite stickyNoteSprite = null;
            if (stickyNoteSprites != null && stickyNoteSprites.Length > 0)
            {
                stickyNoteSprite = stickyNoteSprites[noteStyleIndex % stickyNoteSprites.Length];
            }

            float minimumScale = Mathf.Min(stickyNoteScaleRange.x, stickyNoteScaleRange.y);
            float maximumScale = Mathf.Max(stickyNoteScaleRange.x, stickyNoteScaleRange.y);
            float uniformScale = Random.Range(minimumScale, maximumScale);

            cardUI.Setup(
                evidence,
                i + 1,
                this,
                stickyNoteSprite,
                uniformScale,
                noteStyleIndex
            );
        }
    }

    private List<int> BuildRandomizedNoteStyles(int itemCount, int styleCount)
    {
        styleCount = Mathf.Max(1, styleCount);
        List<int> styles = new List<int>(itemCount);

        // Fill a balanced bag first so every design is used before any repeats.
        for (int i = 0; i < itemCount; i++)
        {
            styles.Add(i % styleCount);
        }

        for (int i = styles.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (styles[i], styles[swapIndex]) = (styles[swapIndex], styles[i]);
        }

        return styles;
    }

    private void ConfigureEvidenceGrid(int itemCount)
    {
        RectTransform gridRect = evidenceContainer as RectTransform;
        GridLayoutGroup grid = evidenceContainer.GetComponent<GridLayoutGroup>();

        if (gridRect == null || grid == null || itemCount <= 0)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, grid.constraintCount)
            : Mathf.CeilToInt(Mathf.Sqrt(itemCount));
        int rows = Mathf.CeilToInt((float)itemCount / columns);

        float maximumScale = Mathf.Clamp(
            Mathf.Max(stickyNoteScaleRange.x, stickyNoteScaleRange.y),
            0.8f,
            1.1f
        );
        float availableWidth = gridRect.rect.width;
        float availableHeight = gridRect.rect.height;
        float maximumVisualSize = Mathf.Min(
            availableWidth / columns,
            availableHeight / rows
        );
        float cellSize = Mathf.Max(
            1f,
            Mathf.Floor(maximumVisualSize / maximumScale) - 1f
        );

        // Scaling happens around the card center. Reserve half of the largest
        // possible overhang on every outside edge of the resized grid.
        int edgePadding = Mathf.CeilToInt(
            Mathf.Max(0f, cellSize * (maximumScale - 1f) * 0.5f)
        );
        grid.padding.left = edgePadding;
        grid.padding.right = edgePadding;
        grid.padding.top = edgePadding;
        grid.padding.bottom = edgePadding;

        float horizontalSpace = availableWidth - (edgePadding * 2f) - (cellSize * columns);
        float verticalSpace = availableHeight - (edgePadding * 2f) - (cellSize * rows);

        grid.cellSize = Vector2.one * cellSize;
        grid.spacing = new Vector2(
            columns > 1 ? Mathf.Max(0f, horizontalSpace / (columns - 1)) : 0f,
            rows > 1 ? Mathf.Max(0f, verticalSpace / (rows - 1)) : 0f
        );
        grid.childAlignment = TextAnchor.MiddleCenter;
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
