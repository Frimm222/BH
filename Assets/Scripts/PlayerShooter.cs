using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("Настройки стрельбы")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float fireRate = 0.5f;
    public float fireballSpeed = 20f;

    [Header("Анимация")]
    public float shootDelay = 0.3f;          // Задержка перед вылетом снаряда (для синхронизации с анимацией)

    [Header("Управление")]
    public KeyCode fireKey = KeyCode.Mouse0;

    [Header("Эффекты")]
    public GameObject muzzleFlash;
    public AudioClip shootSound;
    public float muzzleFlashDuration = 0.1f;

    [Header("Ссылки")]
    public Transform cameraTransform;
    public Animator animator;
    public LayerMask aimLayerMask = -1;
    public float maxAimDistance = 100f;

    private float nextFireTime = 0f;
    private bool isShooting = false;
    private float shootTimer = 0f;
    private Vector3 cachedShootDirection;
    private AudioSource audioSource;
    private Camera playerCamera;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (firePoint == null)
        {
            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(transform);
            firePoint.localPosition = new Vector3(0, 1.5f, 0.5f);
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            playerCamera = Camera.main;
        }

        if (fireballPrefab == null)
        {
            Debug.LogError("Fireball Prefab не назначен в PlayerShooter!");
        }
    }

    void Update()
    {
        // Проверяем кулдаун
        if (Time.time < nextFireTime)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Mouse0)) animator.SetTrigger("Shoot");
        // Нажатие ЛКМ
        if (Input.GetKeyDown(fireKey) && !isShooting)
        {
            
            StartShoot();
        }

        // Обработка задержки вылета
        if (isShooting)
        {
            shootTimer -= Time.deltaTime;

            if (shootTimer <= 0)
            {
                // Вылетает снаряд
                FireProjectile();
                isShooting = false;

                // Кулдаун
                nextFireTime = Time.time + fireRate;

                // Сбрасываем анимацию через небольшую задержку
                Invoke(nameof(ResetAnimation), 0.3f);
            }
        }
    }

    void StartShoot()
    {
        isShooting = true;
        shootTimer = shootDelay;

        // Сохраняем направление выстрела
        cachedShootDirection = GetShootDirection();

        // Запускаем анимацию броска
        if (animator != null)
        {
            animator.SetTrigger("Shoot");
            animator.SetBool("IsFiring", true);
        }

        // Звук броска (опционально)
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, 0.7f);
        }

        Debug.Log($"Бросок! Снаряд вылетит через {shootDelay} сек");
    }

    void FireProjectile()
    {
        if (fireballPrefab == null || cameraTransform == null)
        {
            Debug.LogWarning("Не хватает ссылок для стрельбы!");
            return;
        }

        // Позиция вылета
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;

        // Создаем снаряд
        GameObject fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);

        Fireball fb = fireball.GetComponent<Fireball>();
        if (fb != null)
        {
            fb.speed = fireballSpeed;
            fb.SetDirection(cachedShootDirection);
        }
        else
        {
            Rigidbody rb = fireball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = cachedShootDirection * fireballSpeed;
            }
        }

        // Вспышка выстрела
        SpawnMuzzleFlash();

        Debug.Log($"Снаряд вылетел! Направление: {cachedShootDirection}");
    }

    void ResetAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsFiring", false);
        }
    }

    Vector3 GetShootDirection()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                return transform.forward;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxAimDistance, aimLayerMask))
        {
            Vector3 startPoint = firePoint != null ? firePoint.position : transform.position;
            Vector3 direction = (hit.point - startPoint).normalized;

            Debug.DrawLine(startPoint, hit.point, Color.green, 1f);
            return direction;
        }
        else
        {
            Vector3 direction = ray.direction.normalized;
            Debug.DrawRay(ray.origin, direction * maxAimDistance, Color.yellow, 1f);
            return direction;
        }
    }

    void SpawnMuzzleFlash()
    {
        if (muzzleFlash == null) return;

        Vector3 pos = firePoint != null ? firePoint.position : transform.position;
        Quaternion rot = Quaternion.LookRotation(cachedShootDirection);

        GameObject flash = Instantiate(muzzleFlash, pos, rot);
        Destroy(flash, muzzleFlashDuration);
    }
}