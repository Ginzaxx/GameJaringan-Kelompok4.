using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State
    {
        WaitingForPlayers,
        Countdown,
        Gameplay,
        GameOver
    }

    [Header("Match Settings")]
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float turnTimeLimit = 30f;

    [Header("Debug")]
    [Tooltip("Jika aktif: game langsung mulai saat 1 player masuk, player 1 selalu jalan pertama, dan tidak ada pergantian turn.")]
    [SerializeField] private bool testingMode = false;

    // PERBAIKAN 1: Menggunakan generic SyncVar<T> alih-alih atribut [SyncVar]
    private readonly SyncVar<State> _gameState = new SyncVar<State>(State.WaitingForPlayers);
    private readonly SyncVar<int> _currentTurnIndex = new SyncVar<int>(-1);

    private List<PlayerController> _activePlayers = new List<PlayerController>();

    public event Action<State> OnGameStateChanged;
    public event Action<ulong> OnGameOver;
    public event Action<int, PlayerController> OnTurnChanged;

    private float _timer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Mendaftarkan callback OnChange untuk SyncVar<T> FishNet versi terbaru
        _gameState.OnChange += OnGameStateChangedCallback;
        _currentTurnIndex.OnChange += OnCurrentTurnIndexChanged;
    }

    private void OnDestroy()
    {
        // Melepas event agar tidak terjadi memory leak
        _gameState.OnChange -= OnGameStateChangedCallback;
        _currentTurnIndex.OnChange -= OnCurrentTurnIndexChanged;

        // PERBAIKAN 2: Mengganti IsServer menjadi IsServerInitialized
        if (IsServerInitialized && ServerManager != null)
        {
            ServerManager.Objects.OnPreDestroyClientObjects -= OnClientDisconnect;
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // PERBAIKAN 2: Mengganti IsServer menjadi IsServerInitialized
        if (IsServerInitialized)
        {
            ServerManager.Objects.OnPreDestroyClientObjects += OnClientDisconnect;
        }
    }

    // Callback event handler saat nilai SyncVar berubah
    private void OnGameStateChangedCallback(State oldState, State newState, bool asServer)
    {
        OnGameStateChanged?.Invoke(newState);
    }

    private void OnCurrentTurnIndexChanged(int oldIndex, int newIndex, bool asServer)
    {
        if (newIndex >= 0 && newIndex < _activePlayers.Count)
        {
            OnTurnChanged?.Invoke(newIndex, _activePlayers[newIndex]);
        }
    }

    public void RegisterPlayer(PlayerController player)
    {
        // PERBAIKAN 2: Mengganti IsServer menjadi IsServerInitialized
        if (!IsServerInitialized) return;

        if (!_activePlayers.Contains(player))
        {
            _activePlayers.Add(player);
            player.EndTurn();

            if (testingMode)
            {
                // Testing: langsung mulai saat player pertama masuk, tanpa menunggu player 2
                if (_gameState.Value == State.WaitingForPlayers && _activePlayers.Count >= 1)
                {
                    _gameState.Value = State.Countdown;
                    _timer = 0f; // langsung mulai tanpa countdown
                }
            }
            else
            {
                if (_gameState.Value == State.WaitingForPlayers && _activePlayers.Count >= 2)
                {
                    _gameState.Value = State.Countdown;
                    _timer = countdownDuration;
                }
            }
        }
    }

    private void OnClientDisconnect(NetworkConnection conn)
    {
        _activePlayers.RemoveAll(p => p.Owner == conn);

        if (_gameState.Value == State.Gameplay && _activePlayers.Count < 2)
        {
            if (_activePlayers.Count == 1)
            {
                CheckWinConditionOnPlayerDeath((ulong)conn.ClientId);
            }
            else
            {
                _gameState.Value = State.WaitingForPlayers;
            }
        }
    }

    private void Update()
    {
        if (!IsServerInitialized) return;

        switch (_gameState.Value)
        {
            case State.Countdown:
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    StartGame();
                }
                break;

            case State.Gameplay:
                // Kosong, karena kita menggunakan Event-Driven (NotifyTurnActionCompleted)
                // alih-alih melakukan pengecekan di setiap frame (Update).
                break;

            case State.GameOver:
                break;
        }
    }

    [Server]
    private void StartGame()
    {
        _gameState.Value = State.Gameplay;

        if (testingMode)
        {
            // Testing: tidak di-shuffle, player pertama yang daftar selalu jalan pertama
            Debug.Log("[Server] Testing mode: no shuffle, player 1 goes first.");
        }
        else
        {
            ShufflePlayers();
        }

        _currentTurnIndex.Value = 0;
        StartTurnForCurrentPlayer();
    }

    [Server]
    private void StartTurnForCurrentPlayer()
    {
        if (_currentTurnIndex.Value >= 0 && _currentTurnIndex.Value < _activePlayers.Count)
        {
            PlayerController currentPlayer = _activePlayers[_currentTurnIndex.Value];
            currentPlayer.StartNewTurn();
            _timer = turnTimeLimit;
            Debug.Log($"[Server] Turn started for Player {currentPlayer.OwnerId}");
        }
    }

    [Server]
    private void NextTurn()
    {
        if (_currentTurnIndex.Value >= 0 && _currentTurnIndex.Value < _activePlayers.Count)
        {
            _activePlayers[_currentTurnIndex.Value].EndTurn();
        }

        int nextIndex = _currentTurnIndex.Value + 1;
        if (nextIndex >= _activePlayers.Count)
        {
            nextIndex = 0; // Looping kembali ke player pertama
        }

        _currentTurnIndex.Value = nextIndex;
        StartTurnForCurrentPlayer();
    }

    // Fungsi Trigger yang akan dipanggil oleh script senjata setelah menembak
    [Server]
    public void NotifyTurnActionCompleted(PlayerController actionPlayer)
    {
        if (_gameState.Value == State.Gameplay &&
            _currentTurnIndex.Value >= 0 &&
            _activePlayers[_currentTurnIndex.Value] == actionPlayer)
        {
            Debug.Log($"[Server] GameManager menerima sinyal aksi selesai dari Player {actionPlayer.OwnerId}");

            if (testingMode)
            {
                // Testing: tidak ganti turn, reset giliran player yang sama
                Invoke(nameof(ResetCurrentPlayerTurn), 1f);
            }
            else
            {
                // Normal: pindah ke turn berikutnya setelah 1 detik
                Invoke(nameof(NextTurn), 1f);
            }
        }
    }

    [Server]
    private void ResetCurrentPlayerTurn()
    {
        if (_currentTurnIndex.Value >= 0 && _currentTurnIndex.Value < _activePlayers.Count)
        {
            PlayerController currentPlayer = _activePlayers[_currentTurnIndex.Value];
            currentPlayer.StartNewTurn();
            Debug.Log($"[Server] Testing mode: turn reset for Player {currentPlayer.OwnerId}");
        }
    }

    [Server]
    private void ShufflePlayers()
    {
        for (int i = 0; i < _activePlayers.Count; i++)
        {
            PlayerController temp = _activePlayers[i];
            int randomIndex = UnityEngine.Random.Range(i, _activePlayers.Count);
            _activePlayers[i] = _activePlayers[randomIndex];
            _activePlayers[randomIndex] = temp;
        }
    }

    [Server]
    public void CheckWinConditionOnPlayerDeath(ulong deadPlayerClientId)
    {
        if (!IsServerInitialized || _gameState.Value != State.Gameplay) return;

        foreach (var p in _activePlayers)
        {
            if ((ulong)p.OwnerId != deadPlayerClientId)
            {
                ulong winnerClientId = (ulong)p.OwnerId;
                _gameState.Value = State.GameOver;
                NotifyGameOverObserversRpc(winnerClientId);
                break;
            }
        }
    }

    [ObserversRpc]
    private void NotifyGameOverObserversRpc(ulong winnerClientId)
    {
        OnGameOver?.Invoke(winnerClientId);
    }
}