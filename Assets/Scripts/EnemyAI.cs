using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Тип врага")]
    public EnemyType enemyType = EnemyType.Melee; // Melee или Ranged

    public enum EnemyType
    {
        Melee,   // Ближний бой
        Ranged   // Дальний бой (стреляет файрболами)
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
    //private float attackTimer = 0f;
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

        // Настройка точки выстрела для Ranged врага
        if (enemyType == EnemyType.Ranged && firePoint == null)
        {
            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(transform);
            firePoint.localPosition = new Vector3(0, 1.5f, 0.5f);
        }

        // Для Ranged врага - увеличиваем stoppingDistance
        if (enemyType == EnemyType.Ranged && agent != null)
        {
            agent.stoppingDistance = rangedAttackRange * 0.8f;
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
        if (distanceToPlayer < detectionRange && CanSeePlayer())
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

        // Проверка на переход в атаку в зависимости от типа врага
        float attackTriggerDistance = enemyType == EnemyType.Melee ? attackRange : rangedAttackRange;

        if (distanceToPlayer < attackTriggerDistance)
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
        // Проверка на расстояние для атаки
        float attackTriggerDistance = enemyType == EnemyType.Melee ? attackRange : rangedAttackRange;

        // Если игрок убежал - догоняем
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

        // Для Ranged врага - останавливаемся на дистанции
        if (enemyType == EnemyType.Ranged)
        {
            agent.isStopped = true;
        }
        else // Melee
        {
            agent.isStopped = true;
        }

        // Поворачиваемся к игроку
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // Атака
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
            // Ближняя атака
            if (animator != null)
            {
                animator.SetTrigger("Attack");
                animator.SetBool("IsAttacking", true);
            }

            if (attackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(attackSound);
            }

            Invoke(nameof(PerformMeleeAttack), attackDelay);
        }
        else // Ranged
        {
            // Дальняя атака - стрельба файрболом
            if (animator != null)
            {
                animator.SetTrigger("Shoot");
                animator.SetBool("IsAttacking", true);
            }

            if (shootSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(shootSound);
            }

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
        Debug.Log("Melee атака завершена, продолжаем преследование");
    }

    void PerformRangedAttack()
    {
        if (player == null || fireballPrefab == null) return;

        // 🎯 РАСЧЕТ НАПРАВЛЕНИЯ ВЫСТРЕЛА
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;

        // Получаем направление на игрока (с учетом его движения)
        Vector3 targetPosition = player.position + Vector3.up * -0.5f; // Цель в центр игрока
        shootDirection = (targetPosition - spawnPosition).normalized;

        // Создаем файрбол
        GameObject fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);

        // Настраиваем файрбол
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
            // Если нет скрипта Fireball - просто двигаем
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

        // Ranged враг остается на месте после выстрела
        agent.isStopped = true;
        currentState = EnemyState.Attack;
        Debug.Log("Ranged атака выполнена!");
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, player.position);

        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 1f;

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

        Debug.Log($"Враг ({enemyType}) погиб!");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Цвет для радиуса атаки в зависимости от типа
        if (enemyType == EnemyType.Melee)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, loseInterestRange);

        // Визуализация точки выстрела
        if (enemyType == EnemyType.Ranged && firePoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(firePoint.position, 0.2f);
        }
    }
}