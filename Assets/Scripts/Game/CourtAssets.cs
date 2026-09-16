using UnityEngine;
using TMPro;

public sealed class CourtAssets : ScriptableObject
{
    public CaseData[] cases;
    public Sprite paper, wood, evidenceBackground;
    public Sprite[] goobers;
    public TMP_FontAsset bodyFont, titleFont;
    public Font legacyFont;
    public AudioClip opening, middle, finale, objection, lobby;
    public GameObject evidenceCanvas;
    public GameObject evidenceCard;
    public GameObject playerPrefab;
    public static CourtAssets Load() => Resources.Load<CourtAssets>("CourtAssets");
}
