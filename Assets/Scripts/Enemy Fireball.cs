using UnityEngine;

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

    private Vector3 direction;
    private AudioSource audioSource;
    private bool hasExploded = false;
    private SphereCollider sphereCollider;
    private Rigidbody rb;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 🔥 НАСТРАИВАЕМ КОЛЛАЙДЕР
        sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            sphereCollider.isTrigger = true;
        }

        // 🔥 ДОБАВЛЯЕМ RIGIDBODY ДЛЯ КОРРЕКТНЫХ СТОЛКНОВЕНИЙ
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true; // Кинематический, чтобы двигать вручную
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
        Vector3 moveDelta = direction * speed * Time.deltaTime;
        transform.position += moveDelta;

        // Поворот
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 🔥 ПРОВЕРКА СТОЛКНОВЕНИЙ ЧЕРЕЗ RAYCAST
        CheckCollisions();
    }

    // 🔥 МЕТОД ДЛЯ ПРОВЕРКИ СТОЛКНОВЕНИЙ
    void CheckCollisions()
    {
        if (hasExploded) return;

        // Создаем луч от текущей позиции в направлении движения
        Ray ray = new Ray(transform.position, direction);
        RaycastHit hit;

        // Проверяем на расстояние, которое пролетим за кадр + небольшой запас
        float checkDistance = speed * Time.deltaTime * 1.5f;

        // 🔥 ПРОВЕРЯЕМ ВСЕ ОБЪЕКТЫ
        if (Physics.Raycast(ray, out hit, checkDistance))
        {
            // Проверяем, что это не сам файрбол
            if (hit.collider.gameObject == gameObject) return;

            // Проверяем, что это не триггер (игнорируем триггеры)
            if (hit.collider.isTrigger) return;

            // 🔥 ПРОВЕРЯЕМ ЧТО ЭТО TERRAIN ИЛИ ИГРОК
            bool isTerrain = hit.collider.CompareTag("Terrain") ||
                            hit.collider.CompareTag("Ground") ||
                            hit.collider.GetComponent<Terrain>() != null;

            bool isPlayer = hit.collider.CompareTag("Player");

            // Если это Terrain или Player - взрываемся
            if (isTerrain || isPlayer)
            {
                Debug.Log($"EnemyFireball попал в: {hit.collider.gameObject.name}, тег: {hit.collider.tag}");

                // Если это игрок - наносим урон
                //if (isPlayer)
                //{
                //    PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
                //    if (playerHealth != null)
                //    {
                //        playerHealth.TakeDamage(damage);
                //        Debug.Log($"EnemyFireball нанес {damage} урона игроку!");
                //    }
                //}

                ExplodeAtPosition(hit.point);
            }
        }
    }

    // 🔥 ПРОВЕРКА ЧЕРЕЗ TRIGGER (для объектов с коллайдерами)
    void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if (other.isTrigger) return;

        // Проверяем, что это не сам файрбол
        if (other.gameObject == gameObject) return;

        // Проверяем на Terrain
        bool isTerrain = other.CompareTag("Terrain") ||
                        other.CompareTag("Ground") ||
                        other.GetComponent<Terrain>() != null;

        bool isPlayer = other.CompareTag("Player");

        if (isTerrain || isPlayer)
        {
            Debug.Log($"EnemyFireball Trigger с: {other.gameObject.name}, тег: {other.tag}");

            //if (isPlayer)
            //{
            //    PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            //    if (playerHealth != null)
            //    {
            //        playerHealth.TakeDamage(damage);
            //        Debug.Log($"EnemyFireball нанес {damage} урона игроку!");
            //    }
            //}

            ExplodeAtPosition(transform.position);
        }
    }

    // 🔥 ПРОВЕРКА ЧЕРЕЗ COLLISION (для физических столкновений)
    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Проверяем, что это не сам файрбол
        if (collision.gameObject == gameObject) return;

        // Проверяем на Terrain
        bool isTerrain = collision.gameObject.CompareTag("Terrain") ||
                        collision.gameObject.CompareTag("Ground") ||
                        collision.gameObject.GetComponent<Terrain>() != null;

        bool isPlayer = collision.gameObject.CompareTag("Player");

        if (isTerrain || isPlayer)
        {
            Debug.Log($"EnemyFireball Collision с: {collision.gameObject.name}, тег: {collision.gameObject.tag}");

            if (isPlayer)
            {
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"EnemyFireball нанес {damage} урона игроку!");
                }
            }

            ExplodeAtPosition(collision.contacts[0].point);
        }
    }

    void ExplodeAtPosition(Vector3 explosionPosition)
    {
        if (hasExploded) return;
        hasExploded = true;

        Debug.Log($"EnemyFireball взорвался в: {explosionPosition}");

        // Наносим урон игроку в радиусе
        if (explosionRadius > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.transform == transform) continue;

                // Наносим урон игроку
                PlayerHealth player = hitCollider.GetComponent<PlayerHealth>();
                if (player != null)
                {
                    player.TakeDamage(damage);
                    Debug.Log($"Взрыв нанес {damage} урона игроку!");
                }

                // Отбрасывание объектов
                Rigidbody rbTarget = hitCollider.GetComponent<Rigidbody>();
                if (rbTarget != null)
                {
                    Vector3 dir = (hitCollider.transform.position - explosionPosition).normalized;
                    rbTarget.AddForce(dir * explosionForce);
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