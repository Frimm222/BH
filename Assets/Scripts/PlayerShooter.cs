using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("Настройки стрельбы")]
    public GameObject fireballPrefab;
    public GameObject aimedFireballPrefab;
    public Transform firePoint;
    public float fireRate = 0.5f;

    [Header("Прицельный файрбол (E)")]
    public float aimedChargeTime = 1.5f;
    public float maxAimDistance = 30f;
    public float arcHeight = 5f;
    public int segments = 20;
    public GameObject projectileIcon;

    [Header("Анимация")]
    public float shootDelay = 0.3f;
    public float aimedAnimationDelay = 0.8f;

    [Header("Управление")]
    public KeyCode fireKey = KeyCode.Mouse0;
    public KeyCode aimedFireKey = KeyCode.E;

    [Header("Эффекты")]
    public GameObject muzzleFlash;
    public AudioClip shootSound;
    public AudioClip chargeSound;
    public float muzzleFlashDuration = 0.1f;

    [Header("Визуализация траектории")]
    public LineRenderer trajectoryLine;
    public Color trajectoryColor = Color.yellow;

    [Header("Ссылки")]
    public Transform cameraTransform;
    public Animator animator;
    public Animator animatorUi;
    public LayerMask aimLayerMask = -1;
    public PlayerController playerController;

    // 🔥 НОВАЯ ПЕРЕМЕННАЯ: БЛОКИРОВКА СТРЕЛЬБЫ
    private bool isShootingLocked = false;

    private enum FireState
    {
        Idle,
        Charging,
        Shooting,
        AimedShooting
    }
    private FireState currentState = FireState.Idle;

    private float nextFireTime = 0f;
    private bool isCharging = false;
    private float chargeTimer = 0f;
    private Vector3 cachedShootDirection;
    private Vector3 aimedTargetPoint;
    private Vector3 aimedStartPosition;
    private Animator iconAnimator;
    private AudioSource audioSource;
    private Camera playerCamera;
    private bool isAimedShooting = false;

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

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (projectileIcon != null)
        {
            iconAnimator = projectileIcon.GetComponent<Animator>();
        }

        SetupTrajectoryLine();
    }

    void SetupTrajectoryLine()
    {
        if (trajectoryLine == null)
        {
            trajectoryLine = GetComponent<LineRenderer>();
            if (trajectoryLine == null)
            {
                trajectoryLine = gameObject.AddComponent<LineRenderer>();
            }
        }

        trajectoryLine.positionCount = segments + 1;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.05f;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = trajectoryColor;
        trajectoryLine.endColor = new Color(trajectoryColor.r, trajectoryColor.g, trajectoryColor.b, 0.3f);
        trajectoryLine.enabled = false;
    }

    void Update()
    {
        // 🔥 ПРОВЕРКА БЛОКИРОВКИ СТРЕЛЬБЫ
        if (isShootingLocked) return;

        // Обычный выстрел (ЛКМ)
        if (Input.GetKeyDown(fireKey) && Time.time >= nextFireTime && currentState == FireState.Idle)
        {
            StartShoot();
        }

        // Прицельный файрбол (удержание E)
        if (Input.GetKeyDown(aimedFireKey) && currentState == FireState.Idle && Time.time >= nextFireTime)
        {
            StartCharging();
        }

        if (Input.GetKey(aimedFireKey) && isCharging)
        {
            UpdateCharging();
        }

        if (Input.GetKeyUp(aimedFireKey) && isCharging)
        {
            ReleaseAimedFireball();
        }

        // Обновление зарядки
        if (currentState == FireState.Charging && isCharging)
        {
            chargeTimer += Time.deltaTime;

            if (chargeTimer > 0.1f)
            {
                UpdateTrajectory();
            }

            if (chargeTimer > 0.5f && chargeSound != null && !audioSource.isPlaying)
            {
                audioSource.PlayOneShot(chargeSound, 0.3f);
            }
        }
    }

    // ========== БЛОКИРОВКА СТРЕЛЬБЫ ==========

    /// <summary>
    /// Блокирует или разблокирует возможность стрельбы
    /// </summary>
    /// <param name="locked">true - стрельба заблокирована, false - разблокирована</param>
    public void SetShootingLocked(bool locked)
    {
        isShootingLocked = locked;

        // Если блокируем и идет зарядка - отменяем её
        if (locked && isCharging)
        {
            CancelCharging();
        }

        Debug.Log($"Стрельба {(locked ? "заблокирована" : "разблокирована")}");
    }

    /// <summary>
    /// Проверяет, заблокирована ли стрельба
    /// </summary>
    public bool IsShootingLocked()
    {
        return isShootingLocked;
    }

    /// <summary>
    /// Отменяет текущую зарядку прицельного файрбола
    /// </summary>
    void CancelCharging()
    {
        if (!isCharging) return;

        isCharging = false;
        trajectoryLine.enabled = false;
        currentState = FireState.Idle;

        if (iconAnimator != null)
        {
            iconAnimator.SetBool("IsCharging", false);
            iconAnimator.SetBool("Charged", false);
        }

        Debug.Log("Зарядка отменена (блокировка)");
    }

    // ========== ОБЫЧНЫЙ ВЫСТРЕЛ ==========
    void StartShoot()
    {
        currentState = FireState.Shooting;
        cachedShootDirection = GetShootDirection();

        if (animator != null)
        {
            animator.SetTrigger("Shoot");
            animator.SetBool("IsFiring", true);
        }
        if (animatorUi != null)
        {
            animatorUi.SetTrigger("Reload");
        }

        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, 0.7f);
        }

        Invoke(nameof(FireProjectile), shootDelay);
        Invoke(nameof(ResetAnimation), 0.5f);
    }

    void FireProjectile()
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning("Fireball Prefab не назначен!");
            currentState = FireState.Idle;
            return;
        }

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;

        GameObject fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);

        Fireball fb = fireball.GetComponent<Fireball>();
        if (fb != null)
        {
            fb.SetDirection(cachedShootDirection);
        }
        else
        {
            Rigidbody rb = fireball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = cachedShootDirection * 20f;
            }
        }

        SpawnMuzzleFlash();
        nextFireTime = Time.time + fireRate;
        currentState = FireState.Idle;
    }

    // ========== ПРИЦЕЛЬНЫЙ ФАЙРБОЛ ==========
    void StartCharging()
    {
        isCharging = true;
        chargeTimer = 0f;
        currentState = FireState.Charging;
        trajectoryLine.enabled = true;

        aimedTargetPoint = GetAimedTargetPoint();
        aimedStartPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;

        if (iconAnimator != null)
        {
            iconAnimator.SetBool("IsCharging", true);
        }

        Debug.Log("Начало зарядки прицельного файрбола (движение разрешено)");
    }

    void UpdateCharging()
    {
        aimedTargetPoint = GetAimedTargetPoint();
        if (iconAnimator != null)
        {
            iconAnimator.SetBool("Charged", true);
        }
    }

    void ReleaseAimedFireball()
    {
        isCharging = false;
        trajectoryLine.enabled = false;

        if (chargeTimer < 0.3f)
        {
            if (iconAnimator != null)
            {
                iconAnimator.SetBool("IsCharging", false);
            }
            currentState = FireState.Idle;
            Debug.Log("Зарядка отменена (слишком короткая)");
            return;
        }

        if (playerController != null)
        {
            playerController.SetMovementLocked(true);
        }

        currentState = FireState.AimedShooting;
        isAimedShooting = true;

        if (animator != null)
        {
            animator.SetBool("IsCharging", false);
            animator.SetTrigger("AimedShoot");
            animator.SetBool("IsFiring", true);
        }

        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, 0.7f);
        }

        Invoke(nameof(FireAimedProjectile), aimedAnimationDelay);
        Invoke(nameof(ResetAimedShoot), aimedAnimationDelay + 0.3f);

        Debug.Log($"Прицельный выстрел! Движение заблокировано на {aimedAnimationDelay + 0.3f} сек");
    }

    void FireAimedProjectile()
    {
        if (aimedFireballPrefab == null)
        {
            Debug.LogWarning("Aimed Fireball Prefab не назначен!");
            currentState = FireState.Idle;

            if (playerController != null)
            {
                playerController.SetMovementLocked(false);
            }
            return;
        }

        if (iconAnimator != null)
        {
            iconAnimator.SetBool("Charged", false);
            iconAnimator.SetBool("IsCharging", false);
        }

        aimedTargetPoint = GetAimedTargetPoint();

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;

        GameObject fireball = Instantiate(aimedFireballPrefab, spawnPosition, Quaternion.identity);

        ParabolicProjectile parabolic = fireball.GetComponent<ParabolicProjectile>();
        if (parabolic != null)
        {
            parabolic.SetTrajectory(
                spawnPosition,
                aimedTargetPoint,
                arcHeight,
                () => {
                    Debug.Log("Прицельный файрбол взорвался!");
                }
            );
        }
        else
        {
            Debug.LogWarning("На префабе прицельного файрбола нет скрипта ParabolicProjectile!");
        }

        SpawnMuzzleFlash();

        nextFireTime = Time.time + fireRate + aimedChargeTime * 0.5f;

        Debug.Log($"Прицельный файрбол выпущен!");
    }

    void ResetAimedShoot()
    {
        isAimedShooting = false;
        currentState = FireState.Idle;

        if (playerController != null)
        {
            playerController.SetMovementLocked(false);
        }

        if (animator != null)
        {
            animator.SetBool("IsFiring", false);
        }

        Debug.Log("Прицельная анимация завершена, движение разблокировано");
    }

    // ========== ПОЛУЧЕНИЕ ТОЧКИ ПРИЦЕЛА ==========
    Vector3 GetAimedTargetPoint()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                return transform.position + transform.forward * 20f;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxAimDistance, aimLayerMask))
        {
            return hit.point;
        }
        else
        {
            return ray.origin + ray.direction * maxAimDistance;
        }
    }

    // ========== ОБНОВЛЕНИЕ ТРАЕКТОРИИ ==========
    void UpdateTrajectory()
    {
        Vector3 startPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 endPos = aimedTargetPoint;

        float distance = Vector3.Distance(startPos, endPos);
        if (distance < 1f) return;

        trajectoryLine.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            Vector3 point = ParabolicProjectile.CalculateParabolicPoint(
                startPos,
                endPos,
                arcHeight,
                t
            );

            if (i > 0)
            {
                Vector3 prevPoint = trajectoryLine.GetPosition(i - 1);
                if (Physics.Linecast(prevPoint, point, aimLayerMask))
                {
                    trajectoryLine.positionCount = i;
                    break;
                }
            }

            trajectoryLine.SetPosition(i, point);
        }

        trajectoryLine.enabled = true;
    }

    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
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
            return (hit.point - startPoint).normalized;
        }
        else
        {
            return ray.direction.normalized;
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

    void ResetAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("IsFiring", false);
        }
    }

    public bool IsAimedShooting()
    {
        return isAimedShooting;
    }

    public bool IsCharging()
    {
        return isCharging;
    }
}