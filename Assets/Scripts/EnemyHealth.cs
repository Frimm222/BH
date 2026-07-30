using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public int maxHealth = 50;
    public int currentHealth;

    [Header("Эффекты")]
    public GameObject hitEffect;
    public GameObject deathEffect;
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Dissolve эффект")]
    public Material dissolveMaterial;           // Ваш DissolveMat_Custom
    public Texture2D dissolveTexture;           // Текстура шума
    public float dissolveDuration = 2f;
    public float dissolveDelay = 0.3f;

    [Header("Настройки dissolve")]
    [Range(0f, 1f)]
    public float startValue = 0f;
    [Range(0f, 1f)]
    public float endValue = 1f;

    [Header("Визуальные эффекты")]
    public Material flashMaterial;
    public float flashDuration = 0.1f;

    private AudioSource audioSource;
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private bool isDead = false;
    private float dissolveAmount = 0f;
    private bool isDissolving = false;
    private Material[] dissolveMaterials;
    private bool isFlashing = false;

    public System.Action OnDeath;

    void Start()
    {
        currentHealth = maxHealth;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        FindRenderer();
    }

    void FindRenderer()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            objectRenderer = meshRenderer;
            originalMaterials = meshRenderer.materials;
            Debug.Log("✅ Найден MeshRenderer");
            return;
        }

        SkinnedMeshRenderer skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null)
        {
            objectRenderer = skinnedRenderer;
            originalMaterials = skinnedRenderer.materials;
            Debug.Log("✅ Найден SkinnedMeshRenderer");
            return;
        }

        MeshRenderer childMesh = GetComponentInChildren<MeshRenderer>();
        if (childMesh != null)
        {
            objectRenderer = childMesh;
            originalMaterials = childMesh.materials;
            Debug.Log($"✅ Найден MeshRenderer в дочернем объекте: {childMesh.gameObject.name}");
            return;
        }

        SkinnedMeshRenderer childSkinned = GetComponentInChildren<SkinnedMeshRenderer>();
        if (childSkinned != null)
        {
            objectRenderer = childSkinned;
            originalMaterials = childSkinned.materials;
            Debug.Log($"✅ Найден SkinnedMeshRenderer в дочернем объекте: {childSkinned.gameObject.name}");
            return;
        }

        Debug.LogError("❌ Renderer не найден!");
    }

    void Update()
    {
        if (isDissolving && objectRenderer != null && dissolveMaterials != null)
        {
            dissolveAmount += Time.deltaTime / dissolveDuration;
            dissolveAmount = Mathf.Clamp01(dissolveAmount);

            // 🔥 РАСЧЕТ ЗНАЧЕНИЯ
            float mappedValue = Mathf.Lerp(startValue, endValue, dissolveAmount);

            foreach (Material mat in dissolveMaterials)
            {
                if (mat != null)
                {
                    // Устанавливаем dissolve параметр
                    if (mat.HasProperty("_DissolveAmount"))
                    {
                        mat.SetFloat("_DissolveAmount", mappedValue);
                    }

                    // 🔥 ПЛАВНО УВЕЛИЧИВАЕМ СВЕЧЕНИЕ ПРИ РАСТВОРЕНИИ
                    if (mat.HasProperty("_GlowIntensity"))
                    {
                        float glow = Mathf.Lerp(0.5f, 2f, dissolveAmount);
                        mat.SetFloat("_GlowIntensity", glow);
                    }
                }
            }

            if (dissolveAmount >= 1f)
            {
                isDissolving = false;
                Debug.Log("Растворение завершено!");
                Destroy(gameObject, 0.5f);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"Враг получил {damage} урона. Осталось: {currentHealth}");

        // Визуальный эффект попадания
        FlashRed();
        SpawnHitEffect();

        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void FlashRed()
    {
        if (objectRenderer == null || isFlashing || isDead) return;

        isFlashing = true;

        if (flashMaterial != null)
        {
            Material[] currentMats = objectRenderer.materials;
            Material[] flashMats = new Material[currentMats.Length];

            for (int i = 0; i < currentMats.Length; i++)
            {
                flashMats[i] = new Material(flashMaterial);
                // Копируем текстуру
                if (currentMats[i].HasProperty("_MainTex") && flashMats[i].HasProperty("_MainTex"))
                {
                    flashMats[i].SetTexture("_MainTex", currentMats[i].GetTexture("_MainTex"));
                }
            }

            objectRenderer.materials = flashMats;
        }

        Invoke(nameof(ResetColor), flashDuration);
    }

    void ResetColor()
    {
        isFlashing = false;

        if (objectRenderer != null && !isDead && originalMaterials != null)
        {
            objectRenderer.materials = originalMaterials;
        }
    }

    void SpawnHitEffect()
    {
        if (hitEffect == null) return;

        GameObject effect = Instantiate(hitEffect, transform.position + Vector3.up * 1f, Quaternion.identity);
        Destroy(effect, 1f);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("💀 Враг уничтожен! Запускаем растворение...");

        // Отключаем всё лишнее
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Отменяем flash
        CancelInvoke(nameof(ResetColor));
        isFlashing = false;

        // Запускаем растворение
        StartDissolve();

        if (OnDeath != null)
        {
            OnDeath.Invoke();
        }
    }

    void StartDissolve()
    {
        if (dissolveMaterial == null)
        {
            Debug.LogError("❌ Dissolve Material не назначен! Используйте DissolveMat_Custom");
            Destroy(gameObject, 1f);
            return;
        }

        if (objectRenderer == null)
        {
            Debug.LogError("❌ Renderer не найден!");
            Destroy(gameObject, 1f);
            return;
        }

        // Получаем текущие материалы
        Material[] currentMats = objectRenderer.materials;
        int materialCount = currentMats.Length;

        Debug.Log($"🎯 Создаем {materialCount} материалов dissolve");

        // Создаем массив dissolve материалов
        dissolveMaterials = new Material[materialCount];

        for (int i = 0; i < materialCount; i++)
        {
            // Создаем новый материал на основе dissolve шейдера
            dissolveMaterials[i] = new Material(dissolveMaterial);

            if (currentMats[i] != null)
            {
                // 🔥 КОПИРУЕМ ОСНОВНУЮ ТЕКСТУРУ
                if (currentMats[i].HasProperty("_MainTex"))
                {
                    Texture mainTex = currentMats[i].GetTexture("_MainTex");
                    if (mainTex != null && dissolveMaterials[i].HasProperty("_MainTex"))
                    {
                        dissolveMaterials[i].SetTexture("_MainTex", mainTex);
                        Debug.Log($"✅ Скопирована текстура: {mainTex.name}");
                    }
                }

                // 🔥 КОПИРУЕМ ЦВЕТ
                if (currentMats[i].HasProperty("_Color"))
                {
                    Color color = currentMats[i].GetColor("_Color");
                    if (dissolveMaterials[i].HasProperty("_Color"))
                    {
                        dissolveMaterials[i].SetColor("_Color", color);
                    }
                }
            }

            // 🔥 УСТАНАВЛИВАЕМ ТЕКСТУРУ РАСТВОРЕНИЯ
            if (dissolveTexture != null && dissolveMaterials[i].HasProperty("_DissolveTex"))
            {
                dissolveMaterials[i].SetTexture("_DissolveTex", dissolveTexture);
                Debug.Log($"✅ Установлена текстура растворения: {dissolveTexture.name}");
            }
            else
            {
                // Если текстура не назначена - создаем procedural шум
                CreateProceduralNoise(dissolveMaterials[i]);
            }
        }

        // Применяем dissolve материалы
        objectRenderer.materials = dissolveMaterials;

        // Сбрасываем dissolve
        dissolveAmount = 0f;
        isDissolving = true;

        // Устанавливаем начальное значение
        foreach (Material mat in dissolveMaterials)
        {
            if (mat.HasProperty("_DissolveAmount"))
            {
                mat.SetFloat("_DissolveAmount", startValue);
            }
        }

        Debug.Log($"✅ Установлен _DissolveAmount = {startValue}");

        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        Debug.Log($"🚀 Растворение запущено! Длительность: {dissolveDuration} сек");
    }

    // 🔥 СОЗДАЕМ ПРОЦЕДУРНУЮ ТЕКСТУРУ ШУМА (если нет своей)
    void CreateProceduralNoise(Material mat)
    {
        if (mat == null || !mat.HasProperty("_DissolveTex")) return;

        int size = 128;
        Texture2D noiseTex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                pixels[y * size + x] = new Color(value, value, value, 1);
            }
        }

        noiseTex.SetPixels(pixels);
        noiseTex.Apply();

        mat.SetTexture("_DissolveTex", noiseTex);
        Debug.Log("✅ Создана процедурная текстура шума для растворения");
    }
}