using UnityEngine;
using UnityEngine.SceneManagement;

public class SensitivityManager : MonoBehaviour
{
    public static SensitivityManager Instance { get; private set; }

    [Header("Настройки")]
    [Range(0f, 1f)]
    public float sensitivityValue = 0.5f;

    public float CurrentSensitivity { get; private set; }
    public float MinSensitivity = 0.5f;
    public float MaxSensitivity = 10f;

    void Awake()
    {
        // Singleton паттерн
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSensitivity();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ApplySensitivity();
    }

    public void SetSensitivity(float value)
    {
        sensitivityValue = Mathf.Clamp01(value);
        CurrentSensitivity = Mathf.Lerp(MinSensitivity, MaxSensitivity, sensitivityValue);

        PlayerPrefs.SetFloat("MouseSensitivity", sensitivityValue);
        PlayerPrefs.Save();

        ApplySensitivity();

        Debug.Log($"Чувствительность установлена: {CurrentSensitivity:F2} (слайдер: {sensitivityValue:F2})");
    }

    public float GetSensitivity()
    {
        return CurrentSensitivity;
    }

    public float GetSensitivityValue()
    {
        return sensitivityValue;
    }

    void LoadSensitivity()
    {
        sensitivityValue = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);
        CurrentSensitivity = Mathf.Lerp(MinSensitivity, MaxSensitivity, sensitivityValue);
    }

    void ApplySensitivity()
    {
        // Применяем ко всем CameraController в сцене
        CameraController[] cameras = FindObjectsOfType<CameraController>(true);
        foreach (var cam in cameras)
        {
            cam.SetSensitivity(sensitivityValue);
        }
    }

    // Вызывается при загрузке новой сцены
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Применяем чувствительность после загрузки сцены
        Invoke(nameof(ApplySensitivity), 0.1f);
    }
}