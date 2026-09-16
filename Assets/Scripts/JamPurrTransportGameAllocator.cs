using System;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Lobby;
using PurrNet.Transports;
using UnityEngine;

/// <summary>
/// MainMenu game allocator. Each start gets a fresh PurrTransport room so a
/// previous match cannot prevent the new authoritative host from listening.
/// </summary>
[CreateAssetMenu(menuName = "Game Jam/PurrTransport Game Allocator")]
public sealed class JamPurrTransportGameAllocator : GameAllocatorProvider
{
    [SerializeField, PurrScene] private string gameScene;
    [SerializeField] private bool useP2P = true;

    public override Task<GameStartResponse> AllocateGame(ILobby lobby)
    {
        return Task.FromResult(GameStartResponse.Success(new ConnectionInfo
        {
            // LobbyView distributes this one response to every member. Generating
            // it once here keeps everyone together while avoiding stale-room reuse.
            serverAddress = "PURRLOBBY_GAME_" + lobby.id + "_" + Guid.NewGuid().ToString("N"),
            hostId = lobby.owner?.id
        }));
    }

    public override Task LoadGame(ILobby lobby)
    {
        return LoadGameScene(gameScene);
    }

    protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
    {
        var transport = manager.transport as PurrTransport
                        ?? GetOrAddComponent<PurrTransport>(manager.gameObject);
        manager.transport = transport;
        transport.roomName = connection.serverAddress;
        transport.useNat = useP2P;
        return true;
    }
}
