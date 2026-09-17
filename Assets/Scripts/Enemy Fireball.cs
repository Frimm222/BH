using UnityEngine;
using UnityEngine.Audio;

public class EnemyFireball : MonoBehaviour
{
    [Header("Настройки полета")]
    public float speed = 20f;
    public float lifetime = 5f;
    public int damage = 10;
    public float explosionRadius = 3f;
    public float explosionForce = 500f;

    [Header("Эффекты")]
    public GameObject impactEffect;
    public GameObject trailEffect;
    public GameObject explosionEffect;

    [Header("Звуки")]
    public AudioClip fireSound;
    public AudioClip impactSound;
    public AudioClip explosionSound;
    public float explosionSoundMaxDistance = 50f;
    public AudioMixerGroup sfxGroup;

    private Vector3 direction;
    private AudioSource audioSource;
    private bool hasExploded = false;
    private SphereCollider sphereCollider;
    private Rigidbody rb;

    void Start()
    {
        SetupAudioSource();

        sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            sphereCollider.isTrigger = true;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation, transform);
            Destroy(trail, lifetime);
        }

        PlaySoundAtPosition(fireSound, transform.position, 0.5f, 20f);

        Destroy(gameObject, lifetime);
    }

    void SetupAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = explosionSoundMaxDistance;
        audioSource.minDistance = 1f;
        audioSource.volume = 1f;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (hasExploded) return;

        transform.position += direction * speed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        CheckForTerrainCollision();
    }

    void CheckForTerrainCollision()
    {
        if (hasExploded) return;

        Ray ray = new Ray(transform.position, direction);
        RaycastHit hit;
        float checkDistance = speed * Time.deltaTime * 1.5f;

        if (Physics.Raycast(ray, out hit, checkDistance))
        {
            if (hit.collider.gameObject == gameObject) return;
            if (hit.collider.isTrigger) return;

            bool isTerrain = hit.collider.CompareTag("Terrain") ||
                            hit.collider.CompareTag("Ground") ||
                            hit.collider.GetComponent<Terrain>() != null;

            bool isPlayer = hit.collider.CompareTag("Player");
            bool isEnemy = hit.collider.CompareTag("Enemy");

            if (isTerrain || isPlayer || isEnemy)
            {
                ExplodeAtPosition(hit.point);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if (other.isTrigger) return;
        if (other.gameObject == gameObject) return;

        bool isTerrain = other.CompareTag("Terrain") ||
                        other.CompareTag("Ground") ||
                        other.GetComponent<Terrain>() != null;

        bool isPlayer = other.CompareTag("Player");

        if (isTerrain || isPlayer)
        {
            ExplodeAtPosition(transform.position);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        if (collision.gameObject == gameObject) return;

        bool isTerrain = collision.gameObject.CompareTag("Terrain") ||
                        collision.gameObject.CompareTag("Ground") ||
                        collision.gameObject.GetComponent<Terrain>() != null;

        bool isPlayer = collision.gameObject.CompareTag("Player");

        if (isTerrain || isPlayer)
        {
            ExplodeAtPosition(collision.contacts[0].point);
        }
    }

    void ExplodeAtPosition(Vector3 explosionPosition)
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionRadius > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.transform == transform) continue;

                PlayerHealth player = hitCollider.GetComponent<PlayerHealth>();
                if (player != null)
                {
                    player.TakeDamage(damage);
                }

                Rigidbody rbTarget = hitCollider.GetComponent<Rigidbody>();
                if (rbTarget != null)
                {
                    Vector3 dir = (hitCollider.transform.position - explosionPosition).normalized;
                    rbTarget.AddForce(dir * explosionForce);
                }
            }
        }

        PlaySoundAtPosition(explosionSound, explosionPosition, 1f, explosionSoundMaxDistance);
        PlaySoundAtPosition(impactSound, explosionPosition, 0.8f, 30f);

        SpawnExplosionEffects(explosionPosition);

        Destroy(gameObject, 0.1f);
    }

    void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 50f)
    {
        if (clip == null) return;

        GameObject soundObject = new GameObject($"3D_Sound_{clip.name}");
        soundObject.transform.position = position;

        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
        tempAudio.clip = clip;
        tempAudio.volume = volume;
        tempAudio.spatialBlend = 1f;
        tempAudio.dopplerLevel = 0f;
        tempAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        tempAudio.maxDistance = maxDistance;
        tempAudio.minDistance = 1f;
        tempAudio.outputAudioMixerGroup = sfxGroup;
        tempAudio.Play();

        Destroy(soundObject, clip.length + 0.5f);
    }

    void SpawnExplosionEffects(Vector3 position)
    {
        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * (explosionRadius / 2f);
            Destroy(effect, 2f);
        }
        else if (impactEffect != null)
        {
            GameObject effect = Instantiate(impactEffect, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    public void SetDirection(Vector3 dir)
    {
        direction = dir.normalized;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(transform.position, explosionRadius);
    //}
}