using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EvidenceSceneLoader : MonoBehaviour
{
    public void LoadEvidenceSelection()
    {
        NetworkManager manager = NetworkManager.main;

        if (manager == null || !manager.isServer)
        {
            Debug.LogWarning("Only the host can change the network scene.");
            return;
        }

        manager.sceneModule.LoadSceneAsync(
            "evidenceSelection",
            LoadSceneMode.Single
        );
    }
}




