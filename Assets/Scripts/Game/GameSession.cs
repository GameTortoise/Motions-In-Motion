using System.Collections.Generic;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;
    public CaseData selectedCase;
    private readonly Dictionary<string, PlayerType> roles = new();
    public PlayerType AssignRole(string owner)
    {
        if (!roles.TryGetValue(owner, out var role))
        {
            role = TrialRules.Role(roles.Count);
            roles.Add(owner, role);
        }
        return role;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void SetCase(CaseData data) { selectedCase = data; }
}
