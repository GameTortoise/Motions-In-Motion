using System.Collections;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

public enum PlayerType : byte { Host, Prosecutor, Defendant, Judge }

[DisallowMultipleComponent]
public sealed class Networking : NetworkBehaviour
{
    public PlayerType Type { get; private set; }
    public bool HasAssignedType { get; private set; }
    private bool configured;
    private float nextSnapshot;

    protected override void OnSpawned() { if (isServer) StartCoroutine(Assign()); }
    private IEnumerator Assign()
    {
        yield return null;
        while (isSpawned && (!owner.HasValue || !networkManager.isLocalPlayerReady)) yield return null;
        if (!isSpawned) yield break;
        var role = owner.Value == networkManager.localPlayer ? PlayerType.Host
            : GameSession.Instance.AssignRole(owner.Value.ToString());
        AssignTypeRpc(role);
    }
    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void AssignTypeRpc(PlayerType role) { Type = role; HasAssignedType = true; }

    private void Update()
    {
        if (!isSpawned || !HasAssignedType) return;
        var trial = CourtTrial.Instance;
        if (isOwner && !configured && trial != null && trial.State.started) configured = trial.Configure(this);
        if (isServer && Type == PlayerType.Host && trial != null && Time.unscaledTime >= nextSnapshot)
        {
            nextSnapshot = Time.unscaledTime + 0.2f;
            StateRpc(trial.Snapshot());
        }
    }
    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void StateRpc(TrialSnapshot snapshot)
    {
        if (CourtTrial.Instance != null) CourtTrial.Instance.Receive(snapshot);
    }
    public void RequestMotion(int evidence)
    {
        if (isOwner && HasAssignedType) MotionRpc(evidence);
    }
    [ServerRpc]
    private void MotionRpc(int evidence)
    {
        if (!isServer || !HasAssignedType || CourtTrial.Instance == null || !owner.HasValue) return;
        bool accepted = CourtTrial.Instance.Reserve(this, evidence);
        MotionResultRpc(owner.Value, accepted);
    }
    [TargetRpc]
    private void MotionResultRpc(PlayerID target, bool accepted)
    {
        if (CourtTrial.Instance != null) CourtTrial.Instance.MotionResult(accepted);
    }
    public bool SendDrawing(byte[] png)
    {
        if (!isOwner || png == null || png.Length == 0 || png.Length > 2 * 1024 * 1024) return false;
        DrawingRpc(png);
        return true;
    }
    [ServerRpc(channel: Channel.ReliableOrdered, mtuExceeded: MTUBehaviour.Fragment)]
    private void DrawingRpc(byte[] png)
    {
        if (!isServer || CourtTrial.Instance == null || !owner.HasValue) return;
        bool accepted = CourtTrial.Instance.SubmitWheels(this, png);
        DrawingResultRpc(owner.Value, accepted);
    }
    [TargetRpc]
    private void DrawingResultRpc(PlayerID target, bool accepted)
    {
        if (CourtTrial.Instance != null) CourtTrial.Instance.DrawingResult(accepted);
    }
    public void CancelMotion() { if (isOwner) CancelRpc(); }
    [ServerRpc]
    private void CancelRpc() { if (isServer && CourtTrial.Instance != null) CourtTrial.Instance.Release(this); }
    public void Verdict(bool guilty) { if (isOwner) VerdictRpc(guilty); }
    [ServerRpc]
    private void VerdictRpc(bool guilty)
    {
        if (isServer && Type == PlayerType.Judge && CourtTrial.Instance != null) CourtTrial.Instance.Verdict(guilty);
    }
    private void OnDisable()
    {
        if (isServer && CourtTrial.Instance != null) CourtTrial.Instance.Release(this);
    }
}
