using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// PlayerSpawner untuk Ship Wars menggunakan FishNet.
/// Bertanggung jawab men-spawn Player 1 dan Player 2 di posisi bertentangan (Opposing Spawn Points)
/// saat client terhubung ke server/host.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab (Must have NetworkObject)")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Positions (Opposing Sides)")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Default Spawn Vectors (Fallback jika Transform belum diassign)")]
    [SerializeField] private Vector3 defaultPos1 = new Vector3(-15f, 0.5f, 0f);
    [SerializeField] private Vector3 defaultPos2 = new Vector3(15f, 0.5f, 0f);
    [SerializeField] private Vector3 defaultRot1 = new Vector3(0f, 90f, 0f);  // Menghadap kanan (Timur)
    [SerializeField] private Vector3 defaultRot2 = new Vector3(0f, -90f, 0f); // Menghadap kiri (Barat)

    private ServerManager _serverManager;
    private int _spawnedPlayerCount = 0;

    private void Awake()
    {
        _serverManager = InstanceFinder.ServerManager;
    }

    private void OnEnable()
    {
        if (_serverManager != null)
        {
            _serverManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }
    }

    private void OnDisable()
    {
        if (_serverManager != null)
        {
            _serverManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }
    }

    /// <summary>
    /// Dipanggil otomatis oleh FishNet saat ada status koneksi client remote berubah.
    /// </summary>
    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        // Hanya proses saat client berhasil terhubung sepenuhnya (Started)
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            SpawnPlayerForConnection(connection);
        }
    }

    /// <summary>
    /// Men-spawn player prefab di server dan memberikan ownership ke connection client.
    /// </summary>
    private void SpawnPlayerForConnection(NetworkConnection connection)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] PlayerPrefab belum di-assign di Inspector!");
            return;
        }

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        // Player 1 (Koneksi pertama): Spawn di SpawnPoint 1
        // Player 2 (Koneksi kedua): Spawn di SpawnPoint 2
        if (_spawnedPlayerCount % 2 == 0)
        {
            spawnPosition = spawnPoint1 != null ? spawnPoint1.position : defaultPos1;
            spawnRotation = spawnPoint1 != null ? spawnPoint1.rotation : Quaternion.Euler(defaultRot1);
        }
        else
        {
            spawnPosition = spawnPoint2 != null ? spawnPoint2.position : defaultPos2;
            spawnRotation = spawnPoint2 != null ? spawnPoint2.rotation : Quaternion.Euler(defaultRot2);
        }

        // Instantiate objek Player di server
        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);

        // Spawn objek ke jaringan FishNet dengan memberikan hak milik (Ownership) ke client
        _serverManager.Spawn(playerInstance.gameObject, connection);

        _spawnedPlayerCount++;
        Debug.Log($"[PlayerSpawner] Spawned Player {_spawnedPlayerCount} untuk Connection ID: {connection.ClientId} di posisi {spawnPosition}");
    }
}
