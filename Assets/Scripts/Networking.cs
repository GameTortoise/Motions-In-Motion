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
    private const int DrawingChunkSize = 8 * 1024;
    private const int MaxDrawingSize = 2 * 1024 * 1024;
    private int uploadSerial, receivingSerial, receivedBytes;
    private byte[] receivingDrawing;
    private float receivingDeadline;

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
        if (receivingDrawing != null && Time.unscaledTime > receivingDeadline) receivingDrawing = null;
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
        if (!isOwner || png == null || png.Length == 0 || png.Length > MaxDrawingSize) return false;
        int serial = ++uploadSerial;
        for (int offset = 0; offset < png.Length; offset += DrawingChunkSize)
        {
            var chunk = new byte[System.Math.Min(DrawingChunkSize, png.Length - offset)];
            System.Buffer.BlockCopy(png, offset, chunk, 0, chunk.Length);
            DrawingChunkRpc(serial, png.Length, offset, chunk);
        }
        return true;
    }
    // WebSocket's transport buffer is limited to 64 KiB. Keep individual RPCs
    // below its batching MTU even for noisy, detailed wheel drawings.
    [ServerRpc(channel: Channel.ReliableOrdered, mtuExceeded: MTUBehaviour.Fragment)]
    private void DrawingChunkRpc(int serial, int total, int offset, byte[] chunk)
    {
        if (!isServer || CourtTrial.Instance == null || !owner.HasValue) return;
        if (total <= 0 || total > MaxDrawingSize || offset < 0 || offset >= total ||
            chunk == null || chunk.Length != System.Math.Min(DrawingChunkSize, total - offset)) return;
        if (offset == 0)
        {
            receivingSerial = serial;
            receivedBytes = 0;
            receivingDrawing = new byte[total];
            receivingDeadline = Time.unscaledTime + 30f;
        }
        if (receivingDrawing == null || receivingSerial != serial || receivingDrawing.Length != total ||
            receivedBytes != offset || Time.unscaledTime > receivingDeadline) return;
        System.Buffer.BlockCopy(chunk, 0, receivingDrawing, offset, chunk.Length);
        receivedBytes += chunk.Length;
        if (receivedBytes < total) return;
        var png = receivingDrawing;
        receivingDrawing = null;
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
    private void CancelRpc()
    {
        receivingDrawing = null;
        if (isServer && CourtTrial.Instance != null) CourtTrial.Instance.Release(this);
    }
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
