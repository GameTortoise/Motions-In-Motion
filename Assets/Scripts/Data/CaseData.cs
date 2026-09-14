using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCase", menuName = "Court Game/Case")]
public class CaseData : ScriptableObject
{
    public string caseName;

    [TextArea(3, 10)]
    public string caseDescription;

    public string caseAnswer;

    public List<EvidenceData> evidence;
}

[Serializable]
public class EvidenceData
{
    public string evidenceName;

    [TextArea(2, 8)]
    public string description;

    public Sprite image;
}