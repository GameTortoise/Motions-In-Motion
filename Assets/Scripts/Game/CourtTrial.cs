using System;
using System.Linq;
using PurrNet;
using UnityEngine;

[Serializable]
public struct TrialSnapshot
{
    public int caseIndex, firstEvidence, secondEvidence, turn, speaker, verdict;
    public float elapsed, objectionRemaining;
    public bool started, prosecutionBusy, defenseBusy;
}

// The server is the only writer. Clients receive small snapshots and submit owned RPCs.
[DefaultExecutionOrder(-900)]
public sealed class CourtTrial : MonoBehaviour
{
    public static CourtTrial Instance { get; private set; }
    public CourtAssets Assets { get; private set; }
    public TrialSnapshot State { get; private set; }
    public Networking Local { get; private set; }
    public bool IsHost => NetworkManager.main != null && NetworkManager.main.isServer;
    private sealed class Delivery
    {
        public Networking sender;
        public int evidence;
        public float deadline;
        public CarDrawingWheelInstaller car;
    }
    private readonly Delivery[] deliveries = new Delivery[2];
    private CarDrawingWheelInstaller[] templates;
    private CourtView view;
    private float elapsed, objectionRemaining;
    private int first, second, caseIndex, verdict;
    private bool initialized;
    private readonly int[] speakerOffsets = new int[2];
    private int lastTurn = -1, speaker;
    private float nextSpeakerCheck;
    public CaseData CurrentCase => Assets.cases[Mathf.Clamp(State.caseIndex, 0, Assets.cases.Length - 1)];

