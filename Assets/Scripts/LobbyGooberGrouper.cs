using System.Collections;
using System.Collections.Generic;
using PurrNet.Lobby;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyGooberGrouper : MonoBehaviour
{
    [SerializeField] private LobbyView _lobbyView;
    [SerializeField] private Sprite[] _goobers;
    [SerializeField, Min(1f)] private float _gooberHeight = 220f;
    [SerializeField, Min(0f)] private float _joinDuration = 0.3f;

    private readonly Dictionary<string, GameObject> _goobersByPlayer = new();
    private ILobby _lobby;
    private Coroutine _bindRoutine;

    private void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindWhenLobbyIsReady());
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        UnbindLobby();
        ClearGoobers();
    }

    private IEnumerator BindWhenLobbyIsReady()
    {
        if (_lobbyView == null)
            _lobbyView = GetComponentInParent<LobbyView>(true);

        while (isActiveAndEnabled && (_lobbyView == null || _lobbyView.lobby == null))
            yield return null;

        _bindRoutine = null;

        if (isActiveAndEnabled && _lobbyView != null)
            BindLobby(_lobbyView.lobby);
    }

    private void BindLobby(ILobby lobby)
    {
        if (ReferenceEquals(_lobby, lobby))
            return;

        UnbindLobby();
        _lobby = lobby;

        if (_lobby == null)
            return;

        _lobby.onPlayerJoined += OnPlayersChanged;
        _lobby.onPlayerLeft += OnPlayersChanged;
        _lobby.onOwnerChanged += OnPlayersChanged;
        _lobby.onLobbyDestroyed += OnLobbyDestroyed;
        SyncGoobers();
    }

    private void UnbindLobby()
    {
        if (_lobby == null)
            return;

        _lobby.onPlayerJoined -= OnPlayersChanged;
        _lobby.onPlayerLeft -= OnPlayersChanged;
        _lobby.onOwnerChanged -= OnPlayersChanged;
        _lobby.onLobbyDestroyed -= OnLobbyDestroyed;
        _lobby = null;
    }

    private void OnPlayersChanged(IPlayer _)
    {
        SyncGoobers();
    }

    private void OnLobbyDestroyed()
    {
        UnbindLobby();
        ClearGoobers();
    }

    private void SyncGoobers()
    {
        if (_lobby == null)
            return;

        var currentPlayerIds = new HashSet<string>();
        foreach (var player in _lobby.players)
        {
            // The first player is the display host. Every additional player gets a goober.
            if (player == null || player.isOwner)
                continue;

            currentPlayerIds.Add(player.id);
            if (!_goobersByPlayer.ContainsKey(player.id))
                AddGoober(player);
        }

        var departedPlayerIds = new List<string>();
        foreach (var pair in _goobersByPlayer)
        {
            if (!currentPlayerIds.Contains(pair.Key))
                departedPlayerIds.Add(pair.Key);
        }

        foreach (var playerId in departedPlayerIds)
            RemoveGoober(playerId);
    }

    private void AddGoober(IPlayer player)
    {
        if (_goobers == null || _goobers.Length == 0)
            return;

        var sprite = _goobers[StableIndex(player.id, _goobers.Length)];
        if (sprite == null)
            return;

        var gooberObject = new GameObject(
            $"Goober - {player.displayName}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement),
            typeof(CanvasGroup));

        var gooberTransform = (RectTransform)gooberObject.transform;
        gooberTransform.SetParent(transform, false);

        var image = gooberObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
        var layout = gooberObject.GetComponent<LayoutElement>();
        layout.preferredHeight = _gooberHeight;
        layout.preferredWidth = _gooberHeight * aspect;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        _goobersByPlayer.Add(player.id, gooberObject);
        StartCoroutine(AnimateJoin(gooberTransform, gooberObject.GetComponent<CanvasGroup>()));
    }

    private IEnumerator AnimateJoin(RectTransform goober, CanvasGroup canvasGroup)
    {
        if (_joinDuration <= 0f)
            yield break;

        goober.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        var elapsed = 0f;

        while (elapsed < _joinDuration && goober != null)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / _joinDuration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            goober.localScale = Vector3.one * eased;
            canvasGroup.alpha = eased;
            yield return null;
        }

        if (goober != null)
        {
            goober.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
        }
    }

    private void RemoveGoober(string playerId)
    {
        if (!_goobersByPlayer.Remove(playerId, out var gooberObject))
            return;

        if (gooberObject != null)
            Destroy(gooberObject);
    }

    private void ClearGoobers()
    {
        foreach (var gooberObject in _goobersByPlayer.Values)
        {
            if (gooberObject != null)
                Destroy(gooberObject);
        }

        _goobersByPlayer.Clear();
    }

    private static int StableIndex(string playerId, int count)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in playerId)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash % (uint)count);
        }
    }
}
