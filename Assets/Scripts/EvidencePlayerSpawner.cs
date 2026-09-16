using PurrNet;

/// <summary>
/// PlayerSpawner variant that remains quiet when the evidence scene is opened
/// directly in the editor without an active network session.
/// </summary>
public sealed class EvidencePlayerSpawner : PlayerSpawner
{
    private bool registered;

    public override void OnEnable()
    {
        if (NetworkManager.main == null)
            return;

        registered = true;
        base.OnEnable();
    }

    public override void OnDisable()
    {
        if (!registered || NetworkManager.main == null)
            return;

        base.OnDisable();
        registered = false;
    }
}
