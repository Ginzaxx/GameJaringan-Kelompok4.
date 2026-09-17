using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

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

    private float _currentElevation = 15f;
    private float _currentYaw = 0f;
    private bool _isAiming = false;

    // Public getters untuk UI HUD
    public float CurrentFuel => _currentFuel.Value;
    public float MaxFuel => maxFuel;
    public bool IsMyTurn => _isMyTurn.Value;
    public float CurrentElevation => _currentElevation;
    public float CurrentYaw => _currentYaw;
    public bool IsAiming => _isAiming;

    public override void OnStartClient()
    {
        base.OnStartClient();

        // -------------------------------------------------------------
        // MODUL 3: Visual Feedback berdasarkan Kepemilikan (IsOwner)
        // -------------------------------------------------------------
        if (shipRenderer == null)
        {
            shipRenderer = GetComponentInChildren<Renderer>();
        }

        if (shipRenderer != null)
        {
            // Player Lokal (Owner) = Hijau | Player Lain (Opponent) = Merah
            if (IsOwner)
            {
                shipRenderer.material.color = Color.green;
            }
            else
            {
                shipRenderer.material.color = Color.red;
            }
        }

        // Sembunyikan trajectory line di awal
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentFuel.Value = maxFuel;
    }

    private void Update()
    {
        // KUNCI MULTIPLAYER :
        // Jalankan input & kontrol HANYA jika objek ini milik player lokal
        if (!IsOwner) return;

        // Cegah aksi jika bukan giliran player atau sudah menembak
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

    /// <summary>
    /// Memproses pergerakan kapal dan mengonsumsi fuel sesuai GDD.
    /// PERGERAKAN HANYA BISA KANAN / KIRI (HORIZONTAL), TIDAK BISA NAIK / TURUN (VERTICAL).
    /// </summary>
    private void HandleMovementAndFuel()
    {
        // Tahan pergerakan jika sedang aktif aiming joystick
        if (_isAiming) return;

        // Membaca input Horizontal saja (A / D atau Panah Kiri / Kanan)
        float horizontalInput = Input.GetAxis("Horizontal");

        // Kapal hanya bergerak secara lateral ke kanan/kiri jika masih ada fuel
        if (_currentFuel.Value > 0f && Mathf.Abs(horizontalInput) > 0.01f)
        {
            // Pergerakan menyamping (Right/Left)
            Vector3 moveDirection = transform.right * horizontalInput * moveSpeed * Time.deltaTime;
            transform.position += moveDirection;

            // Konsumsi Fuel berdasarkan pergerakan horizontal
            float fuelUsed = fuelConsumptionRate * Time.deltaTime;
            ConsumeFuelServerRpc(fuelUsed);
        }
    }

    /// Dipanggil saat Joystick Tembak mulai DITEKAN (PointerDown).
    public void StartAiming()
    {
        if (!IsOwner || !_isMyTurn.Value || _hasFiredThisTurn.Value) return;

        _isAiming = true;
        UpdateTrajectoryPreview();
    }

    public void AimJoystickUpdate(Vector2 joystickInput)
    {
        if (!_isAiming || !IsOwner) return;

        // Update Elevasi Meriam (Sumbu Y Joystick)
        _currentElevation += joystickInput.y * elevationSpeed * Time.deltaTime;
        _currentElevation = Mathf.Clamp(_currentElevation, minElevation, maxElevation);

        // Update Rotasi Meriam Horizontal (Sumbu X Joystick)
        _currentYaw += joystickInput.x * yawAimSpeed * Time.deltaTime;

        // Terapkan rotasi pada cannon transform
        if (cannonTransform != null)
        {
            cannonTransform.localRotation = Quaternion.Euler(-_currentElevation, _currentYaw, 0f);
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
