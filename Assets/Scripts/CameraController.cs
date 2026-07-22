using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Настройки камеры")]
    public Transform target;           // Цель (персонаж)
    public float distance = 5f;         // Расстояние от персонажа
    public float height = 2f;           // Высота над персонажем
    public float rotationSpeed = 3f;    // Скорость вращения камеры

    [Header("Ограничения")]
    public float minYAngle = -40f;      // Минимальный угол взгляда вниз
    public float maxYAngle = 80f;       // Максимальный угол взгляда вверх

    [Header("Сглаживание")]
    public float smoothSpeed = 10f;     // Плавность движения камеры

    private float currentX = 0f;
    private float currentY = 0f;
    private Vector3 currentVelocity;

    void Start()
    {
        // Блокируем курсор для управления мышью
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Инициализируем углы камеры
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Ввод с мыши
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

        currentX += mouseX;
        currentY -= mouseY;
        currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);

        // Вычисляем позицию камеры
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 desiredPosition = target.position - (rotation * Vector3.forward * distance) + Vector3.up * height;

        // Плавно двигаем камеру
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / smoothSpeed);
        transform.LookAt(target.position + Vector3.up * height * 0.5f);
    }
}