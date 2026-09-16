using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class NetworkWheelPaintSession : MonoBehaviour
{
    [SerializeField] private PaintEditorCanvas paintEditorPrefab;
    [SerializeField] private CarDrawingWheelInstaller leftCar, rightCar;
    [SerializeField] private GameObject[] regularPresentationObjects;
    public static NetworkWheelPaintSession instance { get; private set; }
    public CarDrawingWheelInstaller LeftCar => leftCar;
    public CarDrawingWheelInstaller RightCar => rightCar;
    private PaintEditorCanvas editor;
    private Func<byte[], bool> handler;
    private void Awake() { instance = this; }
    public void ConfigureCourt(PlayerType role, Func<byte[], bool> submit)
    {
        bool host = role == PlayerType.Host;
        foreach (var item in regularPresentationObjects) if (item != null) item.SetActive(host);
        if (host) return;
        var cameraObject = new GameObject("Controller Background Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(74, 47, 30, 255);
        camera.cullingMask = 0;
        if (FindObjectsByType<AudioListener>().Length == 0) cameraObject.AddComponent<AudioListener>();
        if (role == PlayerType.Judge) return;
        editor = Instantiate(paintEditorPrefab);
        editor.transform.SetParent(null, false);
        editor.SetToggleButtonVisible(false);
        editor.SetPermanentOpen(false);
        editor.SetOpen(false);
        handler = submit;
        editor.DrawingSubmitted += handler;
    }
    public void SetDrawingOpen(bool open)
    {
        if (editor == null) return;
        editor.SetPermanentOpen(false);
        editor.SetOpen(open);
    }
    private void OnDestroy()
    {
        if (editor != null && handler != null) editor.DrawingSubmitted -= handler;
        if (instance == this) instance = null;
    }
}
