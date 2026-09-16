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

    private bool serverTypeAssigned;
    private bool localPresentationConfigured;

    protected override void OnSpawned()
    {
        AssignTypeOnServer();
        ConfigureOwnedPlayer();
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        if (asServer)
            AssignTypeOnServer();

        ConfigureOwnedPlayer();
    }

    private void AssignTypeOnServer()
    {
        if (serverTypeAssigned || !isServer || !owner.HasValue)
            return;

        var assignedType = owner.Value == networkManager.localPlayer
            ? PlayerType.Host
            : RandomDrawingType();

        serverTypeAssigned = true;
        AssignTypeRpc(assignedType);
        Debug.Log($"Assigned player {owner.Value} the role {assignedType}.", this);
    }

    private static PlayerType RandomDrawingType()
    {
        return Random.value < 0.5f ? PlayerType.Prosecutor : PlayerType.Defendant;
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void AssignTypeRpc(PlayerType assignedType)
    {
        Type = assignedType;
        HasAssignedType = true;
        ConfigureOwnedPlayer();
    }

    private void ConfigureOwnedPlayer()
    {
        if (localPresentationConfigured || !isSpawned || !isOwner || !HasAssignedType)
            return;

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
        if (!localPresentationConfigured)
            return;

        var paintSession = NetworkWheelPaintSession.instance;
        if (paintSession != null)
            paintSession.ReleaseLocalPlayer(SendDrawingToHost);
        localPresentationConfigured = false;
    }
}
