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

    private float currentX = 0f;
    private float currentY = 0f;

    // 🔥 ФЛАГ ДЛЯ ОТКЛЮЧЕНИЯ ВРАЩЕНИЯ
    private bool isRotationEnabled = true;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }

        LoadSensitivity();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 🔥 ВРАЩАЕМ КАМЕРУ ТОЛЬКО ЕСЛИ РАЗРЕШЕНО
        if (isRotationEnabled)
        {
            float mouseX = Input.GetAxis("Mouse X") * currentSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * currentSensitivity;

            currentX += mouseX;
            currentY -= mouseY;
            currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);
        }

        // Позиция камеры всегда обновляется (даже на паузе)
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

    // 🔥 МЕТОД ДЛЯ ВКЛЮЧЕНИЯ/ОТКЛЮЧЕНИЯ ВРАЩЕНИЯ
    public void SetRotationEnabled(bool enabled)
    {
        isRotationEnabled = enabled;

        if (!enabled)
        {
            // Разблокируем курсор когда вращение отключено
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Блокируем курсор когда вращение включено
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log($"Вращение камеры {(enabled ? "включено" : "отключено")}");
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

    public void ResetCamera()
    {
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }
    }
}