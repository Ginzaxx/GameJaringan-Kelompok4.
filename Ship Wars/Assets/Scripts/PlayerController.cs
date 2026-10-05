using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

/// PlayerController untuk Ship Wars (FishNet 3D).
public class PlayerController : NetworkBehaviour
{
    [Header("Movement & Fuel Settings (GDD)")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float maxFuel = 100f;
    [SerializeField] private float fuelConsumptionRate = 10f; // Fuel terpakai per detik saat bergerak



    [Header("Visual Feedback (Modul 3)")]
    [SerializeField] private Renderer shipRenderer;

    // Synchronized variables via FishNet
    private readonly SyncVar<float> _currentFuel = new SyncVar<float>();
    private readonly SyncVar<bool> _isMyTurn = new SyncVar<bool>(true);
    private readonly SyncVar<bool> _hasFiredThisTurn = new SyncVar<bool>(false);



    private float _uiMovementInput = 0f;

    // Public getters untuk UI HUD
    public float CurrentFuel => _currentFuel.Value;
    public float MaxFuel => maxFuel;
    public bool IsMyTurn => _isMyTurn.Value;
    public bool HasFiredThisTurn => _hasFiredThisTurn.Value;

    public override void OnStartClient()
    {
        base.OnStartClient();
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentFuel.Value = maxFuel;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
    }

    private void Update()
    {
        if (!IsOwner || !_isMyTurn.Value || _hasFiredThisTurn.Value)
        {
            return;
        }

        HandleMovementAndFuel();
    }

    private void HandleMovementAndFuel()
    {

        float horizontalInput = GetHorizontalInput();

        if (_currentFuel.Value > 0f && Mathf.Abs(horizontalInput) > 0.01f)
        {
            // Mengubah pergerakan menjadi sumbu Z (-/+ Z)
            Vector3 moveDirection = Vector3.forward * horizontalInput * moveSpeed * Time.deltaTime;
            transform.position += moveDirection;

            float fuelUsed = fuelConsumptionRate * Time.deltaTime;
            ConsumeFuelServerRpc(fuelUsed);
        }
    }

    /// <summary>
    /// Menghasilkan input horizontal (-1 sampai 1) secara aman dari New Input System, UI, atau Legacy Input.
    /// </summary>
    private float GetHorizontalInput()
    {
        if (Mathf.Abs(_uiMovementInput) > 0.01f)
        {
            return _uiMovementInput;
        }

        float input = 0f;

        // 1. Membaca New Input System (Keyboard)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                input -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                input += 1f;
        }

        // 2. Membaca New Input System (Gamepad Stick)
        if (Mathf.Abs(input) < 0.01f && Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.x.ReadValue();
        }

        // 3. Legacy Input Fallback (Safe check)
        if (Mathf.Abs(input) < 0.01f)
        {
            try
            {
                input = Input.GetAxis("Horizontal");
            }
            catch
            {
                // Disembunyikan jika Legacy Input dimatikan di Player Settings
            }
        }

        return input;
    }

    /// <summary>
    /// API untuk tombol UI Movement (Kiri/Kanan)
    /// </summary>
    public void SetUIMovementInput(float direction)
    {
        _uiMovementInput = direction;
    }



    [ServerRpc]
    private void ConsumeFuelServerRpc(float amount)
    {
        _currentFuel.Value = Mathf.Max(0f, _currentFuel.Value - amount);
    }

    [Server]
    public void SetHasFiredThisTurnServer()
    {
        _hasFiredThisTurn.Value = true;
    }

    [Server]
    public void StartNewTurn()
    {
        _currentFuel.Value = maxFuel;
        _hasFiredThisTurn.Value = false;
        _isMyTurn.Value = true;
    }

    [Server]
    public void EndTurn()
    {
        _isMyTurn.Value = false;
    }
}
