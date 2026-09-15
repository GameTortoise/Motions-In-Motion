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
    private bool playerEventsSubscribed;
    private bool instantiatedLocalEditor;
    private Transform localEditorOriginalParent;
    private GameObject editorBackgroundCamera;

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
            RemovePlayerEventSubscriptions();
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
            defendantRevision = 0;
            otherRevision = 0;
            latestDefendantDrawing = null;
            latestOtherDrawing = null;
            manager.Subscribe<WheelDrawingUpload>(OnDrawingUploaded, true);
            manager.onPlayerLoadedScene += OnPlayerLoadedScene;
            serverSubscribed = true;
        }

        if (!asServer && !clientSubscribed)
        {
            appliedDefendantRevision = 0;
            appliedOtherRevision = 0;
            manager.Subscribe<WheelDrawingState>(OnDrawingStateReceived, false);
            clientSubscribed = true;
            AddPlayerEventSubscriptions();
            ConfigureLocalPresentation();
        }
    }

    private void OnNetworkStopped(NetworkManager stoppedManager, bool asServer)
    {
        if (asServer)
            RemoveServerSubscriptions();
        else
        {
            RemovePlayerEventSubscriptions();
            RemoveClientSubscriptions();
            RestoreRegularPresentation();
        }
    }

    private void ConfigureLocalPresentation()
    {
        if (manager == null || !manager.isClient)
            return;

        // Planned-host is available before both sides finish connecting, preventing
        // a one-frame editor/main-screen role swap during lobby startup.
        var isLobbyHost = manager.isHost || manager.isPlannedHost;
        var showEditor = !isLobbyHost;
        SetRegularPresentationVisible(!showEditor);

        if (!showEditor)
        {
            HideLocalEditor();
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

        if (localEditorOriginalParent == null && localEditor.transform.parent != null)
            localEditorOriginalParent = localEditor.transform.parent;
        if (localEditor.transform.parent != null)
            localEditor.transform.SetParent(null, false);

        var editorRect = (RectTransform)localEditor.transform;
        editorRect.anchorMin = Vector2.zero;
        editorRect.anchorMax = Vector2.one;
        editorRect.anchoredPosition = Vector2.zero;
        editorRect.sizeDelta = Vector2.zero;
        editorRect.pivot = new Vector2(0.5f, 0.5f);
        localEditor.transform.localScale = Vector3.one;
        localEditor.name = defendant ? "Defendant Wheel Paint Editor" : "Other Team Wheel Paint Editor";
        localEditor.DrawingSubmitted -= SubmitLocalDrawing;
        localEditor.DrawingSubmitted += SubmitLocalDrawing;
        localEditor.SetToggleButtonVisible(false);
        localEditor.SetPermanentOpen(true);
        EnsureEditorBackgroundCamera();
    }

    private bool SubmitLocalDrawing(byte[] pngData)
    {
        if (manager == null || !manager.isClient || !IsValidDrawing(pngData))
            return false;

        var upload = new WheelDrawingUpload
        {
            defendant = defendant,
            pngData = pngData
        };

        if (manager.isHost)
            OnDrawingUploaded(manager.localPlayer, upload, true);
        else
            manager.SendToServer(upload, Channel.ReliableOrdered);
        return true;
    }

    private void AddPlayerEventSubscriptions()
    {
        if (playerEventsSubscribed || manager == null)
            return;

        playerEventsSubscribed = true;
        manager.onPlayerJoined += OnPlayerCountChanged;
        manager.onPlayerLeft += OnPlayerCountChanged;
    }

    private void RemovePlayerEventSubscriptions()
    {
        if (!playerEventsSubscribed || manager == null)
            return;

        playerEventsSubscribed = false;
        manager.onPlayerJoined -= OnPlayerCountChanged;
        manager.onPlayerLeft -= OnPlayerCountChanged;
    }

    private void OnPlayerCountChanged(PlayerID player, bool isReconnect, bool asServer)
    {
        if (!asServer)
            ConfigureLocalPresentation();
    }

    private void OnPlayerCountChanged(PlayerID player, bool asServer)
    {
        if (!asServer)
            ConfigureLocalPresentation();
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

        if (state.defendant)
            latestDefendantDrawing = state;
        else
            latestOtherDrawing = state;

        manager.SendToAll(state, Channel.ReliableOrdered);

        // A host runs both server and client in one process. Apply explicitly here so
        // its rendered cars update even if the transport does not loop broadcasts
        // back through the local client connection.
        if ((manager.isHost || manager.isPlannedHost) && !ApplyDrawing(state))
            Debug.LogError("The host received a wheel drawing but could not install it on the target car.", this);
    }

    private void OnDrawingStateReceived(PlayerID sender, WheelDrawingState state, bool asServer)
    {
        if (asServer || state == null || !IsValidDrawing(state.pngData))
            return;

        if (!ApplyDrawing(state))
            Debug.LogError("A network wheel drawing could not be installed on the target car.", this);
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
        SetRegularPresentationVisible(true);

        HideLocalEditor();
    }

    private void HideLocalEditor()
    {
        if (localEditor == null)
            localEditor = FindAnyObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);

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

        if (editorBackgroundCamera != null)
        {
            Destroy(editorBackgroundCamera);
            editorBackgroundCamera = null;
        }
    }

    private void EnsureEditorBackgroundCamera()
    {
        if (editorBackgroundCamera != null)
            return;

        editorBackgroundCamera = new GameObject("Paint Editor Background Camera");
        var backgroundCamera = editorBackgroundCamera.AddComponent<Camera>();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = new Color32(24, 27, 34, 255);
        backgroundCamera.cullingMask = 0;
        backgroundCamera.depth = -100f;
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
