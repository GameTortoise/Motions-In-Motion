using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

public enum PlayerType : byte
{
    Host = 0,
    Prosecutor = 1,
    Defendant = 2,
    Judge = 3
}

[Serializable]
public sealed class PlayerTypeRequest
{
    public byte protocolVersion = 1;
}

[Serializable]
public sealed class PlayerTypeAssignment
{
    public PlayerType playerType;
}

[Serializable]
public sealed class WheelDrawingUpload
{
    public uint submissionId;
    public PlayerType playerType;
    public byte[] pngData;
}

[Serializable]
public sealed class WheelDrawingChunk
{
    public uint submissionId;
    public int totalBytes;
    public int chunkIndex;
    public int chunkCount;
    public byte[] data;
}

[Serializable]
public sealed class WheelDrawingReceipt
{
    public uint submissionId;
    public bool accepted;
}

/// <summary>
/// Scene-owned bridge between the PurrLobby-created NetworkManager and game systems.
/// The host owns player roles and derives each drawing's target car from the sender's
/// server-side role. Large PNGs are split into reliable transport-sized messages.
/// </summary>
[RegisterNetworkType(typeof(PlayerType))]
[RegisterNetworkType(typeof(PlayerTypeRequest))]
[RegisterNetworkType(typeof(PlayerTypeAssignment))]
[RegisterNetworkType(typeof(WheelDrawingChunk))]
[RegisterNetworkType(typeof(WheelDrawingReceipt))]
[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class Networking : MonoBehaviour
{
    private const int ChunkPayloadBytes = 8 * 1024;
    private const int MaximumDrawingBytes = 2 * 1024 * 1024;
    private const int MaximumChunkCount = MaximumDrawingBytes / ChunkPayloadBytes + 1;
    private const float IncompleteTransferLifetime = 15f;
    private const float RoleRequestInterval = 1f;

    private sealed class IncomingDrawing
    {
        public int totalBytes;
        public int chunkCount;
        public byte[][] chunks;
        public int receivedChunks;
        public int receivedBytes;
        public float lastTouched;
    }

    public static Networking instance { get; private set; }

    public event Action<PlayerID, WheelDrawingUpload> drawingReceivedOnHost;
    public event Action<WheelDrawingReceipt> drawingReceiptReceived;
    public event Action<PlayerType> localPlayerTypeChanged;
    public event Action<PlayerID, PlayerType> playerTypeAssignedOnHost;

    public bool hasLocalPlayerType { get; private set; }
    public PlayerType localPlayerType { get; private set; }

    private readonly Dictionary<PlayerID, Dictionary<uint, IncomingDrawing>> incomingByPlayer = new();
    private readonly Dictionary<PlayerID, PlayerType> playerTypes = new();
    private NetworkManager manager;
    private bool serverSubscribed;
    private bool clientSubscribed;
    private float nextCleanupTime;
    private float nextRoleRequestTime;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("More than one Networking bridge exists in the game scene.", this);
            enabled = false;
            return;
        }

        instance = this;
    }

    private IEnumerator Start()
    {
        while (NetworkManager.main == null)
            yield return null;

        manager = NetworkManager.main;
        manager.RegisterEvents(OnNetworkStarted, OnNetworkStopped);
    }

    private void Update()
    {
        if (clientSubscribed && !hasLocalPlayerType && manager != null && manager.isClient &&
            !manager.isServer && Time.unscaledTime >= nextRoleRequestTime)
        {
            RequestPlayerType();
        }

        if (!serverSubscribed || Time.unscaledTime < nextCleanupTime)
            return;

        nextCleanupTime = Time.unscaledTime + 5f;
        RemoveExpiredTransfers();
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.UnregisterEvents(OnNetworkStarted, OnNetworkStopped);
            RemoveServerSubscription();
            RemoveClientSubscription();
        }

        if (instance == this)
            instance = null;
    }

    public static bool IsDrawingRole(PlayerType playerType)
    {
        return playerType == PlayerType.Prosecutor || playerType == PlayerType.Defendant;
    }

    public bool TryGetPlayerType(PlayerID player, out PlayerType playerType)
    {
        return playerTypes.TryGetValue(player, out playerType);
    }

    public bool SendDrawingToHost(uint submissionId, byte[] pngData)
    {
        if (manager == null || !manager.isClient || manager.isServer || !hasLocalPlayerType ||
            !IsDrawingRole(localPlayerType) || submissionId == 0 || pngData == null ||
            pngData.Length == 0 || pngData.Length > MaximumDrawingBytes)
            return false;

        int chunkCount = (pngData.Length + ChunkPayloadBytes - 1) / ChunkPayloadBytes;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int sourceOffset = chunkIndex * ChunkPayloadBytes;
            int payloadLength = Math.Min(ChunkPayloadBytes, pngData.Length - sourceOffset);
            var payload = new byte[payloadLength];
            Buffer.BlockCopy(pngData, sourceOffset, payload, 0, payloadLength);

            manager.SendToServer(new WheelDrawingChunk
            {
                submissionId = submissionId,
                totalBytes = pngData.Length,
                chunkIndex = chunkIndex,
                chunkCount = chunkCount,
                data = payload
            }, Channel.ReliableOrdered);
        }

        return true;
    }

    public void SendDrawingReceipt(PlayerID player, uint submissionId, bool accepted)
    {
        if (manager == null || !manager.isServer)
            return;

        manager.Send(player, new WheelDrawingReceipt
        {
            submissionId = submissionId,
            accepted = accepted
        }, Channel.ReliableOrdered);
    }

    private void OnNetworkStarted(NetworkManager activeManager, bool asServer)
    {
        manager = activeManager;

        if (asServer && !serverSubscribed)
        {
            manager.Subscribe<PlayerTypeRequest>(OnPlayerTypeRequested, true);
            manager.Subscribe<WheelDrawingChunk>(OnDrawingChunkReceived, true);
            manager.onPlayerJoined += OnPlayerJoined;
            manager.onPlayerLeft += OnPlayerLeft;
            serverSubscribed = true;

            if (manager.isLocalPlayerReady)
            {
                AssignPlayerType(manager.localPlayer, PlayerType.Host);
                SetLocalPlayerType(PlayerType.Host);
            }
        }
        else if (!asServer && !clientSubscribed)
        {
            manager.Subscribe<PlayerTypeAssignment>(OnPlayerTypeAssignmentReceived, false);
            manager.Subscribe<WheelDrawingReceipt>(OnDrawingReceiptReceived, false);
            clientSubscribed = true;

            if (manager.isServer)
                SetLocalPlayerType(PlayerType.Host);
            else
                RequestPlayerType();
        }
    }

    private void OnNetworkStopped(NetworkManager stoppedManager, bool asServer)
    {
        if (asServer)
            RemoveServerSubscription();
        else
            RemoveClientSubscription();
    }

    private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
    {
        if (!asServer || manager == null || !manager.isServer)
            return;

        PlayerType playerType = player == manager.localPlayer
            ? PlayerType.Host
            : ChooseRandomDrawingRole();
        AssignPlayerType(player, playerType);
        SendPlayerType(player, playerTypes[player]);

        if (player == manager.localPlayer)
            SetLocalPlayerType(PlayerType.Host);
    }

    private void OnPlayerLeft(PlayerID player, bool asServer)
    {
        if (!asServer)
            return;

        playerTypes.Remove(player);
        incomingByPlayer.Remove(player);
    }

    private void OnPlayerTypeRequested(PlayerID sender, PlayerTypeRequest request, bool asServer)
    {
        if (!asServer || manager == null || !manager.isServer || request == null)
            return;

        PlayerType playerType = sender == manager.localPlayer
            ? PlayerType.Host
            : ChooseRandomDrawingRole();
        AssignPlayerType(sender, playerType);
        SendPlayerType(sender, playerTypes[sender]);
    }

    private void OnPlayerTypeAssignmentReceived(PlayerID sender, PlayerTypeAssignment assignment, bool asServer)
    {
        if (!asServer && assignment != null)
            SetLocalPlayerType(assignment.playerType);
    }

    private void AssignPlayerType(PlayerID player, PlayerType playerType)
    {
        if (playerTypes.ContainsKey(player))
            return;

        playerTypes.Add(player, playerType);
        playerTypeAssignedOnHost?.Invoke(player, playerType);
        Debug.Log($"Assigned network player {player} the role {playerType}.", this);
    }

    private static PlayerType ChooseRandomDrawingRole()
    {
        return UnityEngine.Random.value < 0.5f ? PlayerType.Prosecutor : PlayerType.Defendant;
    }

    private void SendPlayerType(PlayerID player, PlayerType playerType)
    {
        manager.Send(player, new PlayerTypeAssignment
        {
            playerType = playerType
        }, Channel.ReliableOrdered);
    }

    private void RequestPlayerType()
    {
        if (manager == null || !manager.isClient || manager.isServer)
            return;

        nextRoleRequestTime = Time.unscaledTime + RoleRequestInterval;
        manager.SendToServer(new PlayerTypeRequest(), Channel.ReliableOrdered);
    }

    private void SetLocalPlayerType(PlayerType playerType)
    {
        if (hasLocalPlayerType && localPlayerType == playerType)
            return;

        localPlayerType = playerType;
        hasLocalPlayerType = true;
        localPlayerTypeChanged?.Invoke(playerType);
        Debug.Log($"This player is {playerType}.", this);
    }

    private void OnDrawingChunkReceived(PlayerID sender, WheelDrawingChunk chunk, bool asServer)
    {
        if (!asServer || !IsValidChunk(chunk))
            return;

        if (!playerTypes.TryGetValue(sender, out PlayerType senderType) || !IsDrawingRole(senderType))
        {
            SendDrawingReceipt(sender, chunk.submissionId, false);
            return;
        }

        if (!incomingByPlayer.TryGetValue(sender, out var playerTransfers))
        {
            playerTransfers = new Dictionary<uint, IncomingDrawing>();
            incomingByPlayer.Add(sender, playerTransfers);
        }

        if (!playerTransfers.TryGetValue(chunk.submissionId, out var incoming) ||
            incoming.totalBytes != chunk.totalBytes || incoming.chunkCount != chunk.chunkCount)
        {
            incoming = new IncomingDrawing
            {
                totalBytes = chunk.totalBytes,
                chunkCount = chunk.chunkCount,
                chunks = new byte[chunk.chunkCount][],
                lastTouched = Time.unscaledTime
            };
            playerTransfers[chunk.submissionId] = incoming;
        }

        incoming.lastTouched = Time.unscaledTime;
        if (incoming.chunks[chunk.chunkIndex] != null)
            return;

        incoming.chunks[chunk.chunkIndex] = chunk.data;
        incoming.receivedChunks++;
        incoming.receivedBytes += chunk.data.Length;

        if (incoming.receivedChunks != incoming.chunkCount)
            return;

        playerTransfers.Remove(chunk.submissionId);
        if (incoming.receivedBytes != incoming.totalBytes)
        {
            SendDrawingReceipt(sender, chunk.submissionId, false);
            return;
        }

        var pngData = new byte[incoming.totalBytes];
        int destinationOffset = 0;
        for (int i = 0; i < incoming.chunks.Length; i++)
        {
            var payload = incoming.chunks[i];
            Buffer.BlockCopy(payload, 0, pngData, destinationOffset, payload.Length);
            destinationOffset += payload.Length;
        }

        drawingReceivedOnHost?.Invoke(sender, new WheelDrawingUpload
        {
            submissionId = chunk.submissionId,
            playerType = senderType,
            pngData = pngData
        });
    }

    private void OnDrawingReceiptReceived(PlayerID sender, WheelDrawingReceipt receipt, bool asServer)
    {
        if (!asServer && receipt != null)
            drawingReceiptReceived?.Invoke(receipt);
    }

    private static bool IsValidChunk(WheelDrawingChunk chunk)
    {
        if (chunk == null || chunk.submissionId == 0 || chunk.data == null ||
            chunk.totalBytes <= 0 || chunk.totalBytes > MaximumDrawingBytes ||
            chunk.chunkCount <= 0 || chunk.chunkCount > MaximumChunkCount ||
            chunk.chunkIndex < 0 || chunk.chunkIndex >= chunk.chunkCount ||
            chunk.data.Length <= 0 || chunk.data.Length > ChunkPayloadBytes)
            return false;

        int expectedChunkCount = (chunk.totalBytes + ChunkPayloadBytes - 1) / ChunkPayloadBytes;
        int expectedPayloadLength = chunk.chunkIndex == chunk.chunkCount - 1
            ? chunk.totalBytes - chunk.chunkIndex * ChunkPayloadBytes
            : ChunkPayloadBytes;
        return chunk.chunkCount == expectedChunkCount && chunk.data.Length == expectedPayloadLength;
    }

    private void RemoveExpiredTransfers()
    {
        float cutoff = Time.unscaledTime - IncompleteTransferLifetime;
        var emptyPlayers = new List<PlayerID>();

        foreach (var playerEntry in incomingByPlayer)
        {
            var expiredTransfers = new List<uint>();
            foreach (var transferEntry in playerEntry.Value)
            {
                if (transferEntry.Value.lastTouched < cutoff)
                    expiredTransfers.Add(transferEntry.Key);
            }

            foreach (uint transferId in expiredTransfers)
                playerEntry.Value.Remove(transferId);
            if (playerEntry.Value.Count == 0)
                emptyPlayers.Add(playerEntry.Key);
        }

        foreach (var player in emptyPlayers)
            incomingByPlayer.Remove(player);
    }

    private void RemoveServerSubscription()
    {
        if (!serverSubscribed || manager == null)
            return;

        serverSubscribed = false;
        manager.Unsubscribe<PlayerTypeRequest>(OnPlayerTypeRequested, true);
        manager.Unsubscribe<WheelDrawingChunk>(OnDrawingChunkReceived, true);
        manager.onPlayerJoined -= OnPlayerJoined;
        manager.onPlayerLeft -= OnPlayerLeft;
        incomingByPlayer.Clear();
        playerTypes.Clear();
    }

    private void RemoveClientSubscription()
    {
        if (clientSubscribed && manager != null)
        {
            clientSubscribed = false;
            manager.Unsubscribe<PlayerTypeAssignment>(OnPlayerTypeAssignmentReceived, false);
            manager.Unsubscribe<WheelDrawingReceipt>(OnDrawingReceiptReceived, false);
        }

        hasLocalPlayerType = false;
    }
}
