using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public sealed class CourtView : MonoBehaviour
{
    private CourtTrial trial;
    private CourtAssets assets;
    private Canvas canvas, cancelCanvas;
    private TMP_Text status, role, objection;
    private Image goober, controllerBackground;
    private RectTransform display;
    private GameObject selection;
    private Button motion, objectButton, cancel, guilty, innocent;
    private bool pending, drawing;
    private float drawingGrace;
    private int shownCase = -1, shownFirst = -1, shownSecond = -1, shownTurn = -1;
    private PlayerType localRole;

    public void Build(CourtTrial owner)
    {
        trial = owner; assets = trial.Assets;
        canvas = CourtUI.Canvas("Courtroom", 400);
        controllerBackground = CourtUI.Panel(canvas.transform, assets.paper, Vector2.zero, Vector2.one);
        controllerBackground.raycastTarget = false;
        CourtUI.Panel(canvas.transform, assets.paper, new Vector2(.14f,.76f), new Vector2(.86f,.99f)).raycastTarget = false;
        status = CourtUI.Label(canvas.transform, "Waiting for the court...", new Vector2(.15f,.86f), new Vector2(.85f,.99f), 32);
        role = CourtUI.Label(canvas.transform, "", new Vector2(.15f,.76f), new Vector2(.85f,.86f), 22);
        display = CourtUI.Rect("Evidence in Court", canvas.transform, new Vector2(.28f,.36f), new Vector2(.72f,.76f));
        var grid = display.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        grid.cellSize = new Vector2(260,260); grid.spacing = new Vector2(14,0); grid.childAlignment = TextAnchor.MiddleCenter;
        objection = CourtUI.Label(canvas.transform, "Objection", new Vector2(.2f,.4f), new Vector2(.8f,.72f), 70);
        objection.font = assets.titleFont; objection.gameObject.SetActive(false);
        goober = CourtUI.Panel(canvas.transform, assets.goobers[UnityEngine.Random.Range(0, assets.goobers.Length)], new Vector2(.78f,.38f), new Vector2(.98f,.75f));
        goober.preserveAspect = true; goober.raycastTarget = false;
        goober.transform.DOScale(1.045f, .65f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(goober.gameObject);
        motion = CourtUI.Button(canvas.transform, "Motion for Evidence", new Vector2(.1f,.12f), new Vector2(.48f,.24f), () => { if (!pending) selection.SetActive(true); });
        objectButton = CourtUI.Button(canvas.transform, "add objection", new Vector2(.52f,.12f), new Vector2(.9f,.24f), () => Request(-1));
        cancelCanvas = CourtUI.Canvas("Delivery Cancel", 700);
        cancel = CourtUI.Button(cancelCanvas.transform, "Cancel delivery", new Vector2(.72f,.01f), new Vector2(.98f,.09f), () => { trial.Local.CancelMotion(); EndDrawing(); });
        guilty = CourtUI.Button(canvas.transform, "Guilty", new Vector2(.1f,.12f), new Vector2(.48f,.24f), () => trial.Local.Verdict(true));
        innocent = CourtUI.Button(canvas.transform, "Not guilty", new Vector2(.52f,.12f), new Vector2(.9f,.24f), () => trial.Local.Verdict(false));
        Configure(PlayerType.Host);
    }
    public void Configure(PlayerType type)
    {
        localRole = type;
        controllerBackground.gameObject.SetActive(type != PlayerType.Host);
        bool lawyer = CourtTrial.TeamIndex(type) >= 0;
        motion.gameObject.SetActive(lawyer); objectButton.gameObject.SetActive(lawyer);
        cancel.gameObject.SetActive(false); guilty.gameObject.SetActive(false); innocent.gameObject.SetActive(false);
        goober.gameObject.SetActive(type == PlayerType.Host);
        display.gameObject.SetActive(type == PlayerType.Host);
        if (lawyer) BuildSelection();
    }
    private void BuildSelection()
    {
        if (selection != null) Destroy(selection);
        selection = Instantiate(assets.evidenceCanvas);
        selection.name = "Motion Evidence Selection";
        var selectionCanvas = selection.GetComponent<Canvas>();
        selectionCanvas.sortingOrder = 600;
        var bg = CourtUI.Panel(selection.transform, assets.evidenceBackground, Vector2.zero, Vector2.one);
        bg.transform.SetAsFirstSibling();
        var board = selection.GetComponentInChildren<EvidenceSelectionUI>(true);
        if (board != null) board.enabled = false;
        Transform grid = board != null ? board.evidenceContainer : null;
        if (grid == null) grid = CourtUI.Rect("EvidenceGrid", selection.transform, new Vector2(.05f,.08f), new Vector2(.95f,.85f));
        var gridRect = (RectTransform)grid;
        gridRect.anchorMin = new Vector2(.06f,.08f); gridRect.anchorMax = new Vector2(.94f,.84f);
        gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;
        foreach (Transform child in grid) Destroy(child.gameObject);
        var layout = grid.GetComponent<GridLayoutGroup>() ?? grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 3;
        layout.cellSize = new Vector2(220,220); layout.spacing = new Vector2(16,16); layout.childAlignment = TextAnchor.MiddleCenter;
        var evidence = trial.CurrentCase.evidence;
        for (int i = 0; i < evidence.Count; i++)
        {
            if (!TrialRules.OwnsEvidence(localRole, i)) continue;
            int index = i;
            var card = Instantiate(assets.evidenceCard, grid);
            var ui = card.GetComponent<EvidenceCardUI>(); ui.Setup(evidence[i], i + 1, null, null, 1f, i % 4);
            var button = card.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); button.interactable = true;
            button.onClick.AddListener(() => Request(index));
        }
        foreach (var label in selection.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name == "SelectionCountText" || label.name == "gameName") label.gameObject.SetActive(false);
            if (label.name == "evidenceSelectionText") label.text = "Motion for Evidence";
        }
        CourtUI.Button(selection.transform, "X", new Vector2(.91f,.88f), new Vector2(.98f,.98f), () => selection.SetActive(false));
        selection.SetActive(false);
        shownCase = trial.State.caseIndex;
    }
    private void Request(int evidence)
    {
        if (pending || trial.Local == null) return;
        pending = true; motion.interactable = objectButton.interactable = false;
        selection.SetActive(false); trial.Local.RequestMotion(evidence);
    }
    public void MotionResult(bool accepted)
    {
        pending = false;
        if (accepted)
        {
            drawing = true; drawingGrace = Time.unscaledTime + 1f;
            NetworkWheelPaintSession.instance.SetDrawingOpen(true);
            cancel.gameObject.SetActive(true);
        }
        else role.text = "Your team already has a delivery. Try again when it arrives.";
    }
    public void DrawingResult(bool accepted)
    {

        if (accepted) EndDrawing();
        else
        {
            NetworkWheelPaintSession.instance.SetDrawingOpen(true);
            role.text = "Drawing could not be accepted. Draw again, or cancel the delivery.";
        }
    }
    private void EndDrawing()
    {
        drawing = false; pending = false;
        cancel.gameObject.SetActive(false);
        NetworkWheelPaintSession.instance.SetDrawingOpen(false);
    }
    public void Refresh()
    {
        var state = trial.State;
        if (!state.started) { status.text = "Waiting for all controllers..."; return; }
        bool ended = state.elapsed >= TrialRules.Duration;
        bool busy = localRole == PlayerType.Prosecutor ? state.prosecutionBusy : state.defenseBusy;
        motion.interactable = objectButton.interactable = !ended && !busy && !pending;
        if ((busy || ended) && selection != null) selection.SetActive(false);
        // A reservation can expire or be cancelled while its drawer is still open.
        if (drawing && !busy && !pending && Time.unscaledTime > drawingGrace) EndDrawing();
        if (CourtTrial.TeamIndex(localRole) >= 0 && shownCase != state.caseIndex) BuildSelection();
        string team = TrialRules.Team(state.turn) == PlayerType.Prosecutor ? "Prosecution" : "Defense";
        int total = Mathf.CeilToInt(TrialRules.Duration - state.elapsed);
        int turn = Mathf.CeilToInt(TrialRules.TurnEnd(state.turn) - state.elapsed);
        status.text = ended ? (state.verdict == 0 ? "Trial complete - Judge, deliver your verdict" : state.verdict == 1 ? "Verdict: Guilty" : "Verdict: Not guilty")
            : $"{team} speaking  {turn / 60:00}:{turn % 60:00}\nTrial remaining  {total / 60:00}:{total % 60:00}";
        role.text = localRole == PlayerType.Host ? $"Called to speak: Lawyer {state.speaker}\n{trial.CurrentCase.caseName}"
            : localRole == PlayerType.Judge ? "You are the Judge. Listen to both sides."
            : $"You are {localRole} - Lawyer {trial.Local?.owner?.GetHashCode()}" +
              (trial.Local != null && trial.Local.owner.HasValue && trial.Local.owner.Value.GetHashCode() == state.speaker ? "\nYou are called to speak!" : "\nListen to the current speaker.");
        objection.gameObject.SetActive(state.objectionRemaining > 0);
        if (state.objectionRemaining > 0) objection.transform.SetAsLastSibling();
        guilty.gameObject.SetActive(localRole == PlayerType.Judge && ended && state.verdict == 0);
        innocent.gameObject.SetActive(localRole == PlayerType.Judge && ended && state.verdict == 0);
        if (localRole == PlayerType.Host && (shownFirst != state.firstEvidence || shownSecond != state.secondEvidence))
        {
            shownFirst = state.firstEvidence; shownSecond = state.secondEvidence;
            foreach (Transform child in display) Destroy(child.gameObject);
            foreach (int index in new[] { shownFirst, shownSecond })
            {
                if (index < 0 || index >= trial.CurrentCase.evidence.Count) continue;
                var card = Instantiate(assets.evidenceCard, display);
                card.GetComponent<EvidenceCardUI>().Setup(trial.CurrentCase.evidence[index], index + 1, null, null, 1f, index % 4);
                card.GetComponent<EvidenceCardUI>().SetInteractable(false);
            }
        }
        if (shownTurn != state.turn)
        {
            shownTurn = state.turn;
            bool defense = TrialRules.Team(state.turn) == PlayerType.Defendant;
            goober.rectTransform.anchorMin = new Vector2(defense ? .02f : .78f,.38f);
            goober.rectTransform.anchorMax = new Vector2(defense ? .22f : .98f,.75f);
        }
    }
    private void OnDestroy()
    {
        if (selection != null) Destroy(selection);
        if (canvas != null) Destroy(canvas.gameObject);
        if (cancelCanvas != null) Destroy(cancelCanvas.gameObject);
    }
}

public static class CourtUI
{
    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    public static Canvas Canvas(string name, int order)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f; return canvas;
    }
    public static Image Panel(Transform parent, Sprite sprite, Vector2 min, Vector2 max)
    {
        var image = Rect("Panel",parent,min,max).gameObject.AddComponent<Image>(); image.sprite = sprite; return image;
    }
    public static TMP_Text Label(Transform parent, string value, Vector2 min, Vector2 max, float size)
    {
        var text = Rect("Label", parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = CourtAssets.Load().bodyFont; text.text = value; text.fontSize = size;
        text.color = new Color32(37,29,24,255); text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size; text.raycastTarget = false;
        return text;
    }
    public static Button Button(Transform parent, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
    {
        var image = Panel(parent, CourtAssets.Load().paper, min, max); image.name = title;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(click);
        Label(image.transform, title, new Vector2(.04f,.04f), new Vector2(.96f,.96f), 28); return button;
    }
}