    private void Awake()
    {
        Instance = this;
        Assets = CourtAssets.Load();
    }
    private void Start()
    {
        var session = FindAnyObjectByType<NetworkWheelPaintSession>();
        templates = session != null ? new[] { session.RightCar, session.LeftCar } : Array.Empty<CarDrawingWheelInstaller>();
        foreach (var car in templates) if (car != null) car.MakeTemplate();
        foreach (var editor in FindObjectsByType<PaintEditorCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { editor.SetPermanentOpen(false); editor.SetToggleButtonVisible(false); editor.SetOpen(false); }
        view = gameObject.AddComponent<CourtView>();
        view.Build(this);
    }
    public bool Configure(Networking local)
    {
        if (view == null) return false;
        Local = local;
        view.Configure(local.Type);
        var session = FindAnyObjectByType<NetworkWheelPaintSession>();
        if (session != null) session.ConfigureCourt(local.Type, Upload);
        return true;
    }
    private bool Upload(byte[] png)
    {
        if (Local != null)
        {
            Local.SendDrawing(png);
            NetworkWheelPaintSession.instance.SetDrawingOpen(false);
        }
        // Always consume the network drawing: rejected requests must never fall back to local physics.
        return true;
    }
    private void Update()
    {
        if (!IsHost || Assets == null || Assets.cases.Length == 0) return;
        if (!initialized)
        {
            if (FindObjectsByType<Networking>(FindObjectsSortMode.None).Count(p => p.HasAssignedType) < NetworkManager.main.playerCount) return;
            caseIndex = Array.IndexOf(Assets.cases, GameSession.Instance.selectedCase);
            if (caseIndex < 0) return;
            var count = Assets.cases[caseIndex].evidence.Count;
            if (count < 2) return;
            first = UnityEngine.Random.Range(0, count);
            second = (first + UnityEngine.Random.Range(1, count)) % count;
            initialized = true;
        }
        if (objectionRemaining > 0) objectionRemaining = Mathf.Max(0, objectionRemaining - Time.unscaledDeltaTime);
        else elapsed = Mathf.Min(TrialRules.Duration, elapsed + Time.unscaledDeltaTime);
        int turn = TrialRules.Turn(elapsed);
        if (turn != lastTurn || Time.unscaledTime >= nextSpeakerCheck)
        {
            nextSpeakerCheck = Time.unscaledTime + 1f;
            if (turn != lastTurn && lastTurn >= 0) speakerOffsets[lastTurn % 2]++;
            lastTurn = turn;
            var members = FindObjectsByType<Networking>(FindObjectsSortMode.None)
                .Where(p => p.HasAssignedType && p.Type == TrialRules.Team(turn) && p.owner.HasValue)
                .OrderBy(p => p.owner.Value.ToString(), StringComparer.Ordinal).ToArray();
            speaker = members.Length == 0 ? -1 : members[speakerOffsets[turn % 2] % members.Length].owner.Value.GetHashCode();
        }
        for (int i = 0; i < 2; i++)
        {
            var delivery = deliveries[i];
            if (delivery == null) continue;
            if (elapsed >= TrialRules.Duration || delivery.sender == null || Time.unscaledTime > delivery.deadline ||
                (delivery.car != null && (Vector2.Dot(delivery.car.transform.up, Vector2.up) < -0.85f || delivery.car.transform.position.y < -100f)))
                Complete(i, false);
        }
    }
    public TrialSnapshot Snapshot() => new TrialSnapshot
    {
        started = initialized, caseIndex = caseIndex, firstEvidence = first, secondEvidence = second,
        elapsed = elapsed, turn = TrialRules.Turn(elapsed), speaker = speaker, verdict = verdict,
        objectionRemaining = objectionRemaining, prosecutionBusy = deliveries[0] != null, defenseBusy = deliveries[1] != null
    };
    public void Receive(TrialSnapshot state)
    {
        State = state;
        if (view != null) view.Refresh();
    }
    public bool Reserve(Networking sender, int evidence)
    {
        if (!IsHost || !initialized || elapsed >= TrialRules.Duration || sender == null) return false;
        int team = TeamIndex(sender.Type);
        if (team < 0 || deliveries[team] != null) return false;
        var list = Assets.cases[caseIndex].evidence;
        if (evidence != -1 && (evidence >= list.Count || !TrialRules.OwnsEvidence(sender.Type, evidence))) return false;
        deliveries[team] = new Delivery { sender = sender, evidence = evidence, deadline = Time.unscaledTime + 120f };
        return true;
    }
    public bool SubmitWheels(Networking sender, byte[] png)
    {
        int team = TeamIndex(sender.Type);
        if (!IsHost || team < 0 || elapsed >= TrialRules.Duration || png == null || png.Length > 2 * 1024 * 1024 ||
            deliveries[team] == null || deliveries[team].sender != sender || deliveries[team].car != null ||
            !CarDrawingWheelInstaller.TryReadPngSize(png, out int w, out int h) || w > 2048 || h > 2048) return false;
        if (templates.Length <= team || templates[team] == null) { Complete(team, false); return false; }
        var template = templates[team];
        var car = Instantiate(template, template.transform.position, template.transform.rotation, template.transform.parent);
        car.name = sender.Type + " Delivery";
        car.gameObject.SetActive(true);
        if (!car.InstallPngAsWheels(png)) { Destroy(car.gameObject); Complete(team, false); return false; }
        deliveries[team].car = car;
        deliveries[team].deadline = Time.unscaledTime + 120f;
        var marker = car.gameObject.AddComponent<CourtDelivery>();
        marker.team = team;
        foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (camera.Follow == template.transform || (camera.Follow != null && camera.Follow.name == car.name))
                camera.Follow = car.transform;
        }
        return true;
    }
    public void Arrive(int team) { if (IsHost && team >= 0 && team < 2) Complete(team, true); }
    private void Complete(int team, bool arrived)
    {
        var delivery = deliveries[team];
        if (delivery == null) return;
        deliveries[team] = null;
        if (delivery.car != null)
        {
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
                if (camera.Follow == delivery.car.transform) camera.Follow = templates[team].transform;
            Destroy(delivery.car.gameObject);
        }
        if (!arrived || elapsed >= TrialRules.Duration) return;
        if (delivery.evidence == -1)
            objectionRemaining += Assets.objection.length; // Concurrent arrivals queue complete objection songs.
        else if (delivery.evidence != first && delivery.evidence != second)
        { first = second; second = delivery.evidence; }
    }
    public void Release(Networking sender)
    {
        if (!IsHost) return;
        for (int i = 0; i < 2; i++) if (deliveries[i]?.sender == sender) Complete(i, false);
    }
    public void MotionResult(bool accepted) { if (view != null) view.MotionResult(accepted); }
    public void DrawingResult(bool accepted) { if (view != null) view.DrawingResult(accepted); }
    public void Verdict(bool guilty) { if (IsHost && elapsed >= TrialRules.Duration && verdict == 0) verdict = guilty ? 1 : 2; }
    public static int TeamIndex(PlayerType type) => type == PlayerType.Prosecutor ? 0 : type == PlayerType.Defendant ? 1 : -1;
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
