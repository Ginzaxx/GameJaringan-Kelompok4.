using FishNet.Managing;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private NetworkManager _networkManager;

    private void Awake()
    {
        // Mendapatkan referensi komponen NetworkManager FishNet di scene
        _networkManager = FindAnyObjectByType<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager (FishNet) tidak ditemukan di dalam Scene!");
            return;
        }

        // Menambahkan listener event pada tombol UI
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
    }

    private void OnHostButtonClicked()
    {
        if (_networkManager != null)
        {
            // FishNet menjalankan Server dan Client secara bersamaan untuk skenario Host
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();

            UpdateUIStatus("Status: Connected as HOST");
        }
        else
        {
            UpdateUIStatus("Status: Failed to Start Host");
        }
    }

    private void OnClientButtonClicked()
    {
        if (_networkManager != null)
        {
            // FishNet menjalankan Client untuk terhubung ke alamat IP server
            _networkManager.ClientManager.StartConnection();

            UpdateUIStatus("Status: Connecting as CLIENT...");
        }
        else
        {
            UpdateUIStatus("Status: Failed to Start Client");
        }
    }

    private void UpdateUIStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        // Menyembunyikan tombol pilihan setelah role dipilih
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
    }
}