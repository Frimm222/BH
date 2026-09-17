using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class DragonBoss : MonoBehaviour
{
    [Header("Тип босса")]
    public BossType bossType = BossType.Dragon;

    public enum BossType
    {
        Dragon
    }

    [Header("Область полёта")]
    public Transform flyAreaCenter;
    public float flyAreaRadius = 40f;
    public float flyHeight = 15f;
    public float flyHeightVariation = 5f;
    public float minDistanceFromTerrain = 8f;
    public float obstacleAvoidanceDistance = 10f;
    public LayerMask obstacleLayerMask = ~0;

    [Header("Настройки полёта")]
    public float flySpeed = 8f;
    public float chaseSpeed = 12f;
    public float rotationSpeed = 3f;

    [Header("Настройки боя")]
    public float detectionRange = 50f;
    public float attackCooldown = 3f;
    public float attackDelay = 1f;
    public float waitBetweenAttacks = 2f;

    [Header("Выбор атак (веса)")]
    [Range(0, 100)] public int fireballWeight = 50;
    [Range(0, 100)] public int groundAttackWeight = 25;
    [Range(0, 100)] public int breathWeight = 25;

    [Header("Атака 1: Фаерболы (3 подряд)")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float fireballSpeed = 20f;
    public float[] fireballDelays = new float[] { 0.4f, 0.5f, 0.6f };
    public int fireballDamage = 15;

    [Header("Атака 2: Удар по земле")]
    public float groundAttackRange = 8f;
    public int groundAttackDamage = 25;
    public float groundAttackWindUp = 0.8f;
    public float groundAttackRecovery = 1.2f;
    public ParticleSystem groundImpactEffect;
    public float groundApproachDistance = 5f;
    public float landingSpeed = 15f;
    public float groundAttackApproachTimeout = 5f;

    [Header("Атака 3: Огненное дыхание")]
    public ParticleSystem fireBreathEffect;              // 🔥 Particle System (FlameThrower)
    public Transform fireBreathPoint;                    // 🔥 Точка вылета дыхания
    public float breathDuration = 3f;
    public float breathRange = 15f;
    public float breathAngle = 45f;
    public int breathDamagePerSecond = 10;
    public float breathTickInterval = 0.5f;
    public float breathApproachDistance = 8f;
    public float breathApproachTimeout = 6f;
    public float breathLandingSpeed = 15f;
    public float breathRotationSpeed;

    [Header("Анимация появления")]
    public float appearanceDuration = 3f;
    public GameObject appearanceEffect;

    [Header("Анимация")]
    public Animator animator;

    [Header("Звуки")]
    public AudioClip roarSound;
    public AudioClip flySound;
    public AudioClip fireballSound;
    public AudioClip fireballSoundWings;
    public AudioClip groundAttackSound;
    public AudioClip breathSound;
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Настройки звука")]
    [Range(0f, 3f)] public float roarVolume = 1f;
    [Range(0f, 3f)] public float fireballVolume = 1f;
    [Range(0f, 3f)] public float groundAttackVolume = 1f;
    [Range(0f, 3f)] public float breathVolume = 0.8f;
    [Range(0f, 3f)] public float deathVolume = 1.5f;
    [Range(0f, 2f)] public float pitchMin = 0.9f;
    [Range(0f, 2f)] public float pitchMax = 1.1f;
    public float soundMaxDistance = 100f;
    public AudioMixerGroup sfxGroup;

    [Header("Ссылки")]
    public Transform player;

    // Приватные переменные
    private AudioSource audioSource;
    private Rigidbody rb;
    private Collider bossCollider;
    private EnemyHealth enemyHealth;

    // Состояния
    private enum BossState
    {
        Appearing,
        Idle,
        Flying,
        Approaching,
        Landing,
        FireballAttack,
        GroundAttack,
        BreathAttack,
        Dying,
        Dead
    }
    private BossState currentState = BossState.Appearing;

    // Таймеры
    private float lastAttackTime = 0f;
    private float flyTimer = 0f;
    private Vector3 currentFlyTarget;
    private Vector3 arenaCenter;
    private bool isAttackInProgress = false;

    // Для удара по земле
    private bool isGroundAttacking = false;
    private Vector3 landingTarget;
    private bool hasLanded = false;

    // Для дыхания
    private bool isBreathing = false;
    private float breathTimer = 0f;
    private float breathTickTimer = 0f;
    private bool isLandingForBreath = false;             // 🔥 Приземление для дыхания

    // Для подлёта
    private Vector3 approachTarget;
    private float approachTimer = 0f;
    private float approachTimeout = 5f;
    private BossState pendingAttackState = BossState.Idle;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = soundMaxDistance;
        audioSource.minDistance = 1f;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        bossCollider = GetComponent<Collider>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += Die;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (flyAreaCenter == null)
        {
            Debug.LogWarning("Fly Area Center не назначен! Создаём временный в позиции дракона.");
            GameObject center = new GameObject("FlyAreaCenter_Temp");
            center.transform.position = transform.position;
            flyAreaCenter = center.transform;
        }

        arenaCenter = flyAreaCenter.position;

        if (firePoint == null)
        {
            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(transform);
            firePoint.localPosition = new Vector3(0, 0, 2f);
        }

        // 🔥 НАСТРАИВАЕМ PARTICLE SYSTEM (ВЫЗЫВАЕМ ПОСЛЕ ИНИЦИАЛИЗАЦИИ)
        SetupFireBreathEffect();
        SetupGroundImpactEffect();

        if (fireballDelays == null || fireballDelays.Length == 0)
        {
            fireballDelays = new float[] { 0.4f, 0.5f, 0.6f };
        }

        StartCoroutine(AppearanceSequence());
    }

    // 🔥 НАСТРОЙКА PARTICLE SYSTEM
    void SetupFireBreathEffect()
    {
        if (fireBreathEffect == null)
        {
            Debug.LogError("❌ Fire Breath Effect НЕ НАЗНАЧЕН! Перетащите FlameThrower в поле.");
            return;
        }

        Debug.Log($"🔍 Настраиваем Particle System: {fireBreathEffect.gameObject.name}");

        // 🔥 ПРОВЕРЯЕМ АКТИВНОСТЬ
        if (!fireBreathEffect.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("⚠️ Particle System неактивен, активируем...");
            fireBreathEffect.gameObject.SetActive(true);
        }

        // 🔥 ПОЛУЧАЕМ МОДУЛЬ ПРАВИЛЬНО
        var main = fireBreathEffect.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // Останавливаем и очищаем
        fireBreathEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 🔥 ПРОВЕРЯЕМ ЧТО ЭФФЕКТ ВИДЕН
        Debug.Log($"✅ Particle System настроен:");
        Debug.Log($"   - Имя: {fireBreathEffect.gameObject.name}");
        Debug.Log($"   - Активен: {fireBreathEffect.gameObject.activeInHierarchy}");
        Debug.Log($"   - Позиция: {fireBreathEffect.transform.position}");
        Debug.Log($"   - Родитель: {(fireBreathEffect.transform.parent != null ? fireBreathEffect.transform.parent.name : "нет")}");
        Debug.Log($"   - Частиц: {fireBreathEffect.particleCount}");
    }
    void SetupGroundImpactEffect()
    {
        if (groundImpactEffect == null)
        {
            Debug.LogError("❌ Ground Impact Effect НЕ НАЗНАЧЕН! Перетащите GroundImpact в поле.");
            return;
        }

        Debug.Log($"🔍 Настраиваем Particle System: {groundImpactEffect.gameObject.name}");

        // 🔥 ПРОВЕРЯЕМ АКТИВНОСТЬ
        if (!groundImpactEffect.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("⚠️ Particle System неактивен, активируем...");
            groundImpactEffect.gameObject.SetActive(true);
        }

        // 🔥 ПОЛУЧАЕМ МОДУЛЬ ПРАВИЛЬНО
        var main = groundImpactEffect.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // Останавливаем и очищаем
        groundImpactEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 🔥 ПРОВЕРЯЕМ ЧТО ЭФФЕКТ ВИДЕН
        Debug.Log($"✅ Particle System настроен:");
        Debug.Log($"   - Имя: {groundImpactEffect.gameObject.name}");
        Debug.Log($"   - Активен: {groundImpactEffect.gameObject.activeInHierarchy}");
        Debug.Log($"   - Позиция: {groundImpactEffect.transform.position}");
        Debug.Log($"   - Родитель: {(groundImpactEffect.transform.parent != null ? groundImpactEffect.transform.parent.name : "нет")}");
        Debug.Log($"   - Частиц: {groundImpactEffect.particleCount}");
    }

    void Update()
    {
        if (currentState == BossState.Dead || currentState == BossState.Dying) return;
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (isBreathing && fireBreathEffect != null)
        {
            if (Time.frameCount % 30 == 0) // Каждые 30 кадров
            {
                Debug.Log($"🔥 Breath Debug: IsPlaying={fireBreathEffect.isPlaying}, " +
                          $"Particles={fireBreathEffect.particleCount}, " +
                          $"Position={fireBreathEffect.transform.position}, " +
                          $"Active={fireBreathEffect.gameObject.activeInHierarchy}");
            }
        }
        switch (currentState)
        {
            case BossState.Appearing:
                break;

            case BossState.Idle:
                IdleUpdate(distanceToPlayer);
                break;

            case BossState.Flying:
                FlyingUpdate(distanceToPlayer);
                break;

            case BossState.Approaching:
                ApproachingUpdate(distanceToPlayer);
                break;

            case BossState.Landing:
                LandingUpdate(distanceToPlayer);
                break;

            case BossState.FireballAttack:
                AttackUpdate(distanceToPlayer);
                break;

            case BossState.GroundAttack:
                GroundAttackUpdate(distanceToPlayer);
                break;

            case BossState.BreathAttack:
                BreathAttackUpdate(distanceToPlayer);
                break;
        }
        UpdateSounds();
        UpdateAnimations();
        AvoidTerrain();
    }

    void UpdateSounds()
    {
        if (currentState == BossState.Flying || currentState == BossState.Approaching || currentState == BossState.Idle)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = flySound;
                audioSource.volume = 1f;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying && audioSource.clip == flySound)
            {
                audioSource.Stop();
            }
        }
    }

    // ========== ПОЯВЛЕНИЕ ==========
    IEnumerator AppearanceSequence()
    {
        Debug.Log("🐉 Дракон появляется!");

        if (appearanceEffect != null)
        {
            Instantiate(appearanceEffect, transform.position, Quaternion.identity);
        }

        if (animator != null)
        {
            animator.SetTrigger("Appear");
        }

        audioSource.clip = roarSound;
        audioSource.volume = 1f;
        audioSource.loop = false;
        audioSource.Play();

        yield return new WaitForSeconds(appearanceDuration);

        Debug.Log("✅ Дракон появился! Начинаем бой.");

        currentState = BossState.Idle;
        flyTimer = 0f;
        lastAttackTime = Time.time;
    }

    // ========== IDLE ==========
    void IdleUpdate(float distanceToPlayer)
    {
        if (Time.time < lastAttackTime + waitBetweenAttacks) return;

        if (distanceToPlayer < detectionRange)
        {
            int attackIndex = ChooseRandomAttack();
            StartAttack(attackIndex);
        }
        else
        {
            currentState = BossState.Flying;
            PickRandomFlyTarget();
        }
    }

    int ChooseRandomAttack()
    {
        int totalWeight = fireballWeight + groundAttackWeight + breathWeight;

        if (totalWeight <= 0)
        {
            return 0;
        }

        int randomValue = Random.Range(0, totalWeight);

        if (randomValue < fireballWeight)
        {
            return 0;
        }
        else if (randomValue < fireballWeight + groundAttackWeight)
        {
            return 1;
        }
        else
        {
            return 2;
        }
    }

    void StartAttack(int attackIndex)
    {
        if (isAttackInProgress) return;

        switch (attackIndex)
        {
            case 0:
                StartFireballAttack();
                break;
            case 1:
                StartGroundAttackWithApproach();
                break;
            case 2:
                StartBreathAttackWithApproach();
                break;
        }
    }

    // ========== ПОЛЁТ ==========
    void FlyingUpdate(float distanceToPlayer)
    {
        flyTimer += Time.deltaTime;

        if (distanceToPlayer < detectionRange && Time.time >= lastAttackTime + waitBetweenAttacks)
        {
            int attackIndex = ChooseRandomAttack();
            StartAttack(attackIndex);
            return;
        }

        Vector3 direction = (currentFlyTarget - transform.position).normalized;
        Vector3 move = direction * flySpeed * Time.deltaTime;

        if (Physics.Raycast(transform.position, direction, obstacleAvoidanceDistance, obstacleLayerMask))
        {
            Vector3 avoidance = Vector3.Cross(direction, Vector3.up).normalized;
            move += avoidance * flySpeed * Time.deltaTime;
        }

        transform.position += move;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        if (Vector3.Distance(transform.position, currentFlyTarget) < 3f || flyTimer > 6f)
        {
            PickRandomFlyTarget();
            flyTimer = 0f;
        }
    }

    void PickRandomFlyTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * flyAreaRadius;
        Vector3 target = arenaCenter + new Vector3(randomCircle.x, 0, randomCircle.y);
        target.y = arenaCenter.y + flyHeight + Random.Range(-flyHeightVariation, flyHeightVariation);

        RaycastHit hit;
        if (Physics.Raycast(target + Vector3.up * 50f, Vector3.down, out hit, 100f, obstacleLayerMask))
        {
            float minY = hit.point.y + minDistanceFromTerrain;
            if (target.y < minY) target.y = minY;
        }

        currentFlyTarget = target;
    }

    // ========== ПОДЛЁТ ==========
    void StartApproaching(BossState attackState, float targetDistance, float timeout)
    {
        currentState = BossState.Approaching;
        pendingAttackState = attackState;
        approachTimer = 0f;
        approachTimeout = timeout;

        if (player != null)
        {
            Vector3 directionToPlayer = (transform.position - player.position).normalized;
            directionToPlayer.y = 0;

            approachTarget = player.position + directionToPlayer * targetDistance;

            // 🔥 ВЫСОТА: для удара и дыхания - над землёй
            if (attackState == BossState.GroundAttack || attackState == BossState.BreathAttack)
            {
                RaycastHit hit;
                if (Physics.Raycast(approachTarget + Vector3.up * 50f, Vector3.down, out hit, 100f, obstacleLayerMask))
                {
                    approachTarget.y = hit.point.y + 2f;
                }
                else
                {
                    approachTarget.y = player.position.y + 2f;
                }
            }
            else
            {
                approachTarget.y = arenaCenter.y + flyHeight;
            }

            Debug.Log($"🐉 Дракон подлетает для {attackState}: цель {approachTarget}");
        }
    }

    void ApproachingUpdate(float distanceToPlayer)
    {
        approachTimer += Time.deltaTime;

        if (approachTimer > approachTimeout)
        {
            Debug.LogWarning($"⏰ Таймаут подлёта! Возвращаемся в Idle");
            isAttackInProgress = false;
            currentState = BossState.Idle;
            lastAttackTime = Time.time;
            return;
        }

        Vector3 direction = (approachTarget - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, approachTarget);

        float currentSpeed = chaseSpeed;
        if (distanceToTarget < 5f)
        {
            currentSpeed = chaseSpeed * 0.5f;
        }

        transform.position += direction * currentSpeed * Time.deltaTime;

        // Поворачиваемся к игроку
        if (player != null)
        {
            Vector3 lookDirection = (player.position - transform.position);
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        if (distanceToTarget < 1.5f)
        {
            Debug.Log($"✅ Дракон прибыл для атаки: {pendingAttackState}");

            if (pendingAttackState == BossState.GroundAttack)
            {
                StartLanding(false);
            }
            else if (pendingAttackState == BossState.BreathAttack)
            {
                // 🔥 ДЛЯ ДЫХАНИЯ ТОЖЕ ПРИЗЕМЛЯЕМСЯ
                StartLanding(true);
            }
        }
    }

    // ========== ПРИЗЕМЛЕНИЕ ==========
    void StartLanding(bool forBreath)
    {
        currentState = BossState.Landing;
        hasLanded = false;
        isLandingForBreath = forBreath;

        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out hit, 50f, obstacleLayerMask))
        {
            landingTarget = hit.point;
            landingTarget.y = hit.point.y + 1f;
        }
        else
        {
            landingTarget = transform.position;
            landingTarget.y = player.position.y + 1f;
        }

        Debug.Log($"🛬 Дракон приземляется для {(forBreath ? "дыхания" : "удара")} в точку: {landingTarget}");
    }

    void LandingUpdate(float distanceToPlayer)
    {
        float distanceToLanding = Vector3.Distance(transform.position, landingTarget);

        if (distanceToLanding > 0.5f)
        {
            Vector3 direction = (landingTarget - transform.position).normalized;
            float speed = isLandingForBreath ? breathLandingSpeed : landingSpeed;
            transform.position += direction * speed * Time.deltaTime;

            // Поворачиваемся к игроку
            if (player != null)
            {
                Vector3 lookDirection = (player.position - transform.position);
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }
            }
        }
        else
        {
            if (!hasLanded)
            {
                hasLanded = true;
                transform.position = landingTarget;

                if (isLandingForBreath)
                {
                    Debug.Log("✅ Дракон приземлился! Начинаем огненное дыхание");
                    StartCoroutine(BreathAttack());
                }
                else
                {
                    Debug.Log("✅ Дракон приземлился! Начинаем удар по земле");
                    StartCoroutine(GroundAttack());
                }
            }
        }
    }

    // ========== АТАКА 1: ФАЕРБОЛЫ ==========
    void StartFireballAttack()
    {
        if (isAttackInProgress) return;

        audioSource.clip = fireballSoundWings;
        audioSource.volume = 1f;
        audioSource.loop = false;
        audioSource.Play();

        isAttackInProgress = true;
        lastAttackTime = Time.time;
        currentState = BossState.FireballAttack;
        if (animator != null)
        {
            animator.SetTrigger("FireballAttack");
        }
        StartCoroutine(FireballAttack());
    }

    IEnumerator FireballAttack()
    {
        Debug.Log("🔥 Дракон атакует фаерболами!");

        yield return new WaitForSeconds(attackDelay);

        for (int i = 0; i < 3; i++)
        {
            ShootFireball(i + 1);
            PlaySoundAtPosition(fireballSound, firePoint.position, 1f, soundMaxDistance);

            float delay = (i < fireballDelays.Length) ? fireballDelays[i] : 0.5f;
            Debug.Log($"🔥 Фаербол {i + 1}/3 выпущен, задержка: {delay} сек");

            yield return new WaitForSeconds(delay);
        }

        //yield return new WaitForSeconds(0.5f);

        EndAttack();
    }

    void ShootFireball(int index)
    {
        if (fireballPrefab == null || player == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 2f;
        Vector3 targetPos = player.position + Vector3.up * -3.5f;
        Vector3 direction = (targetPos - spawnPos).normalized;

        GameObject fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);

        Fireball fb = fireball.GetComponent<Fireball>();
        if (fb != null)
        {
            fb.speed = fireballSpeed;
            fb.damage = fireballDamage;
            fb.SetDirection(direction);
        }
        else
        {
            Rigidbody rbFireball = fireball.GetComponent<Rigidbody>();
            if (rbFireball != null)
            {
                rbFireball.linearVelocity = direction * fireballSpeed;
            }
        }
    }

    // ========== АТАКА 2: УДАР ПО ЗЕМЛЕ ==========
    void StartGroundAttackWithApproach()
    {
        if (isAttackInProgress) return;

        isAttackInProgress = true;
        lastAttackTime = Time.time;

        StartApproaching(BossState.GroundAttack, groundApproachDistance, groundAttackApproachTimeout);
    }

    void GroundAttackUpdate(float distanceToPlayer)
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position);
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        if (isGroundAttacking && hasLanded)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out hit, 20f, obstacleLayerMask))
            {
                Vector3 targetPos = transform.position;
                targetPos.y = hit.point.y + 1f;
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
            }
        }
    }

    IEnumerator GroundAttack()
    {
        currentState = BossState.GroundAttack;
        isGroundAttacking = true;

        audioSource.Stop();
        audioSource.clip = groundAttackSound;
        audioSource.volume = 1f;
        audioSource.loop = false;
        audioSource.Play();

        Debug.Log("💥 Дракон готовится к удару по земле!");

        if (animator != null)
        {
            animator.SetTrigger("GroundAttackWindUp");
        }

        yield return new WaitForSeconds(groundAttackWindUp);

        Debug.Log("💥 Дракон наносит удар по земле!");

        if (animator != null)
        {
            animator.SetTrigger("GroundAttackHit");
        }

        if (groundImpactEffect != null)
        {
            Debug.Log($"🔥 Запускаем Particle System: {groundImpactEffect.gameObject.name}");

            // Убеждаемся что объект активен
            if (!groundImpactEffect.gameObject.activeInHierarchy)
            {
                groundImpactEffect.gameObject.SetActive(true);
            }

            // 🔥 СБРАСЫВАЕМ И ЗАПУСКАЕМ
            groundImpactEffect.Clear();
            groundImpactEffect.Play();

            // Проверяем что играет
            yield return new WaitForSeconds(0.1f);
            Debug.Log($"   - IsPlaying: {groundImpactEffect.isPlaying}");
            Debug.Log($"   - ParticleCount: {groundImpactEffect.particleCount}");
        }
        else
        {
            Debug.LogError("❌ groundImpactEffect == null в GroundAttack!");
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, groundAttackRange);
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(groundAttackDamage);
                    Debug.Log($"💥 Удар по земле нанёс {groundAttackDamage} урона!");
                }
            }
        }

        yield return new WaitForSeconds(groundAttackRecovery);

        if (groundImpactEffect != null)
        {
            groundImpactEffect.Clear();
            groundImpactEffect.Stop();
            Debug.Log("🔥 Particle System удар по земле остановлен");
        }

        isGroundAttacking = false;
        hasLanded = false;
        EndAttack();
        
    }

    // ========== АТАКА 3: ОГНЕННОЕ ДЫХАНИЕ ==========
    void StartBreathAttackWithApproach()
    {
        if (isAttackInProgress) return;

        isAttackInProgress = true;
        lastAttackTime = Time.time;

        StartApproaching(BossState.BreathAttack, breathApproachDistance, breathApproachTimeout);
    }

    void BreathAttackUpdate(float distanceToPlayer)
    {
        // 🔥 ПОВОРАЧИВАЕМСЯ К ИГРОКУ
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position);
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed * 2f);
            }
        }

        // 🔥 ПОВОРАЧИВАЕМ PARTICLE SYSTEM В СТОРОНУ ИГРОКА
        UpdateFireBreathDirection();

        if (isBreathing)
        {
            breathTickTimer += Time.deltaTime;
            if (breathTickTimer >= breathTickInterval)
            {
                breathTickTimer = 0f;
                CheckBreathDamage();
            }
        }
    }

    // 🔥 ПОВОРОТ PARTICLE SYSTEM К ИГРОКУ
    void UpdateFireBreathDirection()
    {
        if (fireBreathEffect == null || player == null) return;

        // Поворачиваем дракона горизонтально
        Vector3 directionToPlayer = (player.position - transform.position);
        directionToPlayer.y = 0;

        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed * 3f
            );
        }

        // 🔥 Поворачиваем Particle System НЕЗАВИСИМО в мировых координатах
        RotateBreathEffectToPlayer();
    }

    void RotateBreathEffectToPlayer()
    {
        if (fireBreathEffect == null || player == null) return;

        // Точка, откуда вылетает пламя
        Vector3 breathPos = fireBreathPoint != null
            ? fireBreathPoint.position
            : fireBreathEffect.transform.position;

        // Направление на игрока (с учётом роста)
        Vector3 dirToPlayer = (player.position + Vector3.up * 1f) - breathPos;

        if (dirToPlayer.sqrMagnitude < 0.01f) return;

        // 🔥 ПРИНУДИТЕЛЬНЫЙ МИРОВОЙ ПОВОРОТ
        Quaternion targetWorldRotation = Quaternion.LookRotation(dirToPlayer);

        fireBreathEffect.transform.rotation = Quaternion.Slerp(
            fireBreathEffect.transform.rotation,
            targetWorldRotation,
            Time.deltaTime * breathRotationSpeed
        );
    }

    IEnumerator BreathAttack()
    {
        currentState = BossState.BreathAttack;

        audioSource.Stop();
        audioSource.clip = breathSound;
        audioSource.volume = 1f;
        audioSource.loop = false;
        audioSource.Play();

        Debug.Log("🔥 Дракон использует огненное дыхание!");

        if (animator != null)
        {
            animator.SetTrigger("BreathAttack");
        }

        yield return new WaitForSeconds(attackDelay);

        // 🔥 ПРОВЕРЯЕМ И ЗАПУСКАЕМ PARTICLE SYSTEM
        if (fireBreathEffect != null)
        {
            Debug.Log($"🔥 Запускаем Particle System: {fireBreathEffect.gameObject.name}");

            // Убеждаемся что объект активен
            if (!fireBreathEffect.gameObject.activeInHierarchy)
            {
                fireBreathEffect.gameObject.SetActive(true);
            }

            // 🔥 СБРАСЫВАЕМ И ЗАПУСКАЕМ
            fireBreathEffect.Clear();
            fireBreathEffect.Play();

            // Проверяем что играет
            yield return new WaitForSeconds(0.1f);
            Debug.Log($"   - IsPlaying: {fireBreathEffect.isPlaying}");
            Debug.Log($"   - ParticleCount: {fireBreathEffect.particleCount}");
        }
        else
        {
            Debug.LogError("❌ fireBreathEffect == null в BreathAttack!");
        }

        isBreathing = true;
        breathTimer = 0f;
        breathTickTimer = 0f;

        while (breathTimer < breathDuration)
        {
            breathTimer += Time.deltaTime;
            yield return null;
        }

        // 🔥 ОСТАНАВЛИВАЕМ
        if (fireBreathEffect != null)
        {
            fireBreathEffect.Stop();
            Debug.Log("🔥 Particle System дыхания остановлен");
        }

        isBreathing = false;

        yield return new WaitForSeconds(0.5f);

        hasLanded = false;
        isLandingForBreath = false;
        EndAttack();
    }

    void CheckBreathDamage()
    {
        if (player == null) return;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Vector3 forward = transform.forward;
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < breathRange)
        {
            float angle = Vector3.Angle(forward, directionToPlayer);

            if (angle < breathAngle)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    int damage = Mathf.RoundToInt(breathDamagePerSecond * breathTickInterval);
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"🔥 Дыхание нанесло {damage} урона! (угол: {angle:F1}°)");
                }
            }
        }
    }

    // ========== ОБЩИЕ МЕТОДЫ ==========
    void AttackUpdate(float distanceToPlayer)
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position);
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }

    void EndAttack()
    {
        isAttackInProgress = false;
        lastAttackTime = Time.time;
        currentState = BossState.Idle;
        hasLanded = false;
        isLandingForBreath = false;

        StartCoroutine(ReturnToFlyHeight());

        Debug.Log("✅ Атака завершена, дракон возвращается в Idle");
    }

    IEnumerator ReturnToFlyHeight()
    {
        float targetY = arenaCenter.y + flyHeight;
        float timer = 0f;
        float duration = 1.5f;
        float startY = transform.position.y;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(startY, targetY, timer / duration);
            transform.position = pos;
            yield return null;
        }
    }

    // ========== ОБЛЁТ TERRAIN ==========
    void AvoidTerrain()
    {
        // 🔥 НЕ ОБЛЕТАЕМ ЗЕМЛЮ ВО ВРЕМЯ ПРИЗЕМЛЕНИЯ И АТАК
        if (currentState == BossState.Landing ||
            (currentState == BossState.GroundAttack && hasLanded) ||
            (currentState == BossState.BreathAttack && hasLanded))
        {
            return;
        }

        RaycastHit hit;
        Vector3 rayStart = transform.position;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, minDistanceFromTerrain + 5f, obstacleLayerMask))
        {
            float distanceToGround = hit.distance;
            if (distanceToGround < minDistanceFromTerrain)
            {
                float pushUp = (minDistanceFromTerrain - distanceToGround) * 0.5f;
                transform.position += Vector3.up * pushUp;
            }
        }

        Vector3 forward = transform.forward;
        if (Physics.Raycast(rayStart, forward, out hit, obstacleAvoidanceDistance, obstacleLayerMask))
        {
            Vector3 avoidance = Vector3.Cross(forward, Vector3.up).normalized;
            transform.position += avoidance * flySpeed * Time.deltaTime * 2f;
        }

        Vector3 left = -transform.right;
        if (Physics.Raycast(rayStart, left, out hit, obstacleAvoidanceDistance * 0.5f, obstacleLayerMask))
        {
            transform.position += transform.right * flySpeed * Time.deltaTime;
        }

        Vector3 right = transform.right;
        if (Physics.Raycast(rayStart, right, out hit, obstacleAvoidanceDistance * 0.5f, obstacleLayerMask))
        {
            transform.position += -transform.right * flySpeed * Time.deltaTime;
        }
    }

    // ========== АНИМАЦИИ ==========
    void UpdateAnimations()
    {
        if (animator == null) return;

        animator.SetBool("IsFlying", currentState == BossState.Flying || currentState == BossState.Approaching);
        animator.SetBool("IsLanding", currentState == BossState.Landing);
        animator.SetBool("IsFireballAttacking", currentState == BossState.FireballAttack);
        animator.SetBool("IsGroundAttacking", currentState == BossState.GroundAttack);
        animator.SetBool("IsBreathing", isBreathing);
        animator.SetFloat("Speed", flySpeed);
    }

    // ========== СМЕРТЬ ==========
    void Die()
    {
        if (currentState == BossState.Dead) return;

        currentState = BossState.Dying;
        Debug.Log("💀 Дракон погибает!");

        // Останавливаем дыхание
        if (fireBreathEffect != null)
        {
            fireBreathEffect.Stop();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (bossCollider != null)
        {
            bossCollider.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
        }

        audioSource.Stop();
        audioSource.clip = deathSound;
        audioSource.volume = 1f;
        audioSource.loop = false;
        audioSource.Play();

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        //float fallTimer = 0f;
        //float fallDuration = 2f;
        //Vector3 startPos = transform.position;
        //Vector3 groundPos = startPos;

        //RaycastHit hit;
        //if (Physics.Raycast(startPos, Vector3.down, out hit, 100f, obstacleLayerMask))
        //{
        //    groundPos = hit.point;
        //}

        //while (fallTimer < fallDuration)
        //{
        //    fallTimer += Time.deltaTime;
        //    transform.position = Vector3.Lerp(startPos, groundPos, fallTimer / fallDuration);
        //    transform.Rotate(Vector3.right, 90f * Time.deltaTime);
        //    yield return null;
        //}

        yield return new WaitForSeconds(5f);

        //float fadeTimer = 0f;
        //float fadeDuration = 2f;
        //Renderer[] renderers = GetComponentsInChildren<Renderer>();

        //while (fadeTimer < fadeDuration)
        //{
        //    fadeTimer += Time.deltaTime;
        //    float alpha = 1f - (fadeTimer / fadeDuration);

        //    foreach (var rend in renderers)
        //    {
        //        if (rend.material.HasProperty("_Color"))
        //        {
        //            Color c = rend.material.color;
        //            c.a = alpha;
        //            rend.material.color = c;
        //        }
        //    }
        //    yield return null;
        //}

        currentState = BossState.Dead;
        Destroy(gameObject);

        Debug.Log("🏆 Дракон побеждён!");
    }

    // ========== 3D ЗВУК ==========
    void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 100f)
    {
        if (clip == null) return;

        GameObject soundObject = new GameObject($"3D_Sound_{clip.name}");
        soundObject.transform.position = position;

        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
        tempAudio.clip = clip;
        tempAudio.volume = Mathf.Clamp01(volume);
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

    // ========== ПУБЛИЧНЫЕ МЕТОДЫ ==========
    public bool IsDead()
    {
        return currentState == BossState.Dead || currentState == BossState.Dying;
    }

    public bool IsAttacking()
    {
        return currentState == BossState.FireballAttack ||
               currentState == BossState.GroundAttack ||
               currentState == BossState.BreathAttack;
    }

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(transform.position, detectionRange);

    //    if (flyAreaCenter != null)
    //    {
    //        Gizmos.color = Color.cyan;
    //        Gizmos.DrawWireSphere(flyAreaCenter.position, flyAreaRadius);

    //        Gizmos.color = Color.green;
    //        Gizmos.DrawWireCube(
    //            flyAreaCenter.position + Vector3.up * flyHeight,
    //            new Vector3(flyAreaRadius * 2, 0.1f, flyAreaRadius * 2)
    //        );

    //        Gizmos.color = Color.red;
    //        Gizmos.DrawWireCube(
    //            flyAreaCenter.position + Vector3.up * minDistanceFromTerrain,
    //            new Vector3(flyAreaRadius * 2, 0.1f, flyAreaRadius * 2)
    //        );
    //    }

    //    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
    //    Gizmos.DrawWireSphere(transform.position, groundAttackRange);

    //    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
    //    Gizmos.DrawWireSphere(transform.position, breathRange);
    //}
}