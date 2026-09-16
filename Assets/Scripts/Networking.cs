using System.Collections;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

public enum PlayerType : byte
{
    Host = 0,
    Prosecutor = 1,
    Defendant = 2,
    Judge = 3
}

/// <summary>
/// One instance is spawned and owned for every connected player. The server assigns
/// its Type once, then the owning client uses this same network object to submit its
/// wheel PNG directly to the server.
/// </summary>
[DisallowMultipleComponent]
public sealed class Networking : NetworkBehaviour
{
    private const int MaximumDrawingBytes = 2 * 1024 * 1024;

    public PlayerType Type { get; private set; } = PlayerType.Host;
    public bool HasAssignedType { get; private set; }

    private PlayerID? serverAssignedOwner;
    private Coroutine serverAssignmentRoutine;
    private bool localPresentationConfigured;
    private bool hasSubmittedEvidence;
    private NetworkEvidenceSession localEvidenceSession;

    protected override void OnSpawned()
    {
        QueueServerTypeAssignment();
        ConfigureOwnedPlayer();
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        if (asServer)
            QueueServerTypeAssignment();

        ConfigureOwnedPlayer();
    }

    private void QueueServerTypeAssignment()
    {
        if (!isServer)
            return;

        if (serverAssignmentRoutine != null)
            StopCoroutine(serverAssignmentRoutine);

        serverAssignmentRoutine = StartCoroutine(AssignTypeAfterOwnershipSettles());
    }

    private IEnumerator AssignTypeAfterOwnershipSettles()
    {
        // PlayerSpawner spawns first and calls GiveOwnership immediately afterward.
        // Waiting one frame prevents that temporary spawn owner from becoming Host.
        yield return null;

        while (isSpawned && isServer && networkManager.isHost && !networkManager.isLocalPlayerReady)
            yield return null;

        serverAssignmentRoutine = null;

        if (!isSpawned || !isServer || !owner.HasValue)
            yield break;

        var settledOwner = owner.Value;
        if (serverAssignedOwner.HasValue && serverAssignedOwner.Value == settledOwner)
            yield break;

        var assignedType = networkManager.isHost && settledOwner == networkManager.localPlayer
            ? PlayerType.Host
            : RandomDrawingType();

        serverAssignedOwner = settledOwner;
        AssignTypeRpc(assignedType);
        Debug.Log($"Assigned settled player {settledOwner} the role {assignedType}.", this);
    }

