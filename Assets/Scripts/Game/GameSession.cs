using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;

    [Header("Current Game")]
    public CaseData selectedCase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void SetCase(CaseData newCase)
    {
        selectedCase = newCase;

        Debug.Log(
            $"Selected case: {selectedCase.caseName}"
        );
    }
}