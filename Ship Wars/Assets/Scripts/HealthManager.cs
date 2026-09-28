using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;

public class HealthManager : NetworkBehaviour
{
    [Header("Debug")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private int currentHealthDebug = 100;

    private readonly SyncVar<int> _health = new SyncVar<int>(100);

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Cari UI di Scene berdasarkan OwnerId (0 = Host/Player1, 1 = Client/Player2)
        int playerNum = OwnerId == 0 ? 1 : 2;
        GameObject hpGo = GameObject.Find($"PlayerHUD/Player{playerNum}/Hp");
        if (hpGo == null) hpGo = GameObject.Find($"Player{playerNum}/Hp"); // Fallback

        if (hpGo != null)
        {
            healthSlider = hpGo.GetComponent<Slider>();
        }
        
        _health.OnChange += OnHealthChanged;
        
        UpdateHealthUI(_health.Value);
    }

    private void OnDestroy()
    {
        _health.OnChange -= OnHealthChanged;
    }

    private void OnHealthChanged(int previousValue, int newValue, bool asServer)
    {
        currentHealthDebug = newValue;
        UpdateHealthUI(newValue);
    }

    private void UpdateHealthUI(int currentHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Simulasi input keyboard untuk pengujian
        // Tekan 'K' untuk mengurangi HP
        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamageServerRpc(10);
        }
    }

    [ServerRpc]
    private void TakeDamageServerRpc(int damageAmount)
    {
        _health.Value = Mathf.Max(0, _health.Value - damageAmount);
    }
}
