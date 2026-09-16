using UnityEngine;
using TMPro;

public class CaseSelectorUI : MonoBehaviour
{
    [Header("Available Cases")]
    public CaseData[] cases;

    [Header("UI")]
    public TMP_Text caseNameText;
    public TMP_Text caseDescriptionText;

    private CaseData selectedCase;

    private void Start()
    {
        SelectRandomCase();
    }

    private void SelectRandomCase()
    {
        if (cases == null || cases.Length == 0)
        {
            Debug.LogError(
                "CaseSelectorUI: No cases have been assigned!"
            );

            return;
        }

        // Pick a random case
        int randomIndex = Random.Range(0, cases.Length);

        selectedCase = cases[randomIndex];

        // Display case name
        if (caseNameText != null)
        {
            caseNameText.text = selectedCase.caseName;
        }

        // Display case description
        if (caseDescriptionText != null)
        {
            caseDescriptionText.text =
                selectedCase.caseDescription;
        }

        // Save the selected case
        if (GameSession.Instance != null)
        {
            GameSession.Instance.SetCase(selectedCase);
        }
        else
        {
            Debug.LogError(
                "CaseSelectorUI: GameSession does not exist!"
            );
        }

        Debug.Log(
            $"Random case selected: {selectedCase.caseName}"
        );
    }
}