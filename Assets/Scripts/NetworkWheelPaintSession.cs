using System;
using System.Collections;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

[Serializable]
public sealed class WheelDrawingUpload
{
    public bool defendant;
    public byte[] pngData;
}

[Serializable]
public sealed class WheelDrawingState
{
    public bool defendant;
    public uint revision;
    public byte[] pngData;
}

[RegisterNetworkType(typeof(WheelDrawingUpload))]
[RegisterNetworkType(typeof(WheelDrawingState))]
[DisallowMultipleComponent]
public sealed class NetworkWheelPaintSession : MonoBehaviour
{
    [Header("Team")]
    [SerializeField] private bool defendant = true;

    [Header("Scene references")]
    [SerializeField] private PaintEditorCanvas paintEditorPrefab;
    [SerializeField] private CarDrawingWheelInstaller leftCar;
    [SerializeField] private CarDrawingWheelInstaller rightCar;
    [SerializeField] private GameObject[] regularPresentationObjects;

    [Header("Upload limits")]
    [SerializeField, Min(1024)] private int maximumDrawingBytes = 2 * 1024 * 1024;
    [SerializeField, Min(128)] private int maximumDrawingDimension = 2048;

    private NetworkManager manager;
    private PaintEditorCanvas localEditor;
    private WheelDrawingState latestDefendantDrawing;
    private WheelDrawingState latestOtherDrawing;
    private uint defendantRevision;
    private uint otherRevision;
    private uint appliedDefendantRevision;
    private uint appliedOtherRevision;
    private bool serverSubscribed;
    private bool clientSubscribed;
    private bool localPresentationConfigured;
    private bool instantiatedLocalEditor;
    private Transform localEditorOriginalParent;

    private IEnumerator Start()
    {
        while (NetworkManager.main == null)
            yield return null;

        manager = NetworkManager.main;
        manager.RegisterEvents(OnNetworkStarted, OnNetworkStopped);
    }

    private void OnDestroy()
    {
        if (localEditor != null)
            localEditor.DrawingSubmitted -= SubmitLocalDrawing;

        if (manager != null)
        {
            manager.UnregisterEvents(OnNetworkStarted, OnNetworkStopped);
            RemoveServerSubscriptions();
            RemoveClientSubscriptions();
        }
    }

    private void OnNetworkStarted(NetworkManager activeManager, bool asServer)
    {
        manager = activeManager;

        if (asServer && !serverSubscribed)
        {
            serverSubscribed = true;
            manager.Subscribe<WheelDrawingUpload>(OnDrawingUploaded, true);
            manager.onPlayerLoadedScene += OnPlayerLoadedScene;
        }

        if (!asServer && !clientSubscribed)
        {
            clientSubscribed = true;
            manager.Subscribe<WheelDrawingState>(OnDrawingStateReceived, false);
            ConfigureLocalPresentation();
        }
    }

    private void OnNetworkStopped(NetworkManager stoppedManager, bool asServer)
    {
        if (asServer)
            RemoveServerSubscriptions();
        else
        {
            RemoveClientSubscriptions();
            RestoreRegularPresentation();
        }
    }

    private void ConfigureLocalPresentation()
    {
        if (localPresentationConfigured || manager == null || !manager.isClient)
            return;

        localPresentationConfigured = true;
        var isLobbyHost = manager.isHost;
        SetRegularPresentationVisible(isLobbyHost);

        if (isLobbyHost)
        {
            var hostEditor = FindAnyObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);
            if (hostEditor != null)
            {
                hostEditor.SetPermanentOpen(false);
                hostEditor.SetToggleButtonVisible(false);
                hostEditor.SetOpen(false);
            }
            return;
        }

        localEditor = FindAnyObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);
        if (localEditor == null && paintEditorPrefab != null)
        {
            localEditor = Instantiate(paintEditorPrefab);
            instantiatedLocalEditor = true;
        }

        if (localEditor == null)
        {
            Debug.LogError("Network wheel painting needs a PaintEditorCanvas in the scene or a prefab reference.", this);
            return;
        }

        localEditorOriginalParent = localEditor.transform.parent;
        localEditor.transform.SetParent(null, false);
        localEditor.transform.localScale = Vector3.one;
        localEditor.name = defendant ? "Defendant Wheel Paint Editor" : "Other Team Wheel Paint Editor";
        localEditor.DrawingSubmitted += SubmitLocalDrawing;
        localEditor.SetToggleButtonVisible(false);
        localEditor.SetPermanentOpen(true);
    }

    private bool SubmitLocalDrawing(byte[] pngData)
    {
        if (manager == null || !manager.isClient || manager.isHost || !IsValidDrawing(pngData))
            return false;

        manager.SendToServer(new WheelDrawingUpload
        {
            defendant = defendant,
            pngData = pngData
        }, Channel.ReliableOrdered);
        return true;
    }

    private void OnDrawingUploaded(PlayerID sender, WheelDrawingUpload upload, bool asServer)
    {
        if (!asServer || upload == null || !IsValidDrawing(upload.pngData))
            return;

        var state = new WheelDrawingState
        {
            defendant = upload.defendant,
            revision = upload.defendant ? ++defendantRevision : ++otherRevision,
            pngData = upload.pngData
        };

        if (!ApplyDrawing(state))
            return;

        if (state.defendant)
            latestDefendantDrawing = state;
        else
            latestOtherDrawing = state;

        manager.SendToAll(state, Channel.ReliableOrdered);
    }

    private void OnDrawingStateReceived(PlayerID sender, WheelDrawingState state, bool asServer)
    {
        if (asServer || state == null || !IsValidDrawing(state.pngData))
            return;

        ApplyDrawing(state);
    }

    private bool ApplyDrawing(WheelDrawingState state)
    {
        var appliedRevision = state.defendant ? appliedDefendantRevision : appliedOtherRevision;

        if (state.revision <= appliedRevision)
            return true;

        var targetCar = state.defendant ? leftCar : rightCar;
        if (targetCar == null || !targetCar.InstallPngAsWheels(state.pngData, maximumDrawingDimension))
            return false;

        if (state.defendant)
            appliedDefendantRevision = state.revision;
        else
            appliedOtherRevision = state.revision;
        return true;
    }

    private bool IsValidDrawing(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0 || pngData.Length > maximumDrawingBytes)
            return false;

        return CarDrawingWheelInstaller.TryReadPngSize(pngData, out var width, out var height) &&
               width <= maximumDrawingDimension && height <= maximumDrawingDimension;
    }

    private void OnPlayerLoadedScene(PlayerID player, SceneID scene, bool asServer)
    {
        if (!asServer || manager == null)
            return;

        if (latestDefendantDrawing != null)
            manager.Send(player, latestDefendantDrawing, Channel.ReliableOrdered);
        if (latestOtherDrawing != null)
            manager.Send(player, latestOtherDrawing, Channel.ReliableOrdered);
    }

    private void RemoveServerSubscriptions()
    {
        if (!serverSubscribed || manager == null)
            return;

        serverSubscribed = false;
        manager.Unsubscribe<WheelDrawingUpload>(OnDrawingUploaded, true);
        manager.onPlayerLoadedScene -= OnPlayerLoadedScene;
    }

    private void RemoveClientSubscriptions()
    {
        if (!clientSubscribed || manager == null)
            return;

        clientSubscribed = false;
        manager.Unsubscribe<WheelDrawingState>(OnDrawingStateReceived, false);
    }

    private void RestoreRegularPresentation()
    {
        localPresentationConfigured = false;
        SetRegularPresentationVisible(true);

        if (localEditor == null)
            return;

        localEditor.DrawingSubmitted -= SubmitLocalDrawing;
        localEditor.SetPermanentOpen(false);
        localEditor.SetToggleButtonVisible(false);
        localEditor.SetOpen(false);

        if (instantiatedLocalEditor)
            Destroy(localEditor.gameObject);
        else if (localEditorOriginalParent != null)
            localEditor.transform.SetParent(localEditorOriginalParent, false);

        localEditor = null;
        localEditorOriginalParent = null;
        instantiatedLocalEditor = false;
    }

    private void SetRegularPresentationVisible(bool visible)
    {
        if (regularPresentationObjects == null)
            return;

        foreach (var presentationObject in regularPresentationObjects)
        {
            if (presentationObject != null)
                presentationObject.SetActive(visible);
        }
    }
}
