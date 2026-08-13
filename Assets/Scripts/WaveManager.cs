using UnityEngine;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    [Header("Основные настройки")]
    public int totalWaves = 5;                    // Количество волн
    public float waveInterval = 5f;               // Интервал между волнами
    public float spawnInterval = 0.5f;            // Интервал между спавном врагов в волне

    [Header("Точки спавна")]
    public Transform[] spawnPoints;               // Точки спавна
    public float spawnRadius = 5f;                // Радиус случайного спавна вокруг точек

    [Header("Настройки волн")]
    public WaveData[] waves;                      // Данные о волнах

    [Header("UI")]
    public UnityEngine.UI.Text waveText;          // Текст для отображения волны
    public UnityEngine.UI.Text enemiesLeftText;   // Текст для отображения оставшихся врагов

    [Header("Ссылки")]
    public GameObject[] enemyPrefabs;             // Префабы врагов (по порядку)

    private int currentWaveIndex = 0;
    private int enemiesSpawnedInWave = 0;
    private int enemiesAlive = 0;
    private int totalEnemiesInWave = 0;
    private bool isWaveActive = false;
    private float waveTimer = 0f;
    private float spawnTimer = 0f;
    private bool isWaitingForNextWave = false;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    [System.Serializable]
    public class WaveData
    {
        [Header("Состав волны")]
        public EnemySpawnData[] enemyGroups;      // Группы врагов в волне

        [Header("Бонусы волны")]
        public float waveSpeedMultiplier = 1f;    // Множитель скорости врагов
        public float waveHealthMultiplier = 1f;   // Множитель здоровья врагов
    }

    [System.Serializable]
    public class EnemySpawnData
    {
        public int enemyPrefabIndex = 0;          // Индекс префаба из массива enemyPrefabs
        public int count = 3;                     // Количество врагов этого типа
        public EnemyType enemyType = EnemyType.Melee; // Тип врага
    }

    public enum EnemyType
    {
        Melee,
        Ranged
    }

    void Start()
    {
        // Если волны не заданы вручную - генерируем автоматически
        if (waves == null || waves.Length == 0)
        {
            GenerateWaves();
        }

        UpdateUI();
        StartCoroutine(StartWaveWithDelay(2f));
    }

    void Update()
    {
        if (isWaitingForNextWave)
        {
            waveTimer -= Time.deltaTime;
            if (waveTimer <= 0)
            {
                isWaitingForNextWave = false;
                StartNextWave();
            }
            return;
        }

        if (isWaveActive)
        {
            // Спавн врагов в волне
            if (enemiesSpawnedInWave < totalEnemiesInWave)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0)
                {
                    SpawnEnemy();
                    spawnTimer = spawnInterval;
                }
            }
            else
            {
                // Все враги заспавнены - проверяем, не убиты ли все
                if (enemiesAlive <= 0)
                {
                    CompleteWave();
                }
            }
        }

        // Очищаем список мертвых врагов
        spawnedEnemies.RemoveAll(e => e == null);

        UpdateUI();
    }

    public void GenerateWaves()
    {
        waves = new WaveData[totalWaves];

        for (int i = 0; i < totalWaves; i++)
        {
            waves[i] = new WaveData();

            // Количество групп врагов в волне (1-3)
            int groupCount = Mathf.Min(i % 3 + 1, 3);
            waves[i].enemyGroups = new EnemySpawnData[groupCount];

            // Базовое количество врагов (увеличивается с волнами)
            int baseCount = 3 + i * 2;

            for (int j = 0; j < groupCount; j++)
            {
                waves[i].enemyGroups[j] = new EnemySpawnData();
                waves[i].enemyGroups[j].enemyPrefabIndex = j % enemyPrefabs.Length;
                waves[i].enemyGroups[j].count = Mathf.RoundToInt(baseCount * (0.5f + j * 0.3f));
                waves[i].enemyGroups[j].enemyType = j % 2 == 0 ? EnemyType.Melee : EnemyType.Ranged;
            }

            // Множители сложности
            waves[i].waveSpeedMultiplier = 1f + i * 0.1f;
            waves[i].waveHealthMultiplier = 1f + i * 0.15f;
        }
    }

    System.Collections.IEnumerator StartWaveWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextWave();
    }

    void StartNextWave()
    {
        if (currentWaveIndex >= waves.Length)
        {
            Debug.Log("🎉 Все волны пройдены! Поздравляю!");
            ShowWaveComplete();
            return;
        }

        isWaveActive = true;
        isWaitingForNextWave = false;
        enemiesSpawnedInWave = 0;
        enemiesAlive = 0;
        totalEnemiesInWave = 0;

        WaveData currentWave = waves[currentWaveIndex];

        // Подсчет общего количества врагов в волне
        foreach (var group in currentWave.enemyGroups)
        {
            totalEnemiesInWave += group.count;
        }

        spawnTimer = spawnInterval;

        Debug.Log($"⚔️ Волна {currentWaveIndex + 1} начинается! Врагов: {totalEnemiesInWave}");
        UpdateUI();

        if (waveText != null)
        {
            waveText.text = $"Волна {currentWaveIndex + 1}/{waves.Length}";
        }
    }

    void SpawnEnemy()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("Нет точек спавна!");
            return;
        }

        WaveData currentWave = waves[currentWaveIndex];

        // Находим какую группу сейчас спавним
        int spawnedInGroups = 0;
        EnemySpawnData currentGroup = null;
        int groupIndex = 0;

        for (int i = 0; i < currentWave.enemyGroups.Length; i++)
        {
            if (enemiesSpawnedInWave < spawnedInGroups + currentWave.enemyGroups[i].count)
            {
                currentGroup = currentWave.enemyGroups[i];
                groupIndex = i;
                break;
            }
            spawnedInGroups += currentWave.enemyGroups[i].count;
        }

        if (currentGroup == null) return;

        // Выбираем префаб
        int prefabIndex = currentGroup.enemyPrefabIndex;
        if (prefabIndex >= enemyPrefabs.Length)
        {
            prefabIndex = 0;
        }

        GameObject enemyPrefab = enemyPrefabs[prefabIndex];
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"Префаб врага с индексом {prefabIndex} не назначен!");
            return;
        }

        // Выбираем точку спавна
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPos = GetRandomSpawnPosition(spawnPoint.position);

        // Создаем врага
        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // Настраиваем врага
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            // Устанавливаем тип врага
            enemyAI.enemyType = currentGroup.enemyType == EnemyType.Melee ? EnemyAI.EnemyType.Melee : EnemyAI.EnemyType.Ranged;

            // Применяем множители сложности
            if (currentWave.waveSpeedMultiplier != 1f)
            {
                enemyAI.moveSpeed *= currentWave.waveSpeedMultiplier;
            }
        }

        // Настраиваем здоровье
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.maxHealth = Mathf.RoundToInt(enemyHealth.maxHealth * currentWave.waveHealthMultiplier);
            enemyHealth.currentHealth = enemyHealth.maxHealth;
        }

        // Подписываемся на событие смерти
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += () => OnEnemyKilled(enemy);
        }

        spawnedEnemies.Add(enemy);
        enemiesAlive++;
        enemiesSpawnedInWave++;

        Debug.Log($"Спавн врага {enemiesSpawnedInWave}/{totalEnemiesInWave}");
    }

    Vector3 GetRandomSpawnPosition(Vector3 center)
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 randomPos = center + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Проверяем, что позиция на NavMesh
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(randomPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }

        return randomPos;
    }

    void OnEnemyKilled(GameObject enemy)
    {
        enemiesAlive--;
        spawnedEnemies.Remove(enemy);
        Debug.Log($"Враг убит! Осталось: {enemiesAlive}");

        UpdateUI();
    }

    void CompleteWave()
    {
        isWaveActive = false;
        currentWaveIndex++;

        Debug.Log($"✅ Волна {currentWaveIndex} завершена!");

        if (currentWaveIndex >= waves.Length)
        {
            Debug.Log("🏆 Все волны пройдены!");
            ShowWaveComplete();
            return;
        }

        // Запускаем таймер до следующей волны
        isWaitingForNextWave = true;
        waveTimer = waveInterval;
        Debug.Log($"⏳ Следующая волна через {waveInterval} секунд...");

        UpdateUI();
    }

    void ShowWaveComplete()
    {
        if (waveText != null)
        {
            waveText.text = "🏆 Все волны пройдены!";
        }
    }

    void UpdateUI()
    {
        if (enemiesLeftText != null)
        {
            enemiesLeftText.text = $"Врагов: {enemiesAlive}";
        }
    }

    // 🔥 ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ

    public int GetCurrentWave()
    {
        return currentWaveIndex;
    }

    public int GetTotalWaves()
    {
        return waves.Length;
    }

    public bool IsWaveActive()
    {
        return isWaveActive;
    }

    public int GetEnemiesAlive()
    {
        return enemiesAlive;
    }

    // Визуализация в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (spawnPoints != null)
        {
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, spawnRadius);
                }
            }
        }
    }
}