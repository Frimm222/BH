using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

public class EnemyAI : MonoBehaviour
{
    [Header("Тип врага")]
    public EnemyType enemyType = EnemyType.Melee;

    public enum EnemyType
    {
        Melee,
        Ranged
    }

    [Header("Настройки движения")]
    public float moveSpeed = 3f;
    public float stoppingDistance = 2f;
    public float attackRange = 1.5f;
    public float detectionRange = 15f;
    public float loseInterestRange = 25f;

    [Header("Настройки атаки (Melee)")]
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    public float attackDelay = 0.5f;

    [Header("Настройки атаки (Ranged)")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public float fireballSpeed = 15f;
    public float rangedAttackRange = 15f;
    public float rangedAttackCooldown = 2f;
    public float rangedAttackDelay = 0.5f;
    public int rangedDamage = 8;

    [Header("Настройки патрулирования")]
    public Transform[] patrolPoints;
    public float waitTime = 2f;

    [Header("Визуальные эффекты")]
    public GameObject hitEffect;
    public GameObject deathEffect;
    public AudioClip hitSound;
    public AudioClip deathSound;
    public AudioClip attackSound;
    public AudioClip shootSound;

    [Header("Настройки звука")]
    [Range(0f, 3f)]
    public float hitSoundVolume = 0.8f;
    [Range(0f, 3f)]
    public float deathSoundVolume = 1f;
    [Range(0f, 3f)]
    public float attackSoundVolume = 1f;
    [Range(0f, 3f)]
    public float shootSoundVolume = 0.7f;
    [Range(0f, 2f)]
    public float pitchMin = 0.9f;
    [Range(0f, 2f)]
    public float pitchMax = 1.1f;
    public float soundMaxDistance = 30f;
    public AudioMixerGroup sfxGroup;

    [Header("Ссылки")]
    public Transform player;
    public Animator animator;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private AudioSource audioSource;
    private Rigidbody rb;
    private Collider enemyCollider;

    private enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Dead
    }
    private EnemyState currentState = EnemyState.Patrol;

    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private Vector3 shootDirection;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        audioSource = GetComponent<AudioSource>();

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        enemyCollider = GetComponent<Collider>();
        if (enemyCollider != null)
        {
            enemyCollider.isTrigger = false;
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 🔥 НАСТРАИВАЕМ AUDIO SOURCE ДЛЯ 3D ЗВУКА
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = soundMaxDistance;
        audioSource.minDistance = 1f;
        audioSource.volume = 1f;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.isStopped = false;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (health != null)
        {
            health.OnDeath += Die;
        }

        if (enemyType == EnemyType.Ranged && firePoint == null)
        {
            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(transform);
            firePoint.localPosition = new Vector3(0, 1.5f, 0.5f);
        }

        if (enemyType == EnemyType.Ranged && agent != null)
        {
            //agent.stoppingDistance = rangedAttackRange * 0.8f;
        }
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;
        if (player == null) return;
        if (agent == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Patrol:
                PatrolUpdate(distanceToPlayer);
                break;

            case EnemyState.Chase:
                ChaseUpdate(distanceToPlayer);
                break;

            case EnemyState.Attack:
                AttackUpdate(distanceToPlayer);
                break;
        }

        UpdateAnimations();
    }

    void PatrolUpdate(float distanceToPlayer)
    {
        if (distanceToPlayer < detectionRange)
        {
            currentState = EnemyState.Chase;
            Debug.Log($"Враг ({enemyType}) заметил игрока!");
            return;
        }

        if (patrolPoints.Length == 0) return;

        if (!isWaiting)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);

            if (agent.remainingDistance < 0.5f)
            {
                isWaiting = true;
                waitTimer = waitTime;
            }
        }
        else
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
        }
    }

    void ChaseUpdate(float distanceToPlayer)
    {
        if (distanceToPlayer > loseInterestRange)
        {
            currentState = EnemyState.Patrol;
            Debug.Log("Враг потерял игрока");
            return;
        }

        float attackTriggerDistance = enemyType == EnemyType.Melee ? attackRange : rangedAttackRange;
        //Debug.Log($"Расстояние до игрока: {distanceToPlayer}, Дальность атаки: {attackTriggerDistance}, Враг видит игрока: {CanSeePlayer()}");
        if ((distanceToPlayer < attackTriggerDistance) && CanSeePlayer())
        {
            currentState = EnemyState.Attack;
            Debug.Log($"Враг ({enemyType}) атакует!");
            return;
        }

        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        agent.SetDestination(player.position);
        agent.speed = moveSpeed;
    }

    void AttackUpdate(float distanceToPlayer)
    {

        if (!CanSeePlayer())
        {
            currentState = EnemyState.Chase;
            return;
        }
        float attackTriggerDistance = enemyType == EnemyType.Melee ? attackRange : rangedAttackRange;

        if (distanceToPlayer > attackTriggerDistance * 1.5f)
        {
            currentState = EnemyState.Chase;
            agent.isStopped = false;
            return;
        }

        if (distanceToPlayer > loseInterestRange)
        {
            currentState = EnemyState.Patrol;
            agent.isStopped = false;
            return;
        }

        agent.isStopped = true;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        if (Time.time >= lastAttackTime + (enemyType == EnemyType.Melee ? attackCooldown : rangedAttackCooldown) && !isAttacking)
        {
            StartAttack();
        }
    }

    void StartAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        if (enemyType == EnemyType.Melee)
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack");
                animator.SetBool("IsAttacking", true);
            }

            // 🔥 3D ЗВУК АТАКИ
            PlaySoundAtPosition(attackSound, transform.position, attackSoundVolume, soundMaxDistance);

            Invoke(nameof(PerformMeleeAttack), attackDelay);
        }
        else
        {
            if (animator != null)
            {
                animator.SetTrigger("Shoot");
                animator.SetBool("IsAttacking", true);
            }

            // 🔥 3D ЗВУК ВЫСТРЕЛА
            PlaySoundAtPosition(shootSound, transform.position + Vector3.up * 1.5f, shootSoundVolume, soundMaxDistance * 1.5f);

            Invoke(nameof(PerformRangedAttack), rangedAttackDelay);
        }
    }

    void PerformMeleeAttack()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < attackRange * 1.2f)
        {
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
                Debug.Log($"Враг (Melee) нанес {attackDamage} урона игроку!");
            }
        }

        isAttacking = false;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        agent.isStopped = false;
        currentState = EnemyState.Chase;
    }

    void PerformRangedAttack()
    {
        if (player == null || fireballPrefab == null) return;

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 targetPosition = player.position + Vector3.up * -1.5f;
        shootDirection = (targetPosition - spawnPosition).normalized;

        GameObject fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);

        Fireball fb = fireball.GetComponent<Fireball>();
        if (fb != null)
        {
            fb.speed = fireballSpeed;
            fb.damage = rangedDamage;
            fb.SetDirection(shootDirection);
            Debug.Log($"Враг (Ranged) выпустил файрбол! Урон: {rangedDamage}");
        }
        else
        {
            Rigidbody rbFireball = fireball.GetComponent<Rigidbody>();
            if (rbFireball != null)
            {
                rbFireball.linearVelocity = shootDirection * fireballSpeed;
            }
        }

        isAttacking = false;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        agent.isStopped = true;
        currentState = EnemyState.Attack;
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;
        Debug.Log($"Проверка видимости игрока: {player.name} от врага ({enemyType})");
        Vector3 directionToPlayer = (player.position - (transform.position + Vector3.up * 8f)).normalized;
        float distance = Vector3.Distance(transform.position, player.position);

        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 6f;

        if (Physics.Raycast(rayStart, directionToPlayer, out hit, distance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                Debug.DrawLine(rayStart, player.position, Color.green, 0.5f);
                return true;
            }
        }

        Debug.DrawRay(rayStart, directionToPlayer * distance, Color.red, 0.5f);
        return false;
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat("Speed", speed);
        animator.SetBool("IsMoving", speed > 0.1f);
        animator.SetBool("IsChasing", currentState == EnemyState.Chase);

        if (enemyType == EnemyType.Ranged)
        {
            animator.SetBool("IsRanged", true);
        }

        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            animator.SetFloat("DistanceToPlayer", distance);
        }
    }

    public bool IsDead()
    {
        return currentState == EnemyState.Dead;
    }
    void Die()
    {
        currentState = EnemyState.Dead;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
        }

        // 🔥 3D ЗВУК СМЕРТИ
        PlaySoundAtPosition(deathSound, transform.position, deathSoundVolume, soundMaxDistance);

        // 🔥 3D ЗВУК ПОПАДАНИЯ (если был)
        PlaySoundAtPosition(hitSound, transform.position, hitSoundVolume, soundMaxDistance);

        // Эффект смерти
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        Debug.Log($"Враг ({enemyType}) погиб!");
    }

    // 🔥 МЕТОД ДЛЯ ВОСПРОИЗВЕДЕНИЯ 3D ЗВУКА
    void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 30f)
    {
        if (clip == null) return;

        // Создаем временный объект для звука
        GameObject soundObject = new GameObject($"3D_Sound_{clip.name}");
        soundObject.transform.position = position;

        // Добавляем AudioSource
        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();

        // 🔥 НАСТРАИВАЕМ 3D ЗВУК
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

        Debug.Log($"🎵 3D Sound: {clip.name} | Volume: {tempAudio.volume} | MaxDist: {maxDistance} | Position: {position}");

        tempAudio.Play();
        Destroy(soundObject, clip.length + 0.5f);
    }

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(transform.position, detectionRange);

    //    if (enemyType == EnemyType.Melee)
    //    {
    //        Gizmos.color = Color.red;
    //        Gizmos.DrawWireSphere(transform.position, attackRange);
    //    }
    //    else
    //    {
    //        Gizmos.color = Color.cyan;
    //        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
    //    }

    //    Gizmos.color = Color.blue;
    //    Gizmos.DrawWireSphere(transform.position, loseInterestRange);

    //    if (enemyType == EnemyType.Ranged && firePoint != null)
    //    {
    //        Gizmos.color = Color.magenta;
    //        Gizmos.DrawSphere(firePoint.position, 0.2f);
    //    }

    //    // 🔥 ВИЗУАЛИЗАЦИЯ ДАЛЬНОСТИ ЗВУКА
    //    Gizmos.color = new Color(0, 1, 0, 0.3f);
    //    Gizmos.DrawWireSphere(transform.position, soundMaxDistance);
    //}
}