using UnityEngine;
using UnityEngine.Audio;

public class Fireball : MonoBehaviour
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
    public float explosionSoundMaxDistance = 50f;  // Максимальная дальность слышимости

    [SerializeField] private AudioMixerGroup sfxGroup;

    private Vector3 direction;
    private AudioSource audioSource;
    private bool hasExploded = false;
    private SphereCollider sphereCollider;
    private Rigidbody rb;

    void Start()
    {
        // 🔥 СОЗДАЕМ AUDIO SOURCE НА СНАРЯДЕ
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

        // 🔥 ЗВУК ВЫСТРЕЛА (ИЗ ПОЗИЦИИ СНАРЯДА)
        PlaySoundAtPosition(fireSound, transform.position, 0.5f, 20f);

        Destroy(gameObject, lifetime);
    }

    void SetupAudioSource()
    {
        // Создаем отдельный AudioSource для снаряда
        audioSource = gameObject.AddComponent<AudioSource>();

        // 🔥 НАСТРАИВАЕМ ДЛЯ 3D ЗВУКА
        audioSource.spatialBlend = 1f;           // Полностью 3D
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = explosionSoundMaxDistance;
        audioSource.minDistance = 1f;
        audioSource.volume = 1f;
        audioSource.playOnAwake = false;

        Debug.Log("AudioSource создан для файрбола");
    }

    void Update()
    {
        if (hasExploded) return;

        Vector3 moveDelta = direction * speed * Time.deltaTime;
        transform.position += moveDelta;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        CheckCollisions();
    }

    void CheckCollisions()
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
            bool isEnemy = hit.collider.CompareTag("Enemy");

            if (isTerrain || isEnemy)
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
        bool isEnemy = other.CompareTag("Enemy");

        if (isTerrain || isEnemy)
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
        bool isEnemy = collision.gameObject.CompareTag("Enemy");

        if (isTerrain || isEnemy)
        {
            ExplodeAtPosition(collision.contacts[0].point);
        }
    }

    void ExplodeAtPosition(Vector3 explosionPosition)
    {
        if (hasExploded) return;
        hasExploded = true;

        // Наносим урон в радиусе
        if (explosionRadius > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.transform == transform) continue;

                EnemyHealth enemy = hitCollider.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }

                Rigidbody rbTarget = hitCollider.GetComponent<Rigidbody>();
                if (rbTarget != null)
                {
                    Vector3 dir = (hitCollider.transform.position - explosionPosition).normalized;
                    rbTarget.AddForce(dir * explosionForce);
                }
            }
        }

        // 🔥 ЗВУК ВЗРЫВА (ИЗ ПОЗИЦИИ ВЗРЫВА)
        PlaySoundAtPosition(explosionSound, explosionPosition, 1f, explosionSoundMaxDistance);
        PlaySoundAtPosition(impactSound, explosionPosition, 0.8f, 30f);

        // Визуальные эффекты
        SpawnExplosionEffects(explosionPosition);

        Destroy(gameObject, 0.1f);
    }

    // 🔥 МЕТОД ДЛЯ ВОСПРОИЗВЕДЕНИЯ ЗВУКА В МИРЕ
    void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 50f, float pitchMin = 0.9f, float pitchMax = 1.1f)
    {
        if (clip == null) return;

        // Создаем временный объект
        GameObject soundObject = new GameObject($"3D_Sound_{clip.name}");
        soundObject.transform.position = position;

        // Добавляем AudioSource
        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();

        // 🔥 ПРИМЕНЯЕМ УМНОЖЕНИЕ ГРОМКОСТИ ЗДЕСЬ
        float finalVolume = volume * 2f;  // Увеличиваем в 2 раза
        finalVolume = Mathf.Clamp01(finalVolume); // Ограничиваем 0-1 для AudioSource

        tempAudio.clip = clip;
        tempAudio.volume = finalVolume * 2f;
        tempAudio.pitch = Random.Range(pitchMin, pitchMax);
        tempAudio.spatialBlend = 1f;
        tempAudio.dopplerLevel = 0f;
        tempAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        tempAudio.maxDistance = maxDistance;
        tempAudio.minDistance = 1f;
        tempAudio.spatialize = true;

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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}