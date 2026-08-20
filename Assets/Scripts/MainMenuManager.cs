using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    [Header("Основные панели")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject loadingPanel;

    [Header("Кнопки")]
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;
    public Button backButton;
    public Button confirmQuitButton;
    public Button cancelQuitButton;

    [Header("Настройки")]
    public Slider volumeSlider;
    public Slider sensitivitySlider;
    public Toggle fullscreenToggle;
    public Dropdown resolutionDropdown;

    [Header("Loading")]
    public Slider loadingProgressBar;
    public Text loadingText;
    public string gameSceneName = "Game";

    [Header("Звуки")]
    public AudioClip buttonClickSound;
    public AudioClip buttonHoverSound;
    private AudioSource audioSource;

    private Resolution[] resolutions;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Подписываемся на события
        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(CloseSettings);

        // Настройки
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        AddButtonSounds();
        LoadSettings();
        SetupResolutionDropdown();

        ShowMainMenu();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

    void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    void StartGame()
    {
        PlaySound(buttonClickSound);
        StartCoroutine(LoadGameScene());
    }

    IEnumerator LoadGameScene()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        loadingPanel.SetActive(true);

        loadingText.text = "Загрузка...";

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            loadingProgressBar.value = progress;
            loadingText.text = $"Загрузка... {Mathf.RoundToInt(progress * 100)}%";
            yield return null;
        }

        loadingProgressBar.value = 1f;
        loadingText.text = "Нажмите любую клавишу...";

        yield return new WaitUntil(() => Input.anyKeyDown);

        asyncLoad.allowSceneActivation = true;
        yield return new WaitForSeconds(0.5f);
    }

    void OpenSettings()
    {
        PlaySound(buttonClickSound);
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    void CloseSettings()
    {
        PlaySound(buttonClickSound);
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        SaveSettings();
    }

    void QuitGame()
    {
        PlaySound(buttonClickSound);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    void SetupResolutionDropdown()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
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

    void LoadSettings()
    {
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        OnVolumeChanged(volumeSlider.value);
        OnSensitivityChanged(sensitivitySlider.value);
        OnFullscreenToggled(fullscreenToggle.isOn);
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", volumeSlider.value);
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivitySlider.value);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ========== ОБРАБОТЧИКИ НАСТРОЕК ==========

    void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        // 🔥 ПЕРЕДАЕМ ЧУВСТВИТЕЛЬНОСТЬ В КАМЕРУ (если она существует в сцене)
        // Это работает только когда мы в игре, а не в меню
        CameraController cam = FindObjectOfType<CameraController>();
        if (cam != null)
        {
            cam.SetSensitivity(value);
            Debug.Log($"Чувствительность передана в CameraController: {value:F2}");
        }
        else
        {
            Debug.Log("CameraController не найден (мы в меню или еще не загружена игровая сцена)");
        }
    }

    void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    void OnResolutionChanged(int index)
    {
        if (resolutions != null && index < resolutions.Length)
        {
            Resolution resolution = resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        }
    }

    public void SetGameSceneName(string sceneName)
    {
        gameSceneName = sceneName;
    }

    public void QuitGameWithoutConfirm()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}