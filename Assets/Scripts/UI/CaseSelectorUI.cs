using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CaseSelectorUI : MonoBehaviour
{
    public CaseData[] cases;
    public TMPro.TMP_Text caseNameText, caseDescriptionText;
    private readonly System.Collections.Generic.List<UnityEngine.UI.Button> caseButtons = new();
    private bool loading;
    private void Update()
    {
        var manager = NetworkManager.main;
        foreach (var button in caseButtons)
            button.interactable = !loading && manager != null && manager.isServer && manager.isLocalPlayerReady;
    }
    private void Start()
    {
        foreach (var oldCanvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) oldCanvas.gameObject.SetActive(false);
        var assets = CourtAssets.Load();
        var canvas = CourtUI.Canvas("Choose a Case", 400);
        CourtUI.Panel(canvas.transform, assets.wood, Vector2.zero, Vector2.one);
        CourtUI.Label(canvas.transform, "Select a case", new Vector2(.1f,.83f), new Vector2(.9f,.97f), 48).font = assets.titleFont;
        cases = assets.cases;
        for (int i = 0; i < cases.Length; i++)
        {
            var data = cases[i];
            float width = .86f / cases.Length;
            var button = CourtUI.Button(canvas.transform, data.caseName + "\n\n" + data.caseDescription,
                new Vector2(.07f + i * width,.15f), new Vector2(.07f + (i + 1) * width - .025f,.78f), () => Choose(data));
            button.interactable = false;
            caseButtons.Add(button);
        }
        CourtUI.Label(canvas.transform, "The display host chooses the case.", new Vector2(.1f,.03f), new Vector2(.9f,.12f), 24);
    }
    private void Choose(CaseData data)
    {
        var manager = NetworkManager.main;
        if (manager == null || !manager.isServer) return;
        loading = true;
        GameSession.Instance.SetCase(data);
        foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)) button.interactable = false;
        manager.sceneModule.LoadSceneAsync("MainGame", LoadSceneMode.Single);
    }
}
