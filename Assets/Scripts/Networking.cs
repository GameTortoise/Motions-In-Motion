using PurrNet;
using UnityEngine;

/// <summary>
/// Lives on the PlayerSpawner prefab. The locally-owned non-host player owns the
/// paint editor, while its matching server-side component receives the drawing.
/// </summary>
[DisallowMultipleComponent]
public sealed class Networking : NetworkBehaviour
{
    [SerializeField] private bool defendant = true;

    private PaintEditorCanvas paintEditor;

    protected override void OnSpawned()
    {
        ConfigureLocalRole();
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        // PlayerSpawner assigns ownership immediately after Spawn(), so OnSpawned
        // can legitimately run before this component reports isOwner.
        ConfigureLocalRole();
    }

    private void ConfigureLocalRole()
    {
        if (!isSpawned || !isOwner || paintEditor != null)
            return;

        paintEditor = FindFirstObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);
        if (paintEditor == null)
        {
            Debug.LogError("Networking could not find PaintEditorCanvas in MainGame.", this);
            return;
        }

        // The lobby owner is also the game server and keeps the normal presentation.
        // Every other locally-owned player gets the editor permanently opened.
        if (isServer)
        {
            paintEditor.SetStartOpen(false);
            Debug.Log("Wheel paint networking ready: lobby host keeps the MainGame view.", this);
            return;
        }

        paintEditor.DrawingSubmitted += SendDrawingToHost;
        paintEditor.SetStartOpen(true);
        Debug.Log("Wheel paint networking ready: client paint editor opened.", this);
    }

    private void OnDisable()
    {
        if (paintEditor != null)
            paintEditor.DrawingSubmitted -= SendDrawingToHost;
    }

    private bool SendDrawingToHost(byte[] drawingPng)
    {
        if (!isSpawned || !isOwner || isServer || drawingPng == null || drawingPng.Length == 0)
            return false;

        SubmitDrawingServerRpc(defendant, drawingPng);
        Debug.Log($"Submitted {drawingPng.Length} bytes of wheel drawing to the host.", this);
        return true;
    }

    [ServerRpc(channel: Channel.ReliableOrdered, mtuExceeded: MTUBehaviour.Fragment)]
    private void SubmitDrawingServerRpc(bool isDefendant, byte[] drawingPng)
    {
        if (!isServer || drawingPng == null || drawingPng.Length == 0)
            return;

        var editor = FindFirstObjectByType<PaintEditorCanvas>(FindObjectsInactive.Include);
        var installers = FindObjectsByType<CarDrawingWheelInstaller>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (editor == null || installers.Length == 0)
        {
            Debug.LogError("Host could not find the paint editor or a car wheel installer.", this);
            return;
        }

        // MainGame places the defendant car on the left and the other car on the right.
        CarDrawingWheelInstaller target = installers[0];
        for (var i = 1; i < installers.Length; i++)
        {
            var candidate = installers[i];
            if (isDefendant
                    ? candidate.transform.position.x < target.transform.position.x
                    : candidate.transform.position.x > target.transform.position.x)
                target = candidate;
        }

        target.SelectForNextDrawing();
        var worldDrawing = editor.CreateWorldDrawingFromPng(drawingPng);
        if (worldDrawing == null)
        {
            Debug.LogError("Host could not decode the submitted wheel drawing.", this);
            return;
        }

        // MainGame's car claims this object here and turns it into its next wheel.
        CarDrawingWheelInstaller.TryInstallDrawing(worldDrawing);
        editor.SetOpen(false);
        Debug.Log($"Host installed the received drawing on {(isDefendant ? "the left" : "the right")} car.", this);
    }
}
