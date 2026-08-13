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

    [Header("Прицеливание (только FOV)")]
    public float normalFOV = 60f;
    public float aimFOV = 40f;
    public float fovSmoothSpeed = 5f;

    [Header("Ограничения")]
    public float minYAngle = -40f;
    public float maxYAngle = 80f;

    private float currentX = 0f;
    private float currentY = 0f;
    private bool isAiming = false;
    private Camera cam;
    private float currentFOV;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }

        currentFOV = normalFOV;
        if (cam != null)
        {
            cam.fieldOfView = normalFOV;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Проверяем состояние прицеливания
        PlayerController player = target.GetComponent<PlayerController>();
        if (player != null)
        {
            isAiming = player.IsAiming();
        }

        // Ввод с мыши
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

        currentX += mouseX;
        currentY -= mouseY;
        currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);

        // 🔥 МЕНЯЕМ ТОЛЬКО FOV (без изменения позиции)
        float targetFOV = isAiming ? aimFOV : normalFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovSmoothSpeed);

        if (cam != null)
        {
            cam.fieldOfView = currentFOV;
        }

        // 🔥 ПОЗИЦИЯ КАМЕРЫ ВСЕГДА СТАБИЛЬНА (не меняется при прицеливании)
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