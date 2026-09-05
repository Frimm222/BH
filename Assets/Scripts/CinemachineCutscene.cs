using UnityEngine;
using Unity.Cinemachine; // 🔥 ВАЖНО: новое пространство имен
using System.Collections;

public class CinemachineCutscene : MonoBehaviour
{
    [Header("Камера катсцены")]
    public CinemachineCamera cutsceneCamera;    // 🔥 Теперь CinemachineCamera

    [Header("Настройки")]
    public float duration = 5f;
    public bool startOnAwake = true;

    [Header("Персонаж")]
    public Transform player;
    public PlayerController playerController;

    [Header("UI и менеджеры")]
    public GameObject uiObject;
    public GameObject waveManager;

    private bool isPlaying = false;
    private GameObject mainCamera;
    private PlayerShooter playerShooter;

    void Start()
    {
        FindReferences();
        mainCamera = GameObject.Find("Camera");
        playerShooter = player.GetComponent<PlayerShooter>();
        if (uiObject != null) uiObject.SetActive(false);
        if (waveManager != null) waveManager.SetActive(false);

        if (startOnAwake)
        {
            PlayCutscene();
        }
    }

    void FindReferences()
    {
        if (cutsceneCamera == null)
        {
            GameObject camObj = GameObject.Find("CinemachineCamera");
            if (camObj != null)
            {
                cutsceneCamera = camObj.GetComponent<CinemachineCamera>();
                if (cutsceneCamera != null)
                    Debug.Log("✅ Найдена Cinemachine Camera");
            }
        }

        // ... остальной поиск ссылок ...
    }

    public void PlayCutscene()
    {
        if (isPlaying) return;
        if (cutsceneCamera == null)
        {
            Debug.LogError("❌ Cinemachine Camera не найдена!");
            return;
        }

        isPlaying = true;
        Debug.Log("🎬 Катсцена началась!");

        // 🔥 ПЕРЕКЛЮЧЕНИЕ КАМЕРЫ через приоритет

        GameObject.Find("Camera").GetComponent<CinemachineBrain>().enabled = true; // Включаем Cinemachine Brain на основной камере

        // Убедитесь, что у катсцены включен Priority And Channel
        // и приоритет установлен выше, чем у основной камеры
        cutsceneCamera.Priority = 20; // Высокий приоритет

        // Блокируем игрока
        if (playerController != null)
        {
            playerController.SetMovementLocked(true);
        }
        //  БЛОКИРУЕМ СТРЕЛЬБУ
        if (playerShooter != null)
        {
            playerShooter.SetShootingLocked(true);
        }

        // Отключаем вращение камеры
        CameraController camCtrl = FindObjectOfType<CameraController>();
        if (camCtrl != null)
        {
            camCtrl.SetRotationEnabled(false);
        }

        StartCoroutine(EndCutsceneAfterDelay(duration));
    }

    IEnumerator EndCutsceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndCutscene();
    }

    public void EndCutscene()
    {
        if (!isPlaying) return;
        isPlaying = false;

        Debug.Log("🎬 Катсцена завершена!");

        // 🔥 ВОЗВРАЩАЕМ КАМЕРУ (понижаем приоритет)
        if (cutsceneCamera != null)
        {
            cutsceneCamera.Priority = 0; // Низкий приоритет
        }

        // 🔥 ВОССТАНАВЛИВАЕМ ОСНОВНУЮ КАМЕРУ
        ResetMainCamera();

        // Разблокируем игрока
        if (playerController != null)
        {
            playerController.SetMovementLocked(false);
        }
        if (playerShooter != null)
        {
            playerShooter.SetShootingLocked(false);
        }
        // Включаем вращение камеры
        CameraController camCtrl = FindObjectOfType<CameraController>();
        if (camCtrl != null)
        {
            camCtrl.SetRotationEnabled(true);
            camCtrl.ForcePlayerRotation();
        }

        // Включаем UI
        if (uiObject != null)
        {
            uiObject.SetActive(true);
            Debug.Log("✅ UI включен");
        }

        if (waveManager != null)
        {
            waveManager.SetActive(true);
            Debug.Log("✅ WaveManager включен");
        }

        Debug.Log("✅ Катсцена завершена!");
        Destroy(gameObject);
    }

    // 🔥 МЕТОД ДЛЯ СБРОСА ОСНОВНОЙ КАМЕРЫ
    void ResetMainCamera()
    {
        if (mainCamera == null) return;

        Debug.Log("🔄 Сброс основной камеры...");

        // 1. Отключаем CinemachineBrain временно
        CinemachineBrain brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.enabled = false;
        }

        // 2. Отключаем все Cinemachine камеры
        CinemachineCamera[] cams = FindObjectsOfType<CinemachineCamera>();
        foreach (var cam in cams)
        {
            cam.enabled = false;
        }

        // 3. 🔥 ПРИНУДИТЕЛЬНЫЙ СБРОС ПОЗИЦИИ
        mainCamera.transform.position = Vector3.zero;
        mainCamera.transform.rotation = Quaternion.identity;

        if (mainCamera.transform.parent != null)
        {
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.identity;
        }

        // 4. Включаем CinemachineBrain обратно
        if (brain != null)
        {
            brain.enabled = true;
        }

        // 5. Включаем основную камеру
        CinemachineCamera playerCam = GameObject.Find("CM_PlayerCamera")?.GetComponent<CinemachineCamera>();
        if (playerCam != null)
        {
            playerCam.Priority = 10;
            playerCam.enabled = true;
        }

        Debug.Log($"✅ Камера сброшена на позицию: {mainCamera.transform.position}");
        Debug.Log($"✅ Камера сброшена на поворот: {mainCamera.transform.eulerAngles}");
    }
    public void SkipCutscene()
    {
        if (isPlaying)
        {
            StopAllCoroutines();
            EndCutscene();
            Debug.Log("⏭ Катсцена пропущена!");
        }
    }

    public bool IsPlaying()
    {
        return isPlaying;
    }
}