using UnityEngine;
using TMPro;

public class CaseSelectorUI : MonoBehaviour
{
    [Header("Available Cases")]
    public CaseData[] cases;

    [Header("UI")]
    public TMP_Text caseNameText;

    private CaseData selectedCase;

    private void Start()
    {
        SelectRandomCase(); // If anyone sees this have an awesome day :)
    }

    private void SelectRandomCase()
    {
        if (cases == null || cases.Length == 0)
        {
            Debug.LogError(
                "CaseSelectorUI tells us that No cases have been assigned!"
            );

            return;
        }

        // Pick random case
        int randomIndex = Random.Range(
            0,
            cases.Length
        );

        selectedCase = cases[randomIndex];

        // Display its name
        if (caseNameText != null)
        {
            caseNameText.text =
                selectedCase.caseName;
        }

        // Save it
        if (GameSession.Instance != null)
        {
            GameSession.Instance.SetCase(
                selectedCase
            );
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