using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using PurrNet.Lobby;
using System.Collections;

public sealed class CourtPresentation : MonoBehaviour
{
    private static CourtPresentation instance;
    private CourtAssets assets;
    private AudioSource music, interruption;
    private AudioClip desired;
    private Coroutine transition;
    private float themeAt;
    private bool objectionActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var obj = new GameObject("Court Presentation"); DontDestroyOnLoad(obj);
        instance = obj.AddComponent<CourtPresentation>();
    }
    private void Awake()
    {
        assets = CourtAssets.Load();
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false;
        interruption = gameObject.AddComponent<AudioSource>(); interruption.playOnAwake = false;
        SceneManager.sceneLoaded += Loaded;
    }
    private void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            if (GameSession.Instance != null) Destroy(GameSession.Instance.gameObject);
        }
        else if (scene.name == "CaseSelection" || scene.name == "MainGame")
        {
            if (GameSession.Instance == null) new GameObject("GameSession").AddComponent<GameSession>();
            if (scene.name == "MainGame")
            {
                new GameObject("Court Trial").AddComponent<CourtTrial>();
                if (GameOrchestrator.active != null && !string.IsNullOrEmpty(GameOrchestrator.active.menuScene))
                    PurrNet.Lobby.GameSession.EnsureInScene(scene);
            }
        }
        ApplyTheme();
    }
    private void Update()
    {
        if (assets == null) return;
        if (Time.unscaledTime >= themeAt) { themeAt = Time.unscaledTime + .5f; ApplyTheme(); }
        var trial = CourtTrial.Instance;
        var state = trial != null ? (trial.IsHost ? trial.Snapshot() : trial.State) : default;
        music.mute = interruption.mute = trial != null && !trial.IsHost;
        bool objection = trial != null && state.objectionRemaining > 0;
        AudioClip song = assets.opening;
        if (trial != null) song = state.elapsed < 480 ? assets.opening : state.elapsed < 960 ? assets.middle : assets.finale;
        else if (FindAnyObjectByType<LobbyView>() != null || FindAnyObjectByType<LobbyBrowserView>() != null || FindAnyObjectByType<CreateLobbyView>() != null)
            song = assets.lobby;
        if (song != desired)
        {
            desired = song;
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(ChangeSong(song));
        }
        if (objection && !objectionActive)
        {
            music.Pause(); interruption.clip = assets.objection; interruption.loop = true; interruption.Play();
        }
        if (!objection && objectionActive) { interruption.Stop(); music.UnPause(); }
        objectionActive = objection;
    }
    private IEnumerator ChangeSong(AudioClip next)
    {
        float start = music.volume;
        for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
        { music.volume = Mathf.Lerp(start,0,t/.35f); yield return null; }
        music.Stop(); music.clip = next; music.volume = 0; music.Play();
        if (objectionActive) music.Pause();
        for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
        { music.volume = Mathf.Lerp(0,.65f,t/.35f); yield return null; }
        music.volume = .65f; transition = null;
    }
    private void ApplyTheme()
    {
        if (assets == null) return;
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != assets.titleFont) text.font = text.fontSize >= 45f ? assets.titleFont : assets.bodyFont;
            for (var parent = text.transform.parent; parent != null; parent = parent.parent)
            {
                var panel = parent.GetComponent<Image>();
                if (panel != null && panel.sprite == assets.paper)
                { text.color = new Color32(37,29,24,255); break; }
            }
        }
        foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) text.font = assets.legacyFont;
        foreach (var image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.GetComponentInParent<EvidenceCardUI>() != null) continue;
            string name = image.name.ToLowerInvariant();
            bool panel = name.Contains("background") || name.Contains("panel") || name == "bg" || name == "content" || name == "paint editor" || name == "toolbar";
            bool plain = image.sprite == null || image.sprite.name == "Background" || image.sprite.name == "UISprite";
            if (panel && plain)
            { image.sprite = assets.paper; image.color = Color.white; image.type = Image.Type.Simple; }
        }
    }
    private void OnDestroy() { SceneManager.sceneLoaded -= Loaded; if (instance == this) instance = null; }
}
