using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("UI")]
    public Slider healthSlider;
    public Text healthText;

    [Header("Эффекты")]
    public GameObject deathEffect;
    public AudioClip hurtSound;
    public Animator animator;

    [Header("Регенерация")]
    public bool regenEnabled = true;
    public float regenDelay = 5f;
    public float regenRate = 5f;

    private AudioSource audioSource;
    private float lastDamageTime = 0f;
    private bool isDead = false;
    private PlayerController playerController;


    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();

        UpdateUI();
    }

    void Update()
    {
        // Регенерация
        if (regenEnabled && !isDead && currentHealth < maxHealth)
        {
            if (Time.time > lastDamageTime + regenDelay)
            {
                currentHealth += Mathf.RoundToInt(regenRate * Time.deltaTime);
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                UpdateUI();
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        lastDamageTime = Time.time;

        UpdateUI();

        // Визуальные эффекты
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        Debug.Log($"Игрок получил {damage} урона. Осталось: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateUI();

        Debug.Log($"Игрок вылечен на {amount}. Текущее здоровье: {currentHealth}");
    }

    void Die()
    {
        isDead = true;
        Debug.Log("Игрок погиб!");

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            playerController.SetMovementLocked(true);
        }

        Destroy(gameObject, 3f); // Уничтожаем игрока через 3 секунды после смерти
        // Здесь можно добавить перезагрузку уровня или экран смерти
        // Application.LoadLevel(Application.loadedLevel);
    }

    void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = (float)currentHealth / maxHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    public bool IsDead()
    {
        return isDead;
    }
}