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

    [Header("Aiming & Cannon Settings")]
    [SerializeField] private Transform cannonTransform;
    [SerializeField] private float elevationSpeed = 40f;   // Kecepatan perubahan elevasi (Sumbu Y Joystick)
    [SerializeField] private float yawAimSpeed = 60f;      // Kecepatan rotasi horizontal meriam (Sumbu X Joystick)
    [SerializeField] private float minElevation = 0f;
    [SerializeField] private float maxElevation = 60f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float firePower = 22f;

    [Header("Trajectory Preview Arc (GDD)")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int trajectoryResolution = 30;
    [SerializeField] private float trajectoryTimeStep = 0.1f;

    [Header("Visual Feedback (Modul 3)")]
    [SerializeField] private Renderer shipRenderer;

    // Synchronized variables via FishNet
    private readonly SyncVar<float> _currentFuel = new SyncVar<float>();
    private readonly SyncVar<bool> _isMyTurn = new SyncVar<bool>(true);
    private readonly SyncVar<bool> _hasFiredThisTurn = new SyncVar<bool>(false);

    // Synchronized Cannon Angles (Agar meriam selaras di layar Host dan Client!)
    private readonly SyncVar<float> _currentElevation = new SyncVar<float>(15f);
    private readonly SyncVar<float> _currentYaw = new SyncVar<float>(0f);

    private bool _isAiming = false;
    private float _uiMovementInput = 0f;

    // Public getters untuk UI HUD
    public float CurrentFuel => _currentFuel.Value;
    public float MaxFuel => maxFuel;
    public bool IsMyTurn => _isMyTurn.Value;
    public float CurrentElevation => _currentElevation.Value;
    public float CurrentYaw => _currentYaw.Value;
    public bool IsAiming => _isAiming;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (shipRenderer == null)
        {
            shipRenderer = GetComponentInChildren<Renderer>();
        }

        if (shipRenderer != null)
        {
            if (IsOwner)
            {
                shipRenderer.material.color = Color.green;
            }
            else
            {
                shipRenderer.material.color = Color.red;
            }
        }

        _currentElevation.OnChange += OnCannonRotationChanged;
        _currentYaw.OnChange += OnCannonRotationChanged;

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }

        ApplyCannonRotation(_currentElevation.Value, _currentYaw.Value);
    }

    private void OnDestroy()
    {
        _currentElevation.OnChange -= OnCannonRotationChanged;
        _currentYaw.OnChange -= OnCannonRotationChanged;
    }

    private void OnCannonRotationChanged(float prev, float next, bool asServer)
    {
        ApplyCannonRotation(_currentElevation.Value, _currentYaw.Value);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentFuel.Value = maxFuel;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            ApplyCannonRotation(_currentElevation.Value, _currentYaw.Value);
            return;
        }

        if (!_isMyTurn.Value || _hasFiredThisTurn.Value)
        {
            HideTrajectoryPreview();
            return;
        }

        HandleMovementAndFuel();

        if (_isAiming)
        {
            UpdateTrajectoryPreview();
        }
        else
        {
            HideTrajectoryPreview();
        }
    }

    private void HandleMovementAndFuel()
    {
        if (_isAiming) return;

        float horizontalInput = GetHorizontalInput();

        if (_currentFuel.Value > 0f && Mathf.Abs(horizontalInput) > 0.01f)
        {
            Vector3 moveDirection = transform.right * horizontalInput * moveSpeed * Time.deltaTime;
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

    public void StartAiming()
    {
        if (!IsOwner || !_isMyTurn.Value || _hasFiredThisTurn.Value) return;

        _isAiming = true;
        UpdateTrajectoryPreview();
    }

    public void AimJoystickUpdate(Vector2 joystickInput)
    {
        if (!_isAiming || !IsOwner) return;

        float newElevation = Mathf.Clamp(_currentElevation.Value + joystickInput.y * elevationSpeed * Time.deltaTime, minElevation, maxElevation);
        float newYaw = _currentYaw.Value + joystickInput.x * yawAimSpeed * Time.deltaTime;

        ApplyCannonRotation(newElevation, newYaw);
        UpdateCannonRotationServerRpc(newElevation, newYaw);
    }

    [ServerRpc]
    private void UpdateCannonRotationServerRpc(float elevation, float yaw)
    {
        _currentElevation.Value = elevation;
        _currentYaw.Value = yaw;
    }

    private void ApplyCannonRotation(float elevation, float yaw)
    {
        if (cannonTransform != null)
        {
            cannonTransform.localRotation = Quaternion.Euler(-elevation, yaw, 0f);
        }
    }

    public void ConfirmFire()
    {
        if (!_isAiming || !IsOwner || _hasFiredThisTurn.Value) return;

        _isAiming = false;
        HideTrajectoryPreview();

        Vector3 spawnPos = firePoint != null ? firePoint.position : (cannonTransform != null ? cannonTransform.position : transform.position + transform.forward * 2f);
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : (cannonTransform != null ? cannonTransform.rotation : transform.rotation);
        Vector3 launchVelocity = (firePoint != null ? firePoint.forward : (cannonTransform != null ? cannonTransform.forward : transform.forward)) * firePower;

        FireShotServerRpc(spawnPos, spawnRot, launchVelocity);
    }

    private void UpdateTrajectoryPreview()
    {
        if (trajectoryLine == null) return;

        trajectoryLine.enabled = true;
        trajectoryLine.positionCount = trajectoryResolution;

        Vector3 startPos = firePoint != null ? firePoint.position : (cannonTransform != null ? cannonTransform.position : transform.position + transform.forward * 2f);
        Vector3 startVelocity = (firePoint != null ? firePoint.forward : (cannonTransform != null ? cannonTransform.forward : transform.forward)) * firePower;

        for (int i = 0; i < trajectoryResolution; i++)
        {
            float t = i * trajectoryTimeStep;
            Vector3 point = startPos + startVelocity * t + 0.5f * Physics.gravity * t * t;
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void HideTrajectoryPreview()
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }
    }

    [ServerRpc]
    private void ConsumeFuelServerRpc(float amount)
    {
        _currentFuel.Value = Mathf.Max(0f, _currentFuel.Value - amount);
    }

    [ServerRpc]
    private void FireShotServerRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (_hasFiredThisTurn.Value) return;

        _hasFiredThisTurn.Value = true;

        if (projectilePrefab != null)
        {
            GameObject proj = Instantiate(projectilePrefab, position, rotation);
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = velocity;
#else
                rb.velocity = velocity;
#endif
            }
            base.Spawn(proj);
        }

        Debug.Log($"[Server] Player {OwnerId} fired a shot!");
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