    private static PlayerType RandomDrawingType()
    {
        return Random.value < 0.5f ? PlayerType.Prosecutor : PlayerType.Defendant;
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void AssignTypeRpc(PlayerType assignedType)
    {
        if (HasAssignedType && Type != assignedType)
            ReleaseLocalPresentation();

        Type = assignedType;
        HasAssignedType = true;
        ConfigureOwnedPlayer();
    }

    private void ConfigureOwnedPlayer()
    {
        if (localPresentationConfigured || !isSpawned || !isOwner || !HasAssignedType)
            return;

        localEvidenceSession = NetworkEvidenceSession.instance;
        if (localEvidenceSession == null)
            localEvidenceSession = FindAnyObjectByType<NetworkEvidenceSession>(FindObjectsInactive.Include);

        if (localEvidenceSession != null)
        {
            localPresentationConfigured = localEvidenceSession.ConfigureLocalPlayer(Type, SendEvidenceToHost);
            if (localPresentationConfigured)
                Debug.Log($"Local player configured for the evidence phase as {Type}.", this);
            return;
        }

        var paintSession = NetworkWheelPaintSession.instance;
        if (paintSession == null)
            paintSession = FindAnyObjectByType<NetworkWheelPaintSession>(FindObjectsInactive.Include);

        if (paintSession == null)
        {
            Debug.LogError("The owned player could not find NetworkWheelPaintSession in MainGame.", this);
            return;
        }

        localPresentationConfigured = paintSession.ConfigureLocalPlayer(Type, SendDrawingToHost);
        if (localPresentationConfigured)
            Debug.Log($"Local player configured as {Type}.", this);
    }

    private bool SendDrawingToHost(byte[] drawingPng)
    {
        if (!isSpawned || !isOwner || isServer || !HasAssignedType ||
            !IsDrawingType(Type) || drawingPng == null || drawingPng.Length == 0 ||
            drawingPng.Length > MaximumDrawingBytes)
            return false;

        SubmitDrawingServerRpc(drawingPng);
        Debug.Log($"Submitted {drawingPng.Length} bytes of {Type} wheel art to the host.", this);
        return true;
    }

    private bool SendEvidenceToHost(string evidenceName, byte[] drawingPng)
    {
        if (!isSpawned || !isOwner || isServer || !HasAssignedType ||
            !IsDrawingType(Type) || hasSubmittedEvidence ||
            string.IsNullOrWhiteSpace(evidenceName) || drawingPng == null ||
            drawingPng.Length == 0 || drawingPng.Length > MaximumDrawingBytes)
            return false;

        SubmitEvidenceServerRpc(evidenceName.Trim(), drawingPng);
        return true;
    }

    [ServerRpc(channel: Channel.ReliableOrdered, mtuExceeded: MTUBehaviour.Fragment)]
    private void SubmitEvidenceServerRpc(string evidenceName, byte[] drawingPng)
    {
        if (!isServer || hasSubmittedEvidence || !HasAssignedType ||
            !IsDrawingType(Type) || string.IsNullOrWhiteSpace(evidenceName) ||
            drawingPng == null || drawingPng.Length == 0 ||
            drawingPng.Length > MaximumDrawingBytes)
            return;

        var evidenceSession = NetworkEvidenceSession.instance;
        if (evidenceSession == null)
            evidenceSession = FindAnyObjectByType<NetworkEvidenceSession>(FindObjectsInactive.Include);

        bool accepted = evidenceSession != null &&
                        evidenceSession.InstallSubmittedEvidence(evidenceName.Trim(), drawingPng);

        if (accepted)
            hasSubmittedEvidence = true;

        if (owner.HasValue)
            EvidenceResultTargetRpc(owner.Value, accepted);
    }

    [TargetRpc]
    private void EvidenceResultTargetRpc(PlayerID target, bool accepted)
    {
        if (localEvidenceSession == null)
            localEvidenceSession = NetworkEvidenceSession.instance;

        if (localEvidenceSession != null)
            localEvidenceSession.ReportSubmissionResult(accepted);
    }

    [ServerRpc(channel: Channel.ReliableOrdered, mtuExceeded: MTUBehaviour.Fragment)]
    private void SubmitDrawingServerRpc(byte[] drawingPng)
    {
        if (!isServer || !HasAssignedType || !IsDrawingType(Type) ||
            drawingPng == null || drawingPng.Length == 0 ||
            drawingPng.Length > MaximumDrawingBytes)
            return;

        var paintSession = NetworkWheelPaintSession.instance;
        if (paintSession == null)
            paintSession = FindAnyObjectByType<NetworkWheelPaintSession>(FindObjectsInactive.Include);

        bool installed = paintSession != null && paintSession.InstallNetworkDrawing(Type, drawingPng);
        if (installed)
            Debug.Log($"Host installed the received {Type} wheel drawing.", this);
        else
            Debug.LogError($"Host could not install the received {Type} wheel drawing.", this);

        if (owner.HasValue)
            DrawingResultTargetRpc(owner.Value, installed);
    }

    [TargetRpc]
    private void DrawingResultTargetRpc(PlayerID target, bool installed)
    {
        if (!installed)
            Debug.LogError("The host received the wheel drawing but could not install it.", this);
    }

    private static bool IsDrawingType(PlayerType playerType)
    {
        return playerType == PlayerType.Prosecutor || playerType == PlayerType.Defendant;
    }

    private void OnDisable()
    {
        if (serverAssignmentRoutine != null)
        {
            StopCoroutine(serverAssignmentRoutine);
            serverAssignmentRoutine = null;
        }

        ReleaseLocalPresentation();
    }

    private void ReleaseLocalPresentation()
    {
        if (!localPresentationConfigured)
            return;

        if (localEvidenceSession != null)
        {
            localEvidenceSession.ReleaseLocalPlayer(SendEvidenceToHost);
            localEvidenceSession = null;
            localPresentationConfigured = false;
            return;
        }

        var paintSession = NetworkWheelPaintSession.instance;
        if (paintSession != null)
            paintSession.ReleaseLocalPlayer(SendDrawingToHost);
        localPresentationConfigured = false;
    }
}
