using FishNet.Object;
using UnityEngine;

/// <summary>
/// Di-attach ke prefab missile. Setelah lockOnDelay detik,
/// missile mencari player musuh dan bergerak lurus ke arahnya.
/// Harus berjalan di server (karena di-spawn via FishNet).
/// </summary>
public class MissileBehavior : NetworkBehaviour
{
    [Header("Homing Settings")]
    public float homingSpeed = 18f;      // Kecepatan setelah lock ke target
    public float lockOnDelay = 2f;       // Detik sebelum missile lock target
    public float lifetime = 12f;         // Waktu hidup maksimum sebelum auto-destruct

    [Header("VFX")]
    public GameObject lockOnVFX;         // Efek visual saat lock (opsional)
    public GameObject explosionVFX;      // Efek meledak saat kena target (opsional)

    private Vector3 _velocity;
    private Transform _target;
    private float _timer = 0f;
    private bool _locked = false;
    private int _ownerClientId = -1;

    // Dipanggil oleh MissileShoot setelah instantiate
    public void Initialize(Vector3 initialVelocity, int ownerClientId)
    {
        _velocity = initialVelocity;
        _ownerClientId = ownerClientId;
    }

    private void Update()
    {
        // Logika hanya berjalan di server
        if (!IsServerInitialized) return;

        _timer += Time.deltaTime;

        // Auto-destruct jika terlalu lama
        if (_timer >= lifetime)
        {
            Despawn();
            return;
        }

        // Setelah lockOnDelay, cari dan lock target musuh
        if (!_locked && _timer >= lockOnDelay)
        {
            TryLockOn();
        }

        if (_locked && _target != null)
        {
            // Gerak lurus ke arah target dengan kecepatan konstan
            Vector3 direction = (_target.position - transform.position).normalized;
            _velocity = direction * homingSpeed;
        }

        // Gerakkan missile
        transform.position += _velocity * Time.deltaTime;

        // Rotasikan ke arah gerak
        if (_velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity);
        }
    }

    private void TryLockOn()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        float closestDist = float.MaxValue;
        Transform closestEnemy = null;

        foreach (var player in players)
        {
            // Skip player yang menembak (owner missile)
            if (player.OwnerId == _ownerClientId) continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestEnemy = player.transform;
            }
        }

        if (closestEnemy != null)
        {
            _target = closestEnemy;
            _locked = true;
            Debug.Log($"[Missile] Locked onto enemy at distance {closestDist:F1}m");

            // Tampilkan efek lock-on (di semua client)
            if (lockOnVFX != null)
                ShowLockOnVFXClientRpc();
        }
    }

    [ObserversRpc]
    private void ShowLockOnVFXClientRpc()
    {
        if (lockOnVFX != null)
            lockOnVFX.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return;

        // Abaikan collider milik ship penembak sendiri
        PlayerController hitPlayer = other.GetComponentInParent<PlayerController>();
        if (hitPlayer != null && hitPlayer.OwnerId == _ownerClientId) return;

        // Jika kena sesuatu, meledak
        if (explosionVFX != null)
            SpawnExplosionClientRpc();

        Despawn();
    }

    [ObserversRpc]
    private void SpawnExplosionClientRpc()
    {
        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
    }
}
