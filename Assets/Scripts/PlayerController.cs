using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;

    [Header("Рывок (Dash)")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashDistance = 5f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 1.5f;
    public float dashSpeedMultiplier = 3f;

    [Header("Прицеливание")]
    public KeyCode aimKey = KeyCode.Mouse1;
    public float aimSpeedMultiplier = 0.5f;

    [Header("Прыжки")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float airControl = 0.3f; // Контроль в воздухе

    [Header("Ссылки")]
    public Transform cameraTransform;
    public Animator animator;

    private CharacterController controller;
    private float verticalVelocity;
    private bool isGrounded;
    private bool isAiming;
    private Vector3 moveDirection;
    private Vector3 dashDirection;
    private bool isJumping = false;

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

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    void Update()
    {
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        bool wasGroundedPrevious = isGrounded;
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;

            if (!wasGroundedPrevious && isJumping)
            {
                isJumping = false;
                //animator.SetBool("Jump", false);
                Debug.Log("Приземлились");
            }
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isJumpingInput = Input.GetButtonDown("Jump");
        bool dashInput = Input.GetKeyDown(dashKey);

        // Анимации
        if (Input.GetKeyDown(KeyCode.W)) animator.SetBool("W", true);
        if (Input.GetKeyUp(KeyCode.W)) animator.SetBool("W", false);

        if (Input.GetKeyDown(KeyCode.S)) animator.SetBool("S", true);
        if (Input.GetKeyUp(KeyCode.S)) animator.SetBool("S", false);

        if (Input.GetKeyDown(KeyCode.A)) animator.SetBool("A", true);
        if (Input.GetKeyUp(KeyCode.A)) animator.SetBool("A", false);

        if (Input.GetKeyDown(KeyCode.D)) animator.SetBool("D", true);
        if (Input.GetKeyUp(KeyCode.D)) animator.SetBool("D", false);

        //bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        //bool isRunning = isMoving && !isAiming && !isDashing && isGrounded;

        // Базовые параметры
        //animator.SetBool("IsMoving", isMoving);
        //animator.SetBool("IsRunning", isRunning);
        //animator.SetBool("IsAiming", isAiming);
        //animator.SetBool("IsDashing", isDashing);
        animator.SetBool("IsGrounded", isGrounded);


        isAiming = Input.GetKey(aimKey);

        // ========== РАСЧЕТ НАПРАВЛЕНИЯ ДВИЖЕНИЯ ==========
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

        // ========== РЫВОК ==========
        if (dashInput && !isDashing && dashCooldownTimer <= 0)
        {
            StartDash();
        }

        if (isDashing)
        {
            UpdateDash();
        }

        // ========== СКОРОСТЬ ==========
        float currentSpeed = walkSpeed;

        if (isDashing)
        {
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
            move = dashDirection * currentSpeed * Time.deltaTime;
        }
        else
        {
            move = moveDirection * currentSpeed * Time.deltaTime;
        }

        // ========== ГРАВИТАЦИЯ ==========
        if (isJumpingInput && isGrounded && !isDashing && !isJumping)
        {
            PerformJump();
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity * Time.deltaTime;

        controller.Move(move);

        // ПОВОРОТ ПЕРСОНАЖА ПО НАПРАВЛЕНИЮ КАМЕРЫ

        if (!isDashing && cameraTransform != null)
        {
            // Берем направление камеры (без наклона)
            Vector3 cameraDirection = cameraTransform.forward;
            cameraDirection.y = 0;
            cameraDirection.Normalize();

            // Поворачиваем персонажа в направлении камеры
            if (cameraDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        // ========== ПРИЦЕЛИВАНИЕ ==========
        if (isAiming)
        {
            Vector3 aimPosition = transform.position + cameraTransform.forward * 1.5f + Vector3.up * 2f;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, aimPosition, Time.deltaTime * 5f);
        }
        UpdateJumpAnimations();
    }

    private void PerformJump()
    {
        isJumping = true;
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Сигнал для анимации
        if (animator != null)
        {
            animator.SetTrigger("JumpTrigger");
            //animator.SetBool("Jump", true);
            //animator.SetBool("IsJumping", true);
        }

        Debug.Log("Прыжок!");
    }

    private void UpdateJumpAnimations()
    {
        if (animator == null) return;

        // Обновляем состояние прыжка
        animator.SetBool("IsGrounded", isGrounded);

        // Вертикальная скорость для анимации (для определения пика прыжка)
        //animator.SetFloat("VerticalVelocity", verticalVelocity);

        // Если в воздухе и не прыгали (например, упали с обрыва)
        //if (!isGrounded && !isJumping)
        //{
        //    animator.SetBool("IsFalling", true);
        //}
        //else
        //{
        //    animator.SetBool("IsFalling", false);
        //}
    }


    // ========== МЕТОДЫ РЫВКА ==========

    private void StartDash()
    {
        isDashing = true;
        dashTimer = 0f;
        dashCooldownTimer = dashCooldown;

        if (moveDirection.magnitude > 0.1f)
        {
            dashDirection = moveDirection.normalized;
        }
        else
        {
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

        dashStartPosition = transform.position;
        Vector3 targetPos = dashStartPosition + dashDirection * dashDistance;
        dashTargetPosition = targetPos;

        //if (animator != null)
        //{
        //    animator.SetTrigger("DashTrigger");
        //}

        Debug.Log($"Рывок активирован! Направление: {dashDirection}");
    }

    private void UpdateDash()
    {
        dashTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(dashTimer / dashDuration);

        Vector3 newPosition = Vector3.Lerp(dashStartPosition, dashTargetPosition, progress);
        Vector3 movement = newPosition - transform.position;
        controller.Move(movement);

        if (progress >= 1f)
        {
            isDashing = false;
            //if (animator != null)
            //{
            //    animator.SetBool("IsDashing", false);
            //}
            Debug.Log("Рывок завершен");
        }
    }

    private void OnGUI()
    {
        if (dashCooldownTimer > 0)
        {
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
    public bool IsJumping()
    {
        return isJumping;
    }
}