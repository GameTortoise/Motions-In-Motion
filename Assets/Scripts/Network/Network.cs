using Random = UnityEngine.Random;

public class Network : NetworkBehaviour
{
    public static Network Instance { get; private set; }

    public enum Role
    {
        Judge,
        Prosecutor,
        Defense
    }

    public Role? LocalRole { get; private set; }

    private readonly Dictionary<PlayerID, Role> _playerRoles = new();
    public IReadOnlyDictionary<PlayerID, Role> PlayerRoles => _playerRoles;

    private readonly List<Role> _availableRoles = new()
    {
        Role.Judge,
        Role.Prosecutor,
        Role.Prosecutor,
        Role.Prosecutor,
        Role.Defense,
        Role.Defense,
        Role.Defense,
        Role.Defense
    };

    private readonly Dictionary<string, List<Action<object>>> _handlers = new();

    private void Awake()
    {
        Instance = this;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
        {
            networkManager.onPlayerJoined += HandlePlayerJoined;
            networkManager.onPlayerLeft += HandlePlayerLeft;

            if (localPlayer.HasValue)
            {
                AssignRoleToPlayer(localPlayer.Value);
            }
        }
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);

        if (asServer && networkManager != null)
        {
            networkManager.onPlayerJoined -= HandlePlayerJoined;
            networkManager.onPlayerLeft -= HandlePlayerLeft;
        }
    }

    private void HandlePlayerJoined(PlayerID player, bool asServer)
    {
        if (!asServer)
            return;

        AssignRoleToPlayer(player);
    }

    private void HandlePlayerLeft(PlayerID player, bool asServer)
    {
        if (!asServer)
            return;

        if (_playerRoles.TryGetValue(player, out Role returnedRole))
        {
            _playerRoles.Remove(player);
            _availableRoles.Add(returnedRole);
            SendFromHost("player_left", player);
        }
    }

    private void AssignRoleToPlayer(PlayerID player)
    {
        if (_playerRoles.ContainsKey(player))
            return;

        Role assignedRole;
        if (_availableRoles.Count > 0)
        {
            int index = Random.Range(0, _availableRoles.Count);
            assignedRole = _availableRoles[index];
            _availableRoles.RemoveAt(index);
        }
        else
        {
            assignedRole = Role.Prosecutor;
        }

        _playerRoles[player] = assignedRole;
        SendToClient(player, "role", assignedRole);
    }
    public void On<T>(string eventName, Action<T> callback)
    {
        if (!_handlers.ContainsKey(eventName))
            _handlers[eventName] = new List<Action<object>>();

        _handlers[eventName].Add(obj =>
        {
            if (obj is T typedData)
                callback(typedData);
        });
    }

    public void SendFromHost<T>(string eventName, T data)
    {
        if (!isServer)
            return;

        ReceiveBroadcastRpc(eventName, data);
    }

    public void SendToHost<T>(string eventName, T data)
    {
        if (!isClient)
            return;

        ReceiveOnHostRpc(eventName, data);
    }

    public void SendToClient<T>(PlayerID target, string eventName, T data)
    {
        if (!isServer)
            return;

        ReceiveTargetRpc(target, eventName, data);
    }
    [ObserversRpc(runLocally: false)]
    private void ReceiveBroadcastRpc<T>(string eventName, T data)
    {
        Dispatch(eventName, data);
    }

    [ServerRpc(requireOwnership: false)]
    private void ReceiveOnHostRpc<T>(string eventName, T data)
    {
        Dispatch(eventName, data);
    }

    [TargetRpc]
    private void ReceiveTargetRpc<T>(PlayerID target, string eventName, T data)
    {
        Dispatch(eventName, data);
    }

    private void Dispatch<T>(string eventName, T data)
    {
        if (eventName == "role" && data is Role role)
        {
            LocalRole = role;
        }

        if (_handlers.TryGetValue(eventName, out var listeners))
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                listeners[i].Invoke(data);
            }
        }
    }
}