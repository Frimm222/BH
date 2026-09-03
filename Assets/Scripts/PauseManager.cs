using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class PauseManager : MonoBehaviour
{
    [Header("Панели")]
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject confirmExitPanel;
    public GameObject UI;
    public GameObject winPanel;
    public GameObject deathPanel;

    [Header("Кнопки")]
    public Button resumeButton;
    public Button settingsButton;
    public Button menuButton;
    public Button quitButton;
    public Button backButton;
    public Button confirmMenuButton;
    public Button cancelMenuButton;
    public Button winMenuButton;
    public Button deathMenuButton;
    public Button deathRestartButton;

    [Header("Настройки (опционально)")]
    public Slider volumeSlider;
    public Slider sensitivitySlider;
    public Toggle fullscreenToggle;
    public Dropdown resolutionDropdown;

    [Header("Звуки")]
    public AudioClip buttonClickSound;
    public AudioClip buttonHoverSound;
    private AudioSource audioSource;

    [Header("Ссылки")]
    public string menuSceneName = "Menu";
    public CameraController cameraController;

    private bool isPaused = false;
    private bool isSettingsOpen = false;
    private bool isConfirmOpen = false;

    void Start()
    {
        // Настраиваем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Подписываемся на кнопки
        resumeButton.onClick.AddListener(ResumeGame);
        settingsButton.onClick.AddListener(OpenSettings);
        menuButton.onClick.AddListener(OpenConfirmExit);
        winMenuButton.onClick.AddListener(GoToMenu);
        deathMenuButton.onClick.AddListener(GoToMenu);
        deathRestartButton.onClick.AddListener(() =>
        {
            PlaySound(buttonClickSound);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
        backButton.onClick.AddListener(CloseSettings);
        confirmMenuButton.onClick.AddListener(GoToMenu);
        cancelMenuButton.onClick.AddListener(CloseConfirmExit);

        // Настройки (если есть)
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        }

        if (resolutionDropdown != null)
        {
            SetupResolutionDropdown();
        }

        // Добавляем звуки на кнопки
        AddButtonSounds();

        // Скрываем все панели
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        confirmExitPanel.SetActive(false);
        winPanel.SetActive(false);
        deathPanel.SetActive(false);
        UI.SetActive(true);

        // Ищем CameraController если не назначен
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
        }

        Debug.Log("Pause Manager инициализирован");
    }

    void Update()
    {
        // Проверяем нажатие Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Если открыты настройки или подтверждение - закрываем их
            if (isSettingsOpen)
            {
                CloseSettings();
                return;
            }

            if (isConfirmOpen)
            {
                CloseConfirmExit();
                return;
            }

            // Иначе переключаем паузу
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    void AddButtonSounds()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(() => PlaySound(buttonClickSound));

            UnityEngine.EventSystems.EventTrigger trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
            {
                trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            UnityEngine.EventSystems.EventTrigger.Entry entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { PlaySound(buttonHoverSound); });
            trigger.triggers.Add(entry);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // ========== УПРАВЛЕНИЕ ПАУЗОЙ ==========

    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (cameraController != null)
        {
            cameraController.SetRotationEnabled(false);
        }

        // Показываем панель паузы
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
        confirmExitPanel.SetActive(false);
        UI.SetActive(false);

        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Отключаем звуки шагов (опционально)
        FootstepSystem footstep = FindObjectOfType<FootstepSystem>();
        if (footstep != null)
        {
            footstep.enabled = false;
        }

        Debug.Log("Игра на паузе");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        if (cameraController != null)
        {
            cameraController.SetRotationEnabled(true);
        }

        // Скрываем все панели
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        confirmExitPanel.SetActive(false);
        UI.SetActive(true);
        isSettingsOpen = false;
        isConfirmOpen = false;

        // Блокируем курсор обратно
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Включаем звуки шагов
        FootstepSystem footstep = FindObjectOfType<FootstepSystem>();
        if (footstep != null)
        {
            footstep.enabled = true;
        }

        Debug.Log("Игра продолжена");
    }

    // ========== НАСТРОЙКИ ==========

    void OpenSettings()
    {
        PlaySound(buttonClickSound);
        isSettingsOpen = true;
        settingsPanel.SetActive(true);
        pausePanel.SetActive(false);
        UI.SetActive(false);

        // Загружаем текущие настройки
        if (volumeSlider != null)
            volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (sensitivitySlider != null)
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
    }

    void CloseSettings()
    {
        PlaySound(buttonClickSound);
        isSettingsOpen = false;
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);

        // Сохраняем настройки
        SaveSettings();
    }

    void SaveSettings()
    {
        if (volumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", volumeSlider.value);
        if (sensitivitySlider != null)
            PlayerPrefs.SetFloat("MouseSensitivity", sensitivitySlider.value);
        if (fullscreenToggle != null)
            PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ========== ВЫХОД В МЕНЮ ==========

    void OpenConfirmExit()
    {
        PlaySound(buttonClickSound);
        isConfirmOpen = true;
        confirmExitPanel.SetActive(true);
        pausePanel.SetActive(false);
    }

    void CloseConfirmExit()
    {
        PlaySound(buttonClickSound);
        isConfirmOpen = false;
        confirmExitPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    void GoToMenu()
    {
        PlaySound(buttonClickSound);

        // Возвращаем время
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Загружаем меню
        SceneManager.LoadScene(menuSceneName);

        Debug.Log("Выход в меню");
    }

    // ========== НАСТРОЙКИ (обработчики) ==========

    void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        // Применяем к камере
        if (cameraController != null)
        {
            cameraController.SetSensitivity(value);
        }

        // Применяем через SensitivityManager если есть
        if (SensitivityManager.Instance != null)
        {
            SensitivityManager.Instance.SetSensitivity(value);
        }
    }

    void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    void OnResolutionChanged(int index)
    {
        // Настройка разрешения (если есть resolutionDropdown)
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions != null && index < resolutions.Length)
        {
            Resolution resolution = resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        }
    }

    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        Resolution[] resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRate}Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    // ========== ПУБЛИЧНЫЕ МЕТОДЫ ==========

    public bool IsPaused()
    {
        return isPaused;
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void ShowWinPanel()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (cameraController != null)
        {
            cameraController.SetRotationEnabled(false);
        }
        // Показываем панель победы
        winPanel.SetActive(true);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        confirmExitPanel.SetActive(false);
        deathPanel.SetActive(false);
        UI.SetActive(false);
        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Отключаем звуки шагов (опционально)
        FootstepSystem footstep = FindObjectOfType<FootstepSystem>();
        if (footstep != null)
        {
            footstep.enabled = false;
        }
        Debug.Log("Победа! Панель победы отображена.");
    }

    public void ShowDeathPanel()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (cameraController != null)
        {
            cameraController.SetRotationEnabled(false);
        }
        // Показываем панель смерти
        deathPanel.SetActive(true);
        winPanel.SetActive(false);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        confirmExitPanel.SetActive(false);
        UI.SetActive(false);
        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Отключаем звуки шагов (опционально)
        FootstepSystem footstep = FindObjectOfType<FootstepSystem>();
        if (footstep != null)
        {
            footstep.enabled = false;
        }
        Debug.Log("Смерть! Панель смерти отображена.");
    }

    // ========== ОЧИСТКА ==========

    void OnDestroy()
    {
        // Убеждаемся что время восстановлено при уничтожении
        Time.timeScale = 1f;
    }

    void OnApplicationQuit()
    {
        Time.timeScale = 1f;
    }
}