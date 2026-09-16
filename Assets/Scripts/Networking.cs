using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

[Serializable]
public sealed class WheelDrawingUpload
{
    public uint submissionId;
    public bool defendant;
    public byte[] pngData;
}

[Serializable]
public sealed class WheelDrawingChunk
{
    public uint submissionId;
    public bool defendant;
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
/// Large wheel PNGs are split into small reliable messages so PurrTransport never
/// has to carry a multi-megabyte broadcast as one packet.
/// </summary>
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

    private sealed class IncomingDrawing
    {
        public bool defendant;
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

    private readonly Dictionary<PlayerID, Dictionary<uint, IncomingDrawing>> incomingByPlayer = new();
    private NetworkManager manager;
    private bool serverSubscribed;
    private bool clientSubscribed;
    private float nextCleanupTime;

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

    public bool SendDrawingToHost(uint submissionId, bool defendant, byte[] pngData)
    {
        if (manager == null || !manager.isClient || manager.isServer ||
            submissionId == 0 || pngData == null || pngData.Length == 0 ||
            pngData.Length > MaximumDrawingBytes)
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
                defendant = defendant,
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
            manager.Subscribe<WheelDrawingChunk>(OnDrawingChunkReceived, true);
            serverSubscribed = true;
        }
        else if (!asServer && !clientSubscribed)
        {
            manager.Subscribe<WheelDrawingReceipt>(OnDrawingReceiptReceived, false);
            clientSubscribed = true;
        }
    }

    private void OnNetworkStopped(NetworkManager stoppedManager, bool asServer)
    {
        if (asServer)
            RemoveServerSubscription();
        else
            RemoveClientSubscription();
    }

    private void OnDrawingChunkReceived(PlayerID sender, WheelDrawingChunk chunk, bool asServer)
    {
        if (!asServer || !IsValidChunk(chunk))
            return;

        if (!incomingByPlayer.TryGetValue(sender, out var playerTransfers))
        {
            playerTransfers = new Dictionary<uint, IncomingDrawing>();
            incomingByPlayer.Add(sender, playerTransfers);
        }

        if (!playerTransfers.TryGetValue(chunk.submissionId, out var incoming) ||
            incoming.defendant != chunk.defendant || incoming.totalBytes != chunk.totalBytes ||
            incoming.chunkCount != chunk.chunkCount)
        {
            incoming = new IncomingDrawing
            {
                defendant = chunk.defendant,
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
            defendant = incoming.defendant,
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
        manager.Unsubscribe<WheelDrawingChunk>(OnDrawingChunkReceived, true);
        incomingByPlayer.Clear();
    }

    private void RemoveClientSubscription()
    {
        if (!clientSubscribed || manager == null)
            return;

        clientSubscribed = false;
        manager.Unsubscribe<WheelDrawingReceipt>(OnDrawingReceiptReceived, false);
    }
}
