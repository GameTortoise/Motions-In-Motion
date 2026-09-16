using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
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

    private readonly Dictionary<PlayerID, uint> lastAcceptedSubmission = new();
    private NetworkManager manager;
    private Networking networking;
    private PaintEditorCanvas localEditor;
    private uint nextSubmissionId;
    private uint pendingSubmissionId;
    private byte[] pendingDrawing;
    private float nextSubmissionRetryTime;
    private bool pendingFailureLogged;
    private bool presentationConfigured;
    private bool editorPresentationVisible;
    private bool instantiatedLocalEditor;
    private Transform localEditorOriginalParent;
    private GameObject editorBackgroundCamera;

    private IEnumerator Start()
    {
        while (NetworkManager.main == null || Networking.instance == null)
            yield return null;

        manager = NetworkManager.main;
        networking = Networking.instance;
        networking.drawingReceivedOnHost += OnDrawingReceivedOnHost;
        networking.drawingReceiptReceived += OnDrawingReceiptReceived;
        manager.RegisterEvents(OnNetworkStarted, OnNetworkStopped);
    }

    private void OnDestroy()
    {
        if (localEditor != null)
            localEditor.DrawingSubmitted -= SubmitLocalDrawing;

        if (networking != null)
        {
            networking.drawingReceivedOnHost -= OnDrawingReceivedOnHost;
            networking.drawingReceiptReceived -= OnDrawingReceiptReceived;
        }

        if (manager != null)
            manager.UnregisterEvents(OnNetworkStarted, OnNetworkStopped);
    }

    private void OnNetworkStarted(NetworkManager activeManager, bool asServer)
    {
        manager = activeManager;
        if (asServer)
        {
            lastAcceptedSubmission.Clear();
            return;
        }

        ConfigureLocalPresentation();
    }

    private void OnNetworkStopped(NetworkManager stoppedManager, bool asServer)
    {
        if (asServer)
            lastAcceptedSubmission.Clear();
        else
        {
            pendingDrawing = null;
            RestoreRegularPresentation();
        }
    }

    private void LateUpdate()
    {
        if (manager == null || !manager.isClient)
            return;

        bool shouldShowEditor = !manager.isServer;
        if (!presentationConfigured || editorPresentationVisible != shouldShowEditor)
            ConfigureLocalPresentation();

        if (pendingDrawing != null && !manager.isServer && Time.unscaledTime >= nextSubmissionRetryTime)
            SendPendingDrawing();
    }

    private void ConfigureLocalPresentation()
    {
        if (manager == null || !manager.isClient)
            return;

        // PurrLobby loads MainGame first and starts this process as either a host
        // (server + client) or a joining client. The actual server state is the
        // authoritative distinction; lobby startup flags are not.
        bool showEditor = !manager.isServer;
        presentationConfigured = true;
        editorPresentationVisible = showEditor;
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

        uint submissionId = ++nextSubmissionId;
        if (submissionId == 0)
            submissionId = ++nextSubmissionId;

        if (manager.isServer)
        {
            OnDrawingReceivedOnHost(manager.localPlayer, new WheelDrawingUpload
            {
                submissionId = submissionId,
                defendant = defendant,
                pngData = pngData
            });
        }
        else
        {
            pendingSubmissionId = submissionId;
            pendingDrawing = pngData;
            pendingFailureLogged = false;
            SendPendingDrawing();
        }

        return true;
    }

    private void SendPendingDrawing()
    {
        if (manager == null || !manager.isClient || manager.isServer || pendingDrawing == null)
            return;

        if (networking != null && networking.SendDrawingToHost(pendingSubmissionId, defendant, pendingDrawing))
            nextSubmissionRetryTime = Time.unscaledTime + 2f;
        else
            nextSubmissionRetryTime = Time.unscaledTime + 0.5f;
    }

    private void OnDrawingReceivedOnHost(PlayerID sender, WheelDrawingUpload upload)
    {
        if (manager == null || !manager.isServer || upload == null)
            return;

        if (lastAcceptedSubmission.TryGetValue(sender, out uint acceptedId) && upload.submissionId <= acceptedId)
        {
            networking.SendDrawingReceipt(sender, upload.submissionId, true);
            return;
        }

        if (!IsValidDrawing(upload.pngData))
        {
            networking.SendDrawingReceipt(sender, upload.submissionId, false);
            return;
        }

        var targetCar = upload.defendant ? leftCar : rightCar;
        bool installed = targetCar != null &&
                         targetCar.InstallPngAsWheels(upload.pngData, maximumDrawingDimension);

        if (installed)
        {
            lastAcceptedSubmission[sender] = upload.submissionId;
            Debug.Log($"Installed wheel drawing {upload.submissionId} from player {sender} on the host car.", this);
        }
        else
        {
            Debug.LogError("The host received a complete wheel drawing but could not install it on the target car.", this);
        }

        networking.SendDrawingReceipt(sender, upload.submissionId, installed);
    }

    private void OnDrawingReceiptReceived(WheelDrawingReceipt receipt)
    {
        if (receipt == null || receipt.submissionId != pendingSubmissionId)
            return;

        if (receipt.accepted)
        {
            pendingDrawing = null;
            pendingFailureLogged = false;
            return;
        }

        // A valid drawing can arrive before every host scene object has completed
        // initialization. Keep it pending and retry instead of losing the submission.
        if (!pendingFailureLogged)
        {
            Debug.LogWarning("The host received the wheel drawing but was not ready to install it; retrying.", this);
            pendingFailureLogged = true;
        }
        nextSubmissionRetryTime = Time.unscaledTime + 2f;
    }

    private bool IsValidDrawing(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0 || pngData.Length > maximumDrawingBytes)
            return false;

        return CarDrawingWheelInstaller.TryReadPngSize(pngData, out int width, out int height) &&
               width <= maximumDrawingDimension && height <= maximumDrawingDimension;
    }

    private void RestoreRegularPresentation()
    {
        presentationConfigured = false;
        editorPresentationVisible = false;
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
