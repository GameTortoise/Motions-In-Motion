using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class NetworkEvidenceSession : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private EvidenceSelectionUI hostBoard;
    [SerializeField] private PaintEditorCanvas paintEditorPrefab;

    [Header("Submission limits")]
    [SerializeField, Min(1)] private int maximumGeneratedEvidence = 2;
    [SerializeField, Min(1024)] private int maximumDrawingBytes = 2 * 1024 * 1024;
    [SerializeField, Min(128)] private int maximumDrawingDimension = 2048;
    [SerializeField, Range(1, 120)] private int maximumTitleLength = 80;

    public static NetworkEvidenceSession instance { get; private set; }

    private readonly List<Texture2D> generatedTextures = new();
    private readonly List<Sprite> generatedSprites = new();
    private Canvas hostCanvas;
    private GraphicRaycaster hostRaycaster;
    private PaintEditorCanvas localEditor;
    private Func<string, byte[], bool> localSubmitHandler;
    private InputField titleInput;
    private Text statusText;
    private Button submitButton;
    private int receivedEvidenceCount;
    private bool submissionPending;
    private bool submissionAccepted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("More than one NetworkEvidenceSession exists in the evidence scene.", this);
            enabled = false;
            return;
        }

        instance = this;
        if (hostBoard == null)
            hostBoard = FindAnyObjectByType<EvidenceSelectionUI>(FindObjectsInactive.Include);

        if (hostBoard != null && hostBoard.evidenceContainer != null)
        {
            hostCanvas = hostBoard.evidenceContainer.GetComponentInParent<Canvas>(true);
            if (hostCanvas != null)
                hostRaycaster = hostCanvas.GetComponent<GraphicRaycaster>();
        }

        SetHostPresentationVisible(false);
    }

    private void Start()
    {
        // Allow artists to preview this scene directly without starting a network match.
        if (PurrNet.NetworkManager.main == null && hostBoard != null)
        {
            SetHostPresentationVisible(true);
            hostBoard.InitializeHostBoard();
        }
    }

    private void OnDestroy()
    {
        if (localEditor != null)
            localEditor.DrawingSubmitted -= OnDrawingSubmitted;

        foreach (Sprite sprite in generatedSprites)
            if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in generatedTextures)
            if (texture != null) Destroy(texture);

        if (instance == this)
            instance = null;
    }

    public bool ConfigureLocalPlayer(PlayerType playerType, Func<string, byte[], bool> submitHandler)
    {
        if (playerType == PlayerType.Host)
        {
            SetHostPresentationVisible(true);
            if (hostBoard == null)
            {
                Debug.LogError("The evidence scene has no EvidenceSelectionUI host board.", this);
                return false;
            }

            hostBoard.InitializeHostBoard();
            return true;
        }

        if (playerType != PlayerType.Prosecutor && playerType != PlayerType.Defendant)
        {
            Debug.LogWarning($"No evidence editor is configured for {playerType}.", this);
            return false;
        }

        if (submitHandler == null || paintEditorPrefab == null)
        {
            Debug.LogError("The evidence session needs its paint editor prefab assigned.", this);
            return false;
        }

        SetHostPresentationVisible(false);
        localSubmitHandler = submitHandler;

        if (localEditor == null)
        {
            localEditor = Instantiate(paintEditorPrefab);
            localEditor.name = $"{playerType} Evidence Editor";
            localEditor.transform.SetParent(null, false);
            BuildEvidenceControls();
        }

        localEditor.DrawingSubmitted -= OnDrawingSubmitted;
        localEditor.DrawingSubmitted += OnDrawingSubmitted;
        localEditor.SetToggleButtonVisible(false);
        localEditor.SetPermanentOpen(true);
        return true;
    }

    public void ReleaseLocalPlayer(Func<string, byte[], bool> submitHandler)
    {
        if (localSubmitHandler != submitHandler)
            return;

        if (localEditor != null)
            localEditor.DrawingSubmitted -= OnDrawingSubmitted;
        localSubmitHandler = null;
    }

    public bool InstallSubmittedEvidence(string evidenceName, byte[] pngData)
    {
        if (receivedEvidenceCount >= maximumGeneratedEvidence || hostBoard == null ||
            string.IsNullOrWhiteSpace(evidenceName) || pngData == null ||
            pngData.Length == 0 || pngData.Length > maximumDrawingBytes)
            return false;

        if (!CarDrawingWheelInstaller.TryReadPngSize(pngData, out int width, out int height) ||
            width > maximumDrawingDimension || height > maximumDrawingDimension)
            return false;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "Submitted Evidence Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        if (!texture.LoadImage(pngData, false))
        {
            Destroy(texture);
            return false;
        }

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Submitted Evidence Drawing";

        string safeName = evidenceName.Trim();
        if (safeName.Length > maximumTitleLength)
            safeName = safeName.Substring(0, maximumTitleLength);

        if (!hostBoard.AddGeneratedEvidence(safeName, sprite))
        {
            Destroy(sprite);
            Destroy(texture);
            return false;
        }

        generatedTextures.Add(texture);
        generatedSprites.Add(sprite);
        receivedEvidenceCount++;
        Debug.Log($"Added submitted evidence {receivedEvidenceCount}/{maximumGeneratedEvidence}: {safeName}", this);
        return true;
    }

    public void ReportSubmissionResult(bool accepted)
    {
        submissionPending = false;
        submissionAccepted = accepted;
        if (statusText != null)
            statusText.text = accepted ? "Evidence added to the host board." : "The host could not accept this evidence.";
        if (titleInput != null)
            titleInput.interactable = !accepted;
        if (submitButton != null)
            submitButton.interactable = !accepted;
    }

    private bool OnDrawingSubmitted(byte[] pngData)
    {
        if (submissionPending || submissionAccepted || localSubmitHandler == null || titleInput == null)
            return false;

        string evidenceName = titleInput.text.Trim();
        if (string.IsNullOrWhiteSpace(evidenceName))
        {
            if (statusText != null) statusText.text = "Enter an evidence title first.";
            return false;
        }

        if (!localSubmitHandler(evidenceName, pngData))
        {
            if (statusText != null) statusText.text = "Evidence could not be sent.";
            return false;
        }

        submissionPending = true;
        if (statusText != null) statusText.text = "Sending evidence to the host...";
        if (submitButton != null) submitButton.interactable = false;
        return true;
    }

    private void SetHostPresentationVisible(bool visible)
    {
        if (hostCanvas != null) hostCanvas.enabled = visible;
        if (hostRaycaster != null) hostRaycaster.enabled = visible;
    }

    private void BuildEvidenceControls()
    {
        Transform toolbar = FindDescendant(localEditor.transform, "Toolbar");
        if (toolbar == null)
        {
            Debug.LogError("The evidence paint editor has no Toolbar object.", localEditor);
            return;
        }

        Transform oldSubmit = toolbar.Find("Submit Wheel");
        if (oldSubmit != null)
        {
            oldSubmit.name = "Submit Evidence to Board";
            Text oldSubmitLabel = oldSubmit.GetComponentInChildren<Text>();
            if (oldSubmitLabel != null) oldSubmitLabel.text = "Submit Evidence";
            submitButton = oldSubmit.GetComponent<Button>();
        }

        titleInput = CreateTitleInput(toolbar);
        titleInput.transform.SetSiblingIndex(1);
        statusText = CreateToolbarText("Draw, name, and submit one evidence piece.", toolbar, 220f);
        statusText.alignment = TextAnchor.MiddleLeft;
    }

    private static InputField CreateTitleInput(Transform parent)
    {
        var root = new GameObject("Evidence Title", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(LayoutElement), typeof(InputField));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredWidth = 260f;
        root.GetComponent<Image>().color = Color.white;

        Text text = CreateToolbarText(string.Empty, root.transform, 0f);
        text.color = new Color32(30, 32, 38, 255);
        text.alignment = TextAnchor.MiddleLeft;
        text.rectTransform.offsetMin = new Vector2(10f, 2f);
        text.rectTransform.offsetMax = new Vector2(-10f, -2f);

        Text placeholder = CreateToolbarText("Evidence title...", root.transform, 0f);
        placeholder.color = new Color32(110, 112, 120, 255);
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.rectTransform.offsetMin = new Vector2(10f, 2f);
        placeholder.rectTransform.offsetMax = new Vector2(-10f, -2f);

        InputField input = root.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 80;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private static Text CreateToolbarText(string value, Transform parent, float preferredWidth)
    {
        var root = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        root.transform.SetParent(parent, false);
        Text text = root.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        if (preferredWidth > 0f)
            root.AddComponent<LayoutElement>().preferredWidth = preferredWidth;
        return text;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform child in root)
        {
            if (child.name == objectName) return child;
            Transform result = FindDescendant(child, objectName);
            if (result != null) return result;
        }
        return null;
    }
}
