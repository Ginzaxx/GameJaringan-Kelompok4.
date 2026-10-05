using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class CannonShoot : NetworkBehaviour
{
    [Header("Aiming & Cannon Settings")]
    public Transform cannonTransform;
    public float elevationSpeed = 1500f;
    public float minElevation = -90f;
    public float maxElevation = 20f;
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float firePower = 22f;
    public ParticleSystem muzzleFlashVFX;

    [Header("Trajectory Preview Arc")]
    public LineRenderer trajectoryLine;
    public int trajectoryResolution = 30;
    public float trajectoryTimeStep = 0.1f;

    private readonly SyncVar<float> _currentElevation = new SyncVar<float>(15f);

    private bool _isAiming = false;
    private PlayerController _playerController;

    public float CurrentElevation => _currentElevation.Value;
    public bool IsAiming => _isAiming;

    public override void OnStartClient()
    {
        base.OnStartClient();

        _playerController = GetComponent<PlayerController>();

        _currentElevation.OnChange += OnCannonRotationChanged;

        if (trajectoryLine != null)
            trajectoryLine.enabled = false;

        ApplyCannonRotation(_currentElevation.Value);
    }

    private void OnDestroy()
    {
        _currentElevation.OnChange -= OnCannonRotationChanged;
    }

    private void OnCannonRotationChanged(float prev, float next, bool asServer)
    {
        ApplyCannonRotation(_currentElevation.Value);
    }

    private void Update()
    {
        if (!IsOwner)
        {
            ApplyCannonRotation(_currentElevation.Value);
            return;
        }

        if (_playerController != null && (!_playerController.IsMyTurn || _playerController.HasFiredThisTurn))
        {
            HideTrajectoryPreview();
            return;
        }

        if (_isAiming)
            UpdateTrajectoryPreview();
        else
            HideTrajectoryPreview();

        // Tembak dengan Space
        if (Input.GetKeyDown(KeyCode.Space))
            Shoot();
    }

    public void Shoot()
    {
        if (!IsOwner || (_playerController != null && _playerController.HasFiredThisTurn)) return;

        _isAiming = false;
        HideTrajectoryPreview();

        Vector3 spawnPos = firePoint != null ? firePoint.position : (cannonTransform != null ? cannonTransform.position : transform.position + transform.forward * 2f);
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : (cannonTransform != null ? cannonTransform.rotation : transform.rotation);
        Vector3 launchVelocity = (firePoint != null ? firePoint.forward : (cannonTransform != null ? cannonTransform.forward : transform.forward)) * firePower;

        FireShotServerRpc(spawnPos, spawnRot, launchVelocity);
    }

    public void StartAiming()
    {
        if (!IsOwner) return;
        if (_playerController != null && (!_playerController.IsMyTurn || _playerController.HasFiredThisTurn)) return;

        _isAiming = true;
        UpdateTrajectoryPreview();
    }

    public void AimJoystickUpdate(Vector2 joystickInput)
    {
        if (!_isAiming || !IsOwner) return;

        float newElevation = Mathf.Clamp(
            _currentElevation.Value + (-joystickInput.y) * elevationSpeed * Time.deltaTime,
            minElevation,
            maxElevation
        );

        ApplyCannonRotation(newElevation);
        UpdateCannonRotationServerRpc(newElevation);
    }

    [ServerRpc]
    private void UpdateCannonRotationServerRpc(float elevation)
    {
        _currentElevation.Value = elevation;
    }

    private void ApplyCannonRotation(float elevation)
    {
        if (cannonTransform != null)
            cannonTransform.localRotation = Quaternion.Euler(70f + elevation, 0f, 0f);
    }

    public void StopAiming()
    {
        if (!IsOwner) return;

        _isAiming = false;
        HideTrajectoryPreview();
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
            trajectoryLine.enabled = false;
    }

    [ServerRpc]
    private void FireShotServerRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (_playerController != null)
        {
            if (_playerController.HasFiredThisTurn) return;
            _playerController.SetHasFiredThisTurnServer();
        }

        if (projectilePrefab != null)
        {
            GameObject proj = Instantiate(projectilePrefab, position, rotation);
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = velocity;
            base.Spawn(proj);
        }

        Debug.Log($"[Server] Player {OwnerId} fired a shot from CannonShoot!");
        PlayShootEffectsClientRpc();
    }

    [ObserversRpc]
    private void PlayShootEffectsClientRpc()
    {
        if (muzzleFlashVFX != null)
            muzzleFlashVFX.Play();
    }
}
