using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Настройки камеры")]
    public Transform target;
    public float distance = 5f;
    public float height = 2f;
    public float rotationSpeed = 3f;

    [Header("Смещение (из-за плеча)")]
    public float shoulderOffset = 1.2f;
    public float lookHeight = 1.5f;

    [Header("Ограничения")]
    public float minYAngle = -40f;
    public float maxYAngle = 80f;

    [Header("Чувствительность")]
    public float baseSensitivity = 3f;
    private float currentSensitivity = 3f;

    [Header("Начальные углы")]
    public float defaultXAngle = 0f;
    public float defaultYAngle = 20f;
    public bool syncPlayerRotation = true;

    private float currentX = 0f;
    private float currentY = 0f;
    private bool isRotationEnabled = true;
    private bool isSyncEnabled = true; // 🔥 Флаг для синхронизации

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target != null)
        {
            currentX = defaultXAngle;
            currentY = defaultYAngle;

            Vector3 angles = transform.eulerAngles;
            if (angles != Vector3.zero)
            {
                currentX = angles.y;
                currentY = angles.x;
            }
        }

        LoadSensitivity();

        // Принудительная синхронизация при старте
        if (syncPlayerRotation && target != null)
        {
            Vector3 camDir = transform.forward;
            camDir.y = 0;
            camDir.Normalize();
            if (camDir != Vector3.zero)
            {
                target.rotation = Quaternion.LookRotation(camDir);
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 🔥 ВРАЩЕНИЕ КАМЕРЫ ТОЛЬКО ЕСЛИ РАЗРЕШЕНО
        if (isRotationEnabled)
        {
            float mouseX = Input.GetAxis("Mouse X") * currentSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * currentSensitivity;

            currentX += mouseX;
            currentY -= mouseY;
            currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);
        }

        // 🔥 СИНХРОНИЗАЦИЯ ПЕРСОНАЖА ТОЛЬКО ЕСЛИ РАЗРЕШЕНА
        if (syncPlayerRotation && isSyncEnabled && isRotationEnabled)
        {
            SyncPlayerRotation();
        }

        // Позиция камеры
        Vector3 lookTarget = target.position + Vector3.up * lookHeight;

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 dir = rotation * Vector3.forward;
        Vector3 rightOffset = rotation * Vector3.right * shoulderOffset;

        Vector3 desiredPosition = target.position
                                - dir * distance
                                + Vector3.up * height
                                + rightOffset;

        transform.position = desiredPosition;
        transform.LookAt(lookTarget);
    }

    void SyncPlayerRotation()
    {
        if (target == null) return;

        Vector3 cameraDirection = transform.forward;
        cameraDirection.y = 0;
        cameraDirection.Normalize();

        if (cameraDirection != Vector3.zero)
        {
            // Мгновенная синхронизация
            target.rotation = Quaternion.LookRotation(cameraDirection);
        }
    }

    // 🔥 МЕТОД ДЛЯ ОТКЛЮЧЕНИЯ СИНХРОНИЗАЦИИ (для катсцен)
    public void SetSyncEnabled(bool enabled)
    {
        isSyncEnabled = enabled;
        Debug.Log($"Синхронизация персонажа {(enabled ? "включена" : "отключена")}");
    }

    // 🔥 МЕТОД ДЛЯ ПРИНУДИТЕЛЬНОГО СБРОСА ПОВОРОТА ПЕРСОНАЖА
    public void ForcePlayerRotation()
    {
        if (target == null) return;

        // Поворачиваем персонажа в направление камеры
        Vector3 cameraDirection = transform.forward;
        cameraDirection.y = 0;
        cameraDirection.Normalize();

        if (cameraDirection != Vector3.zero)
        {
            target.rotation = Quaternion.LookRotation(cameraDirection);
            Debug.Log($"Принудительный сброс поворота персонажа: {cameraDirection}");
        }
    }

    public void ResetCamera()
    {
        ResetCamera(defaultXAngle, defaultYAngle);
    }

    public void ResetCamera(float xAngle, float yAngle)
    {
        currentX = xAngle;
        currentY = yAngle;

        // После сброса камеры - синхронизируем персонажа
        if (syncPlayerRotation && target != null)
        {
            ForcePlayerRotation();
        }

        Debug.Log($"Камера сброшена на углы: X={xAngle}, Y={yAngle}");
    }

    public void SetRotationEnabled(bool enabled)
    {
        isRotationEnabled = enabled;

        if (!enabled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 🔥 ОТКЛЮЧАЕМ СИНХРОНИЗАЦИЮ КОГДА ПАУЗА
            isSyncEnabled = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // 🔥 ВКЛЮЧАЕМ СИНХРОНИЗАЦИЮ И ПРИНУДИТЕЛЬНО СИНХРОНИЗИРУЕМ
            isSyncEnabled = true;
            ForcePlayerRotation();
        }
    }

    public void SetSensitivity(float value)
    {
        float minSensitivity = 0.5f;
        float maxSensitivity = 10f;
        currentSensitivity = Mathf.Lerp(minSensitivity, maxSensitivity, value);

        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
    }

    void LoadSensitivity()
    {
        float savedValue = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);
        SetSensitivity(savedValue);
    }
}