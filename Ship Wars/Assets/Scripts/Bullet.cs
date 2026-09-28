using FishNet.Object;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private int damageAmount = 10;
    [SerializeField] private float destroyTime = 3f;

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Hancurkan peluru otomatis di Server setelah beberapa detik jika tidak mengenai apapun
        Invoke(nameof(DestroyBullet), destroyTime);
    }

    private void Update()
    {
        // Gerakkan peluru maju setiap frame
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Hanya Server yang berhak memproses Hit Detection (Authoritative)
        if (!IsServer) return;

        // Cek apakah peluru mengenai Player lain (karena kita memisahkan HealthManager)
        if (other.TryGetComponent<HealthManager>(out HealthManager targetHealth))
        {
            // Panggil method pengurangan HP pada target
            targetHealth.TakeDamage(damageAmount);
            DestroyBullet();
        }
        else if (other.TryGetComponent<PlayerController>(out PlayerController targetPlayer))
        {
            // Fallback jika mengenai PlayerController langsung tapi collider-nya ada di sana
            HealthManager hm = targetPlayer.GetComponent<HealthManager>();
            if (hm != null)
            {
                hm.TakeDamage(damageAmount);
                DestroyBullet();
            }
        }
    }

    private void DestroyBullet()
    {
        if (IsServer && IsSpawned)
        {
            // Despawn dari jaringan dan hancurkan objek di Server
            base.Despawn();
            Destroy(gameObject);
        }
    }
}
