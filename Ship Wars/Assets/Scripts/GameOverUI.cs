using FishNet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button restartButton;

    private void Start()
    {
        gameOverPanel.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleGameOver;
        }

        restartButton.onClick.AddListener(() =>
        {
            // Shutdown network koneksi FishNet (Client dan Server)
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.StopConnection(true);
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.StopConnection();

            // Kembali ke scene awal (misal index 0 adalah MainMenu)
            SceneManager.LoadScene("Battle");
        });
    }

    private void HandleGameOver(ulong winnerClientId)
    {
        gameOverPanel.SetActive(true);

        // Mendapatkan ID Client lokal menggunakan InstanceFinder FishNet
        ulong localClientId = (ulong)InstanceFinder.ClientManager.Connection.ClientId;

        if (localClientId == winnerClientId)
        {
            winnerText.text = "YOU WIN!";
            winnerText.color = Color.green;
        }
        else
        {
            winnerText.text = "YOU LOSE!";
            winnerText.color = Color.red;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }
}