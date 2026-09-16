using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PurrNet.UI;
using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyView : MonoView
    {
        public const string LOBBY_STATUS_STRING = "LOBBY_STATUS_STRING";
        public const string LOBBY_STATUS_DETAILS_STRING = "LOBBY_STATUS_DETAILS_STRING";
        public const string LOBBY_CONN_INFO = GameStartKeys.ConnInfo;

        [SerializeField] private RectTransform _content;
        [SerializeField] private PlayerEntry _playerPrefab;
        [SerializeField] private GameObject _playerPlaceholderPrefab;
        [SerializeField] private RectTransform _playerContent;
        [SerializeField] private LobbyChat _chat;
        [SerializeField] private TMP_InputField _lobbyCode;
        [Space]
        [SerializeField] private ColorInfo _readyColor = new() { enabled = true, color = ColorType.Surface };
        [SerializeField] private ColorTone _readyColorTone;
        [SerializeField] private ColorInfo _readyHover = new() { enabled = true, color = ColorType.Surface };
        [SerializeField] private ColorTone _readyHoverTone;
        [SerializeField] private ColorInfo _unreadyColor = new() { enabled = true, color = ColorType.Warning };
        [SerializeField] private ColorTone _unreadyColorTone;
        [SerializeField] private ColorInfo _unreadyHover = new() { enabled = true, color = ColorType.Warning };
        [SerializeField] private ColorTone _unreadyHoverTone;
        [SerializeField] private ColorInfo _readyTextColor = new() { enabled = true, color = ColorType.Surface, contrast = true };
        [SerializeField] private ColorInfo _unreadyTextColor = new() { enabled = true, color = ColorType.Background, contrast = true };
        [SerializeField] private TMP_Text _readyButtonText;
        [SerializeField] private ButtonElement _readyButton;
        [Space]
        [SerializeField] private ColorInfo _unmutedColor = new() { enabled = true, color = ColorType.Surface };
        [SerializeField] private ColorTone _unmutedColorTone;
        [SerializeField] private ColorInfo _unmutedHover = new() { enabled = true, color = ColorType.Surface };
        [SerializeField] private ColorTone _unmutedHoverTone;
        [SerializeField] private ColorInfo _mutedColor = new() { enabled = true, color = ColorType.Danger };
        [SerializeField] private ColorTone _mutedColorTone;
        [SerializeField] private ColorInfo _mutedHover = new() { enabled = true, color = ColorType.Danger };
        [SerializeField] private ColorTone _mutedHoverTone;
        [SerializeField] private ButtonElement _microphoneButton;
        [SerializeField] private TMP_Text _microphoneText;
        [SerializeField] private GameObject _microphoneFeature;
        [Space] [SerializeField, Min(1)] private int _minimumPlayersToStart = 3;
        [SerializeField] private TMP_Text _lobbyStatus;
        [SerializeField] private TMP_Text _lobbyStatusDetails;
        [Space]
        [SerializeField] private bool _connectToPurrnetInLobby = true;
        [SerializeField] private LobbyConnectionProvider _lobbyConnection;

        private ILobby _lobby;
        private bool _lobbyConnected;
        private bool _hasLocalMicEnabled;
        private bool _gameStarted;
        private bool _closingLobby;
        private string _localPlayerId;

        private UIPool<Transform> _playerPlaceholderPool;

        private GameOrchestrator _orchestrator;
        public ILobby lobby => _lobby;

#if PURR_VOICE
        public bool localMicEnabled => _hasLocalMicEnabled;
        public event Action<bool> onLocalMicEnabledChanged;
#endif

        public void Setup(ILobby lobby, GameOrchestrator orchestrator)
        {
            UnsubscribeLobbyEvents();
            DisconnectFromLobby();
            ClearPlayerEntries();

            _orchestrator = orchestrator;
            _readyStateInitialized = false;
            _lobbyEventsUnsubscribed = false;
            _gameStarted = false;
            _closingLobby = false;
            _localPlayerId = lobby?.localPlayer?.id;
            ResetStatusLabels();

            _playerPlaceholderPool ??= new UIPool<Transform>(_playerPlaceholderPrefab.transform, _playerContent);

            _lobby = lobby;
            _orchestrator.activeLobby = lobby;

            RenderPlayerList(lobby);

            // Destruction is terminal and replays on subscription. Observe it before
            // replaying player joins, which can otherwise start a transient connection
            // to a lobby that was already destroyed while this view was loading.
            _lobby.onLobbyDestroyed += OnLobbyDestroyed;
            if (_closingLobby)
                return;

            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
            _lobby.onPlayerUpdated += OnPlayerUpdated;
            _lobby.onOwnerChanged += OnOwnerChanged;

            if (_lobby.lobbyData.TryGetData(LOBBY_STATUS_STRING, out var lobbyStatus))
                OnMetadata(LOBBY_STATUS_STRING, lobbyStatus);
            if (_lobby.lobbyData.TryGetData(LOBBY_STATUS_DETAILS_STRING, out var lobbyStatusDetails))
                OnMetadata(LOBBY_STATUS_DETAILS_STRING, lobbyStatusDetails);
            if (_lobby.lobbyData.TryGetData(LOBBY_CONN_INFO, out var lobbyConnInfo))
                OnMetadata(LOBBY_CONN_INFO, lobbyConnInfo);

            _lobby.lobbyData.onDataChanged += OnMetadata;

            _chat.Setup(lobby);
            _lobbyCode.text = lobby.joinCode;

            _microphoneFeature.SetActive(false);
            ConnectToLobby(lobby);
            UpdateMicGraphics();
        }

        private void OnMetadata(string key, string value)
        {
            if (_lobby.isOwner)
                return;

            switch (key)
            {
                case LOBBY_STATUS_STRING:
                {
                    _lobbyStatus.text = value;
                    break;
                }
                case LOBBY_STATUS_DETAILS_STRING:
                {
                    _lobbyStatusDetails.text = value;
                    break;
                }
                case LOBBY_CONN_INFO:
                {
                    JoinGame(value);
                    break;
                }
            }
        }

        private void ResetStatusLabels()
        {
            _lobbyStatus.SetText("WAITING FOR PLAYERS");
            _lobbyStatusDetails.SetText("READY UP!");
        }

#if PURR_VOICE
        public void EnableMicrophoneFeature()
        {
            _microphoneFeature.SetActive(true);
        }
#endif

        private bool CanHostStartGame()
        {
            //HERE!!!
            return true;
            
            if (_lobby?.localPlayer == null || !_lobby.localPlayer.isOwner || _gameStarted)
                return false;

            int participantCount = 0;
            bool allParticipantsReady = true;
            foreach (var player in _lobby.players)
            {
                if (player.isOwner)
                    continue;

                participantCount++;
                if (!player.isReady)
                    allParticipantsReady = false;
            }

            // The lobby owner is the TV/display authority and does not count as a player.
            return participantCount != 4 && participantCount >= _minimumPlayersToStart && allParticipantsReady;
        }

        private void StartGame()
        {
            LaunchGame(async loadingView =>
            {
                var joinInfo = await _orchestrator.gameAllocator.AllocateGame(lobby);
                if (!this || _lobby == null)
                    return;

                if (!joinInfo.success)
                    throw new Exception(joinInfo.error);

                GameStartKeys.PublishConnectionInfo(_lobby, joinInfo.connection);

                loadingView.Setup("Loading game ...");
                var orchestrator = _orchestrator;
                await orchestrator.gameAllocator.LoadGame(lobby);
                orchestrator.gameAllocator.Connect(joinInfo.connection, true);
            }).Forget("[LobbyView] StartGame failed");
        }

        private void JoinGame(string rawConnectionInfo)
        {
            LaunchGame(async loadingView =>
            {
                if (!GameStartKeys.TryReadConnectionInfo(rawConnectionInfo, out var info))
                    throw new Exception("Received malformed connection info from the lobby.");

                loadingView.Setup("Loading game ...");
                var orchestrator = _orchestrator;
                await orchestrator.gameAllocator.LoadGame(lobby);
                orchestrator.gameAllocator.Connect(info, false);
            }).Forget("[LobbyView] JoinGame failed");
        }

        /// <summary>Runs a launch flow behind a LoadingView; on failure resets the ready/countdown state.</summary>
        private async Task LaunchGame(Func<LoadingView, Task> flow)
        {
            LoadingView loadingView = null;

            try
            {
                loadingView = parentStack.Push<LoadingView>();
                loadingView.Setup("Allocating game ...");

                await flow(loadingView);
            }
            catch (Exception e)
            {
                Toaster.PushError("Failed to start game", e);
                Debug.LogException(e, _orchestrator.gameAllocator);
                ResetStartState();
            }
            finally
            {
                if (loadingView)
                    loadingView.PopMe();
            }
        }

        private void ResetStartState()
        {
            ResetStatusLabels();
            _gameStarted = false;
            if (_lobby != null)
                UpdateLocalPlayerData(_lobby);
        }

        public void ToggleMicrophone()
        {
            _hasLocalMicEnabled = !_hasLocalMicEnabled;
#if PURR_VOICE
            onLocalMicEnabledChanged?.Invoke(_hasLocalMicEnabled);
#endif
            UpdateMicGraphics();
        }

        private void UpdateMicGraphics()
        {
            _microphoneText.text = _hasLocalMicEnabled ? "<icon=microphone>" : "<icon=microphone_off>";

            var color = _hasLocalMicEnabled ? _unmutedColor : _mutedColor;
            var highlight = _hasLocalMicEnabled ? _unmutedHover : _mutedHover;

            ThemeColors.Set(_microphoneButton, color, highlight,
                _hasLocalMicEnabled ? _unmutedColorTone : _mutedColorTone,
                _hasLocalMicEnabled ? _unmutedHoverTone : _mutedHoverTone);
            color.contrast = true;
            ThemeColors.Set(_microphoneText, color);
        }

        private void ConnectToLobby(ILobby lobby)
        {
            if (!_closingLobby && _connectToPurrnetInLobby && _lobby.localPlayer != null &&
                !_lobbyConnected && _lobbyConnection)
            {
                // Mark connected first because provider setup can synchronously replay
                // a terminal event and ask this view to disconnect again.
                _lobbyConnected = true;
                try
                {
                    _lobbyConnection.JoinedLobby(lobby);
                }
                catch
                {
                    _lobbyConnected = false;
                    throw;
                }
            }
        }

        private void OnPlayerUpdated(IPlayer player)
        {
            UpdateLocalPlayerData(_lobby);
        }

        public void CopyLobbyCodeToClipboard()
        {
            GUIUtility.systemCopyBuffer = _lobby.joinCode;
            Toaster.Push("Lobby Code", "Code copied to clipboard!");
        }

        private bool _wasReady = true;
        private bool _readyStateInitialized;

        private void RenderPlayerList(ILobby lobby)
        {
            // Keep the lobby owner connected, but never render it as a player. This
            // also handles host migration by revealing the old owner and hiding the new one.
            List<IPlayer> entriesToHide = null;
            foreach (var pair in _uiPlayerEntry)
            {
                if (!pair.Key.isOwner)
                    continue;

                entriesToHide ??= new List<IPlayer>();
                entriesToHide.Add(pair.Key);
                if (pair.Value)
                    Destroy(pair.Value.gameObject);
            }

            if (entriesToHide != null)
            {
                foreach (var player in entriesToHide)
                    _uiPlayerEntry.Remove(player);
            }

            foreach (var player in lobby.players)
            {
                if (!player.isOwner && !_uiPlayerEntry.ContainsKey(player))
                    CreatePlayerEntry(player);
            }

            _playerPlaceholderPool.ResetCounter();

            int emptySlots = Mathf.Max(0, lobby.maxPlayers - lobby.players.Count);
            for (int i = 0; i < emptySlots; i++)
                _playerPlaceholderPool.GetInstance().SetAsLastSibling();

            _playerPlaceholderPool.DiscardRest();

            UpdateLocalPlayerData(lobby);
        }

        private void CreatePlayerEntry(IPlayer player)
        {
            var entry = Instantiate(_playerPrefab, _playerContent);
            entry.Setup(_lobby, player, OnKickPlayer);
            _uiPlayerEntry.Add(player, entry);
        }

        private void UpdateLocalPlayerData(ILobby lobby)
        {
            bool localIsDisplayHost = lobby.localPlayer?.isOwner == true;
            if (_readyButton)
            {
                _readyButton.gameObject.SetActive(true);
                // Keep the host button clickable so an invalid attempt can explain
                // the missing requirement in chat. TryStartGame remains authoritative.
                _readyButton.interactable = !localIsDisplayHost || !_gameStarted;
            }

            if (localIsDisplayHost)
            {
                _readyButtonText.text = "Start Game";
                ThemeColors.Set(_readyButton, _readyColor, _readyHover, _readyColorTone, _readyHoverTone);
                ThemeColors.Set(_readyButtonText, _readyTextColor);
                return;
            }

            bool localPlayerReady = lobby.localPlayer?.isReady == true;

            if (!_readyStateInitialized || _wasReady != localPlayerReady)
            {
                _readyButtonText.text = localPlayerReady ? "Unready" : "Ready";
                var color = localPlayerReady ? _readyColor : _unreadyColor;
                ThemeColors.Set(_readyButton, color,
                    localPlayerReady ? _readyHover : _unreadyHover,
                    localPlayerReady ? _readyColorTone : _unreadyColorTone,
                    localPlayerReady ? _readyHoverTone : _unreadyHoverTone);
                ThemeColors.Set(_readyButtonText, localPlayerReady ? _readyTextColor : _unreadyTextColor);
                _wasReady = localPlayerReady;
                _readyStateInitialized = true;
            }
        }

        public void ToggleReady()
        {
            if (_lobby?.localPlayer == null)
            {
                Toaster.PushError("Lobby Error", "Your player isn't connected yet.");
                return;
            }

            if (_lobby.localPlayer.isOwner)
            {
                TryStartGame();
                return;
            }

            _lobby.localPlayer.SetReady(!_lobby.localPlayer.isReady);
        }

        private void TryStartGame()
        {
            // Revalidate on the host at click time; the disabled button is only presentation.
            if (!CanHostStartGame())
            {
                SendStartRequirementToChat();
                return;
            }

            _gameStarted = true;
            _readyButton.interactable = false;
            _lobbyStatus.text = "STARTING GAME";
            _lobbyStatusDetails.text = "LOADING ...";
            _lobby.lobbyData.SetData(LOBBY_STATUS_STRING, _lobbyStatus.text);
            _lobby.lobbyData.SetData(LOBBY_STATUS_DETAILS_STRING, _lobbyStatusDetails.text);
            StartGame();
        }

        private void SendStartRequirementToChat()
        {
            int participantCount = 0;
            bool allParticipantsReady = true;

            foreach (var player in _lobby.players)
            {
                if (player.isOwner)
                    continue;

                participantCount++;
                if (!player.isReady)
                    allParticipantsReady = false;
            }

            string message;
            if (participantCount == 4)
            {
                message = "4 player mode doesn\u2019t work. Sorry\u2026";
            }
            else if (participantCount < _minimumPlayersToStart)
            {
                message = "This game needs at least 3 players, excluding the display host.";
            }
            else if (!allParticipantsReady)
            {
                message = "Chill, man! Not everyone is readied up!";
            }
            else
            {
                return;
            }

            _lobby.chat.SendMessage(Encoding.UTF8.GetBytes(message));
        }

#if !UNITY_WEBGL
        private void OnGUI()
        {
            if (!Application.isPlaying || _lobby?.localPlayer?.isOwner != true || _minimumPlayersToStart <= 1)
                return;

            if (GUI.Button(new Rect(16f, 16f, 280f, 42f), "DEV: Allow 2-player start"))
            {
                // One participant plus the display host is two connected people total.
                _minimumPlayersToStart = 1;
                UpdateLocalPlayerData(_lobby);
            }
        }
#endif

        private void OnKickPlayer(IPlayer target)
        {
            _lobby.KickPlayer(target);
        }

        private bool _lobbyEventsUnsubscribed;

        private void UnsubscribeLobbyEvents()
        {
            if (_lobbyEventsUnsubscribed || _lobby == null)
                return;

            _lobbyEventsUnsubscribed = true;
            _lobby.onPlayerJoined -= OnPlayerJoined;
            _lobby.onPlayerLeft -= OnPlayerLeft;
            _lobby.onPlayerUpdated -= OnPlayerUpdated;
            _lobby.onLobbyDestroyed -= OnLobbyDestroyed;
            _lobby.onOwnerChanged -= OnOwnerChanged;
            _lobby.lobbyData.onDataChanged -= OnMetadata;
        }

        public override void OnPopped()
        {
            UnsubscribeLobbyEvents();
            DisconnectFromLobby();
        }

        private void DisconnectFromLobby()
        {
            if (!_lobbyConnected)
                return;

            // Clear this first because stopping the transport can synchronously
            // invoke lobby/provider callbacks.
            _lobbyConnected = false;
            if (_lobby != null && _lobbyConnection)
                _lobbyConnection.LeftLobby(_lobby);
        }

        private void OnDestroy()
        {
            UnsubscribeLobbyEvents();
            DisconnectFromLobby();
        }

        private void OnOwnerChanged(IPlayer host)
        {
            RenderPlayerList(_lobby);

            if (_lobbyConnected && _lobbyConnection)
                _lobbyConnection.OnHostChanged(_lobby, host, host == _lobby.localPlayer);
        }

        private void OnLobbyDestroyed()
        {
            CloseLobbyView();
        }

        public void LeaveLobby()
        {
            if (_closingLobby)
                return;

            _closingLobby = true;
            var lobby = _lobby;
            ClearActiveLobby();
            DisconnectFromLobby();
            lobby?.LeaveLobby();
            PopMe();
        }

        private void CloseLobbyView()
        {
            if (_closingLobby)
                return;

            _closingLobby = true;
            ClearActiveLobby();
            DisconnectFromLobby();
            PopMe();
        }

        private void ClearActiveLobby()
        {
            if (_orchestrator != null && _orchestrator.activeLobby == _lobby)
                _orchestrator.activeLobby = null;
        }

        private readonly Dictionary<IPlayer, PlayerEntry> _uiPlayerEntry = new();

        private void ClearPlayerEntries()
        {
            foreach (var entry in _uiPlayerEntry.Values)
            {
                if (entry)
                    Destroy(entry.gameObject);
            }
            _uiPlayerEntry.Clear();
        }

        private void OnPlayerJoined(IPlayer player)
        {
            var localPlayer = _lobby?.localPlayer;
            if (localPlayer != null)
                _localPlayerId = localPlayer.id;

            RenderPlayerList(_lobby);
            ConnectToLobby(_lobby);

            if (_lobbyConnected)
                _lobbyConnection.OnPlayerRegistered(player);
        }

        private void OnPlayerLeft(IPlayer player)
        {
            if (player != null && !string.IsNullOrEmpty(_localPlayerId) && player.id == _localPlayerId)
            {
                CloseLobbyView();
                return;
            }

            if (_uiPlayerEntry.Remove(player, out var entry) && entry)
                Destroy(entry.gameObject);

            RenderPlayerList(_lobby);

            if (_lobbyConnected)
                _lobbyConnection.OnPlayerUnregistered(player);
        }

        protected override IEnumerator OnExitTransition()
        {
            return ViewTransitions.SlideToRight(_content);
        }

        protected override IEnumerator OnEnterTransition()
        {
            return ViewTransitions.SlideFromRight(_content);
        }

        public bool TryGetPlayerEntry(IPlayer player, out PlayerEntry entry)
        {
            return _uiPlayerEntry.TryGetValue(player, out entry);
        }
    }
}
