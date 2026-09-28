using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using TMPro;

public class ScoreManager : NetworkBehaviour
{
    [Header("Debug")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private int currentScoreDebug = 0;

    private readonly SyncVar<int> _score = new SyncVar<int>(0);

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Cari UI di Scene berdasarkan OwnerId (0 = Host/Player1, 1 = Client/Player2)
        int playerNum = OwnerId == 0 ? 1 : 2;
        GameObject scoreGo = GameObject.Find($"PlayerHUD/Player{playerNum}/Score");
        if (scoreGo == null) scoreGo = GameObject.Find($"Player{playerNum}/Score"); // Fallback

        if (scoreGo != null)
        {
            scoreText = scoreGo.GetComponent<TextMeshProUGUI>();
        }
        
        _score.OnChange += OnScoreChanged;
        
        UpdateScoreUI(_score.Value);
    }

    private void OnDestroy()
    {
        _score.OnChange -= OnScoreChanged;
    }

    private void OnScoreChanged(int previousValue, int newValue, bool asServer)
    {
        currentScoreDebug = newValue;
        UpdateScoreUI(newValue);
    }

    private void UpdateScoreUI(int currentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Simulasi input keyboard untuk pengujian
        // Tekan 'L' untuk menambah Score
        if (Input.GetKeyDown(KeyCode.L))
        {
            AddScoreServerRpc(5);
        }
    }

    [ServerRpc]
    private void AddScoreServerRpc(int scoreAmount)
    {
        _score.Value += scoreAmount;
    }
}
