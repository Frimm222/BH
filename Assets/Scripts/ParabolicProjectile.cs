using UnityEngine;

public class ParabolicProjectile : MonoBehaviour
{
    [Header("Настройки полета (из префаба)")]
    public float speed = 15f;
    public float lifetime = 5f;
    public int damage = 15;
    public float explosionRadius = 3f;
    public float explosionForce = 500f;

    [Header("Эффекты (из префаба)")]
    public GameObject impactEffect;
    public GameObject trailEffect;
    public GameObject explosionEffect;

    [Header("Звуки (из префаба)")]
    public AudioClip fireSound;
    public AudioClip impactSound;
    public AudioClip explosionSound;

    [Header("Настройки звука")]
    [Range(0f, 3f)]
    public float fireSoundVolume = 0.5f;
    [Range(0f, 3f)]
    public float explosionSoundVolume = 1.5f;
    [Range(0f, 3f)]
    public float impactSoundVolume = 0.8f;
    [Range(0f, 2f)]
    public float pitchMin = 0.9f;
    [Range(0f, 2f)]
    public float pitchMax = 1.1f;
    public float soundMaxDistance = 50f;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private float arcHeight = 5f;
    private float progress = 0f;
    private bool isFlying = false;
    private System.Action onExplode;

    void Start()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.isTrigger = true;
        }

        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation, transform);
            Destroy(trail, lifetime);
        }

        // 🔥 3D ЗВУК ВЫСТРЕЛА
        PlaySoundAtPosition(fireSound, transform.position, fireSoundVolume, soundMaxDistance * 0.5f);

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!isFlying) return;

        progress += Time.deltaTime / (Vector3.Distance(startPosition, endPosition) / speed);
        progress = Mathf.Clamp01(progress);

        transform.position = CalculateParabolicPoint(
            startPosition,
            endPosition,
            arcHeight,
            progress
        );

        if (progress < 1f)
        {
            Vector3 nextPos = CalculateParabolicPoint(
                startPosition,
                endPosition,
                arcHeight,
                Mathf.Min(progress + 0.01f, 1f)
            );
            Vector3 direction = (nextPos - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        if (progress >= 1f)
        {
            Explode();
        }
    }

    public void SetTrajectory(Vector3 start, Vector3 end, float height, System.Action callback)
    {
        startPosition = start;
        endPosition = end;
        arcHeight = height;
        onExplode = callback;
        isFlying = true;
        progress = 0f;

        transform.position = startPosition;

        Vector3 direction = (end - start).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        Debug.Log($"Параболический снаряд запущен! Скорость: {speed}, Урон: {damage}");
    }

    public static Vector3 CalculateParabolicPoint(Vector3 start, Vector3 end, float height, float t)
    {
        Vector3 horizontal = Vector3.Lerp(start, end, t);
        float vertical = height * 4f * t * (1f - t);
        return horizontal + Vector3.up * vertical;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isFlying) return;
        if (other.isTrigger) return;
        if (other.CompareTag("Player")) return;

        Debug.Log($"Параболический снаряд столкнулся с: {other.gameObject.name}");

        if (other.CompareTag("Enemy"))
        {
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                Debug.Log($"Нанесен урон: {damage}");
            }
        }

        Explode();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isFlying) return;
        if (collision.gameObject.CompareTag("Player")) return;

        Debug.Log($"Параболический снаряд столкнулся с: {collision.gameObject.name}");

        EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }

        Explode();
    }

    void Explode()
    {
        if (!isFlying) return;
        isFlying = false;

        Debug.Log("Параболический снаряд взорвался!");

        // Урон в радиусе
        if (explosionRadius > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.transform == transform) continue;
                if (hitCollider.CompareTag("Player")) continue;

                EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                    float damageMultiplier = 1f - (distance / explosionRadius);
                    int finalDamage = Mathf.RoundToInt(damage * Mathf.Max(damageMultiplier, 0.2f));
                    enemyHealth.TakeDamage(finalDamage);
                }

                Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (hitCollider.transform.position - transform.position).normalized;
                    rb.AddForce(dir * explosionForce);
                }
            }
        }

        // 🔥 3D ЗВУК ВЗРЫВА И ПОПАДАНИЯ
        PlaySoundAtPosition(explosionSound, transform.position, explosionSoundVolume, soundMaxDistance);
        PlaySoundAtPosition(impactSound, transform.position, impactSoundVolume, soundMaxDistance * 0.6f);

        SpawnExplosionEffects();

        if (onExplode != null)
        {
            onExplode.Invoke();
        }

        // Отключаем визуал, но оставляем звук
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = false;
        }

        // Уничтожаем через время (чтобы звук успел сыграть)
        Destroy(gameObject, 3f);
    }

    void SpawnExplosionEffects()
    {
        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * (explosionRadius / 2f);
            Destroy(effect, 2f);
        }
        else if (impactEffect != null)
        {
            GameObject effect = Instantiate(impactEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    // 🔥 МЕТОД ДЛЯ ВОСПРОИЗВЕДЕНИЯ 3D ЗВУКА
    void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 50f)
    {
        if (clip == null) return;

        // Создаем временный объект для звука
        GameObject soundObject = new GameObject($"3D_Sound_{clip.name}");
        soundObject.transform.position = position;

        // Добавляем AudioSource
        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();

        // Настраиваем 3D звук
        tempAudio.clip = clip;
        tempAudio.volume = Mathf.Clamp01(volume);
        tempAudio.pitch = Random.Range(pitchMin, pitchMax);
        tempAudio.spatialBlend = 1f;
        tempAudio.dopplerLevel = 0f;
        tempAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        tempAudio.maxDistance = maxDistance;
        tempAudio.minDistance = 1f;
        tempAudio.spatialize = true;

        Debug.Log($"🎵 3D Sound: {clip.name} | Volume: {tempAudio.volume} | MaxDist: {maxDistance} | Position: {position}");

        tempAudio.Play();
        Destroy(soundObject, clip.length + 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(startPosition, 0.3f);
        Gizmos.DrawWireSphere(endPosition, 0.3f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        // Визуализация дальности звука
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, soundMaxDistance);
    }
}