using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class NetworkWheelPaintSession : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private PaintEditorCanvas paintEditorPrefab;
    [SerializeField] private CarDrawingWheelInstaller leftCar;
    [SerializeField] private CarDrawingWheelInstaller rightCar;
    [SerializeField] private GameObject[] regularPresentationObjects;

    [Header("Upload limits")]
    [SerializeField, Min(1024)] private int maximumDrawingBytes = 2 * 1024 * 1024;
    [SerializeField, Min(128)] private int maximumDrawingDimension = 2048;

    public static NetworkWheelPaintSession instance { get; private set; }

    private PaintEditorCanvas localEditor;
    private Func<byte[], bool> localSubmitHandler;
    private bool instantiatedLocalEditor;
    private Transform localEditorOriginalParent;
    private GameObject editorBackgroundCamera;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("More than one NetworkWheelPaintSession exists in MainGame.", this);
            enabled = false;
            return;
        }

        instance = this;

        // Keep the normal scene fully visible until an owned network player has
        // actually received its role. This avoids an empty Game view while joining.
        SetRegularPresentationVisible(true);
    }

    private void OnDestroy()
    {
        if (localEditor != null && localSubmitHandler != null)
            localEditor.DrawingSubmitted -= localSubmitHandler;

        if (editorBackgroundCamera != null)
            Destroy(editorBackgroundCamera);

        if (instance == this)
            instance = null;
    }

    public bool ConfigureLocalPlayer(PlayerType playerType, Func<byte[], bool> submitHandler)
    {
        if (playerType == PlayerType.Host)
        {
            SetRegularPresentationVisible(true);
            HideLocalEditor();
            return true;
        }

        if (playerType != PlayerType.Prosecutor && playerType != PlayerType.Defendant)
        {
            Debug.LogWarning($"No local presentation is configured yet for {playerType}.", this);
            return false;
        }

        if (submitHandler == null)
            return false;

        localEditor = FindAnyObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);
        if (localEditor == null && paintEditorPrefab != null)
        {
            localEditor = Instantiate(paintEditorPrefab);
            instantiatedLocalEditor = true;
        }

        if (localEditor == null)
        {
            Debug.LogError("MainGame needs a PaintEditorCanvas instance or prefab reference.", this);
            return false;
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
        editorRect.localScale = Vector3.one;

        if (localSubmitHandler != null)
            localEditor.DrawingSubmitted -= localSubmitHandler;
        localSubmitHandler = submitHandler;
        localEditor.DrawingSubmitted -= localSubmitHandler;
        localEditor.DrawingSubmitted += localSubmitHandler;

        localEditor.name = $"{playerType} Wheel Paint Editor";
        localEditor.SetToggleButtonVisible(false);
        localEditor.SetPermanentOpen(true);

        // Create a real camera before disabling the host presentation. Unity's Game
        // view therefore always has an active camera behind the overlay canvas.
        EnsureEditorBackgroundCamera();
        SetRegularPresentationVisible(false);
        return true;
    }

    public void ReleaseLocalPlayer(Func<byte[], bool> submitHandler)
    {
        if (localEditor != null && submitHandler != null)
            localEditor.DrawingSubmitted -= submitHandler;
        if (localSubmitHandler == submitHandler)
            localSubmitHandler = null;
    }

    public bool InstallNetworkDrawing(PlayerType playerType, byte[] pngData)
    {
        if ((playerType != PlayerType.Prosecutor && playerType != PlayerType.Defendant) ||
            !IsValidDrawing(pngData))
            return false;

        // Defendants contribute to the left car; prosecutors to the right car.
        var targetCar = playerType == PlayerType.Defendant ? leftCar : rightCar;
        return targetCar != null && targetCar.InstallPngAsWheels(pngData, maximumDrawingDimension);
    }

    private bool IsValidDrawing(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0 || pngData.Length > maximumDrawingBytes)
            return false;

        return CarDrawingWheelInstaller.TryReadPngSize(pngData, out int width, out int height) &&
               width <= maximumDrawingDimension && height <= maximumDrawingDimension;
    }

    private void HideLocalEditor()
    {
        if (localEditor == null)
            localEditor = FindAnyObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);

        if (localEditor != null)
        {
            if (localSubmitHandler != null)
                localEditor.DrawingSubmitted -= localSubmitHandler;
            localSubmitHandler = null;
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
