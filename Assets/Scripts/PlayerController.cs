using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;

    [Header("Рывок (Dash)")]
    public KeyCode dashKey = KeyCode.LeftShift;    // Клавиша рывка
    public float dashDistance = 5f;                // Дистанция рывка
    public float dashDuration = 0.3f;              // Длительность рывка (сек)
    public float dashCooldown = 1.5f;              // Перезарядка рывка (сек)
    public float dashSpeedMultiplier = 3f;         // Множитель скорости во время рывка

    [Header("Прицеливание")]
    public KeyCode aimKey = KeyCode.Mouse1;
    public float aimSpeedMultiplier = 0.5f;

    [Header("Прыжки")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Ссылки")]
    public Transform cameraTransform;

    private CharacterController controller;
    private float verticalVelocity;
    private bool isGrounded;
    private bool isAiming;
    private Vector3 moveDirection;
    private Vector3 dashDirection;

    // Состояние рывка
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private Vector3 dashStartPosition;
    private Vector3 dashTargetPosition;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // Обновляем таймеры
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Проверка на земле
        isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        // Ввод движения
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isJumping = Input.GetButtonDown("Jump");
        bool dashInput = Input.GetKeyDown(dashKey);

        // Переключение прицеливания (удержание ПКМ)
        isAiming = Input.GetKey(aimKey);

        // ========== РАСЧЕТ НАПРАВЛЕНИЯ ДВИЖЕНИЯ ОТ КАМЕРЫ ==========
        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            moveDirection = (camForward * vertical) + (camRight * horizontal);
            moveDirection.Normalize();
        }
        else
        {
            moveDirection = new Vector3(horizontal, 0, vertical).normalized;
        }

        // ========== ОБРАБОТКА РЫВКА ==========
        // Активация рывка (только если не в рывке, перезарядка прошла и персонаж двигается)
        if (dashInput && !isDashing && dashCooldownTimer <= 0)
        {
            StartDash();
        }

        // Обновление рывка
        if (isDashing)
        {
            UpdateDash();
        }

        // ========== РАСЧЕТ СКОРОСТИ ==========
        float currentSpeed = walkSpeed;

        if (isDashing)
        {
            // Во время рывка скорость увеличена
            currentSpeed = runSpeed * dashSpeedMultiplier;
        }
        else if (isAiming)
        {
            currentSpeed = walkSpeed * aimSpeedMultiplier;
        }
        else if (moveDirection.magnitude > 0.1f)
        {
            currentSpeed = runSpeed;
        }

        // ========== ДВИЖЕНИЕ ==========
        Vector3 move;

        if (isDashing)
        {
            // При рывке игнорируем обычное движение - двигаемся строго по направлению рывка
            move = dashDirection * currentSpeed * Time.deltaTime;
        }
        else
        {
            move = moveDirection * currentSpeed * Time.deltaTime;
        }

        // ========== ГРАВИТАЦИЯ И ПРЫЖКИ ==========
        if (isJumping && isGrounded && !isDashing)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity * Time.deltaTime;

        controller.Move(move);

        // ========== ПОВОРОТ ПЕРСОНАЖА ==========
        if (!isDashing && moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // ========== ВИЗУАЛЬНАЯ ОБРАТНАЯ СВЯЗЬ ПРИЦЕЛИВАНИЯ ==========
        if (isAiming)
        {
            Vector3 aimPosition = transform.position + cameraTransform.forward * 1.5f + Vector3.up * 2f;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, aimPosition, Time.deltaTime * 5f);
        }
        else
        {
            CameraController camController = cameraTransform?.GetComponent<CameraController>();
            if (camController != null)
            {
                // Контроллер сам обновит позицию
            }
        }
    }

    // ========== МЕТОДЫ РЫВКА ==========

    private void StartDash()
    {
        isDashing = true;
        dashTimer = 0f;
        dashCooldownTimer = dashCooldown;

        // 🎯 НАПРАВЛЕНИЕ РЫВКА:
        // 1. Если персонаж двигается - рывок в направлении движения
        // 2. Если стоит - рывок в направлении взгляда камеры
        if (moveDirection.magnitude > 0.1f)
        {
            dashDirection = moveDirection.normalized;
        }
        else
        {
            // Рывок вперед от камеры (куда смотрит камера)
            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0;
                camForward.Normalize();
                dashDirection = camForward;
            }
            else
            {
                dashDirection = transform.forward;
            }
        }

        // Сохраняем начальную позицию
        dashStartPosition = transform.position;

        // Вычисляем целевую позицию (с учетом препятствий)
        Vector3 targetPos = dashStartPosition + dashDirection * dashDistance;
        dashTargetPosition = targetPos;

        Debug.Log($"Рывок активирован! Направление: {dashDirection}, Дистанция: {Vector3.Distance(dashStartPosition, targetPos)}");
    }

    private void UpdateDash()
    {
        dashTimer += Time.deltaTime;

        // Прогресс рывка (0 -> 1)
        float progress = Mathf.Clamp01(dashTimer / dashDuration);

        // Плавное движение к цели
        Vector3 newPosition = Vector3.Lerp(dashStartPosition, dashTargetPosition, progress);

        // Применяем позицию через CharacterController
        Vector3 movement = newPosition - transform.position;
        controller.Move(movement);

        // Завершаем рывок
        if (progress >= 1f)
        {
            isDashing = false;
            Debug.Log("Рывок завершен");
        }
    }

    // Визуальная индикация готовности рывка (опционально)
    private void OnGUI()
    {
        if (dashCooldownTimer > 0)
        {
            // Простая индикация перезарядки в левом верхнем углу
            GUI.Label(new Rect(10, 10, 200, 20), $"Dash CD: {dashCooldownTimer:F1}s");
        }
        else
        {
            GUI.Label(new Rect(10, 10, 200, 20), "Dash READY! (Shift)");
        }

        if (isDashing)
        {
            GUI.Label(new Rect(10, 30, 200, 20), "DASHING!");
        }
    }

    public bool IsAiming()
    {
        return isAiming;
    }

    public bool IsDashing()
    {
        return isDashing;
    }
}