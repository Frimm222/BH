using UnityEngine;

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

    private Vector3 direction;
    private AudioSource audioSource;
    private bool hasExploded = false;
    private SphereCollider sphereCollider;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Добавляем коллайдер если его нет
        sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            sphereCollider.isTrigger = true; // Используем Trigger для обнаружения
        }

        // Эффект следа
        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation, transform);
            Destroy(trail, lifetime);
        }

        // Звук выстрела
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, 0.5f);
        }

        // Автоматическое уничтожение
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (hasExploded) return;

        // Движение
        transform.position += direction * speed * Time.deltaTime;

        // Поворот
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 🔥 ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА НА TERRAIN (Raycast)
        CheckForTerrainCollision();
    }

    // 🔥 МЕТОД ДЛЯ ОБНАРУЖЕНИЯ TERRAIN ЧЕРЕЗ RAYCAST
    void CheckForTerrainCollision()
    {
        if (hasExploded) return;

        // Создаем луч от текущей позиции в направлении движения
        Ray ray = new Ray(transform.position, direction);
        RaycastHit hit;

        // Проверяем на небольшом расстоянии вперед
        float checkDistance = speed * Time.deltaTime * 1.5f;

        // 🔥 ПРОВЕРЯЕМ ВСЕ ОБЪЕКТЫ (включая Terrain)
        if (Physics.Raycast(ray, out hit, checkDistance))
        {
            // Проверяем, что это не сам файрбол
            if (hit.collider.gameObject == gameObject) return;

            // Проверяем, что это не игрок
            if (hit.collider.CompareTag("Player")) return;

            // Проверяем, что это не триггер
            if (hit.collider.isTrigger) return;

            Debug.Log($"Fireball Raycast обнаружил: {hit.collider.gameObject.name}, тег: {hit.collider.tag}");

            // Любое столкновение - взрываемся
            ExplodeAtPosition(hit.point);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if (other.isTrigger) return;
        if (other.CompareTag("Player")) return;

        Debug.Log($"Fireball Trigger с: {other.gameObject.name}, тег: {other.tag}");

        // Любое столкновение - взрываемся
        ExplodeAtPosition(transform.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        if (collision.gameObject.CompareTag("Player")) return;

        Debug.Log($"Fireball Collision с: {collision.gameObject.name}, тег: {collision.gameObject.tag}");

        // Любое столкновение - взрываемся
        ExplodeAtPosition(collision.contacts[0].point);
    }

    void ExplodeAtPosition(Vector3 explosionPosition)
    {
        if (hasExploded) return;
        hasExploded = true;

        Debug.Log($"Fireball взорвался в: {explosionPosition}");

        // Наносим урон врагам в радиусе
        if (explosionRadius > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.transform == transform) continue;
                if (hitCollider.CompareTag("Player")) continue;

                // Наносим урон врагам
                EnemyHealth enemy = hitCollider.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    Debug.Log($"Урон врагу: {damage}");
                }

                // Отбрасывание объектов
                Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (hitCollider.transform.position - explosionPosition).normalized;
                    rb.AddForce(dir * explosionForce);
                }
            }
        }

        // Визуальные эффекты
        SpawnExplosionEffects(explosionPosition);

        // Звук
        if (explosionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(explosionSound, 1f);
        }
        else if (impactSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(impactSound, 0.8f);
        }

        Destroy(gameObject, 0.1f);
    }

    void SpawnExplosionEffects(Vector3 position)
    {
        if (explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * (explosionRadius / 2f);
            Destroy(effect, 1f);
        }
        else if (impactEffect != null)
        {
            GameObject effect = Instantiate(impactEffect, position, Quaternion.identity);
            Destroy(effect, 1f);
        }
    }

    public void SetDirection(Vector3 dir)
    {
        direction = dir.normalized;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        Debug.Log($"Fireball направление: {direction}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        // Визуализация raycast
        if (Application.isPlaying && direction != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, direction * speed * Time.deltaTime * 1.5f);
        }
    }
}