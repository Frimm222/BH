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
    public AudioClip dashSound;
    public GameObject dashIcon;

    [Header("Прицеливание")]
    public KeyCode aimKey = KeyCode.Mouse1;
    public float aimSpeedMultiplier = 0.5f;

    [Header("Прыжки")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float airControl = 0.3f;
    public AudioClip jumpSound;

    [Header("Ссылки")]
    public Transform cameraTransform;
    public Animator animator;

    private CharacterController controller;
    private AudioSource audioSource;
    private float verticalVelocity;
    private bool isGrounded;
    private bool isAiming;
    private Vector3 moveDirection;
    private Vector3 dashDirection;
    private bool isJumping = false;
    private bool isMovementLocked = false;
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

        // 🔥 НАСТРАИВАЕМ AUDIO SOURCE
        SetupAudioSource();
    }

    void SetupAudioSource()
    {
        // Пытаемся получить существующий AudioSource
        audioSource = GetComponent<AudioSource>();

        // Если нет - создаем новый
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("Добавлен AudioSource на игрока");
        }

        // Настраиваем AudioSource
        audioSource.spatialBlend = 1f;        // 3D звук
        audioSource.dopplerLevel = 0f;        // Отключаем доплер
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = 20f;
        audioSource.volume = 1f;

        Debug.Log("AudioSource настроен!");
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
                if (animator != null)
                {
                    animator.SetBool("IsJumping", false);
                }
            }
        }

        if (isMovementLocked)
        {
            UpdateGravityOnly();
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isJumpingInput = Input.GetButtonDown("Jump");
        bool dashInput = Input.GetKeyDown(dashKey);

        UpdateAnimations(horizontal, vertical);

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
            if (!isGrounded)
            {
                move = moveDirection * currentSpeed * airControl * Time.deltaTime;
            }
            else
            {
                move = moveDirection * currentSpeed * Time.deltaTime;
            }
        }

        // ========== ГРАВИТАЦИЯ ==========
        if (isJumpingInput && isGrounded && !isDashing && !isJumping)
        {
            PerformJump();
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity * Time.deltaTime;

        controller.Move(move);

        // ========== ПОВОРОТ ПЕРСОНАЖА ==========
        if (!isDashing && moveDirection.magnitude > 0.1f && cameraTransform != null)
        {
            Vector3 cameraDirection = cameraTransform.forward;
            cameraDirection.y = 0;
            cameraDirection.Normalize();

            if (cameraDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }

    void UpdateGravityOnly()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 move = new Vector3(0, verticalVelocity * Time.deltaTime, 0);
        controller.Move(move);

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsRunning", false);
            animator.SetFloat("Speed", 0f);
            animator.SetBool("W", false);
            animator.SetBool("S", false);
            animator.SetBool("A", false);
            animator.SetBool("D", false);
        }
    }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        if (locked)
        {
            moveDirection = Vector3.zero;
        }
        Debug.Log($"Движение {(locked ? "заблокировано" : "разблокировано")}");
    }

    public bool IsMovementLocked()
    {
        return isMovementLocked;
    }

    void UpdateAnimations(float horizontal, float vertical)
    {
        if (animator == null) return;

        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        bool isRunning = isMoving && !isAiming && !isDashing && isGrounded;

        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsAiming", isAiming);
        animator.SetBool("IsDashing", isDashing);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsJumping", isJumping);

        //animator.SetFloat("Horizontal", horizontal);
        //animator.SetFloat("Vertical", vertical);
        //animator.SetFloat("Speed", isRunning ? 1f : (isMoving ? 0.5f : 0f));

        animator.SetBool("W", vertical > 0.1f);
        animator.SetBool("S", vertical < -0.1f);
        animator.SetBool("A", horizontal < -0.1f);
        animator.SetBool("D", horizontal > 0.1f);
    }

    private void PerformJump()
    {
        isJumping = true;
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (animator != null)
        {
            animator.SetTrigger("JumpTrigger");
            animator.SetBool("IsJumping", true);
        }

        // 🔥 ВОСПРОИЗВОДИМ ЗВУК ПРЫЖКА
        PlaySound(jumpSound);

        Debug.Log("Прыжок!");
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

        if (animator != null)
        {
            animator.SetTrigger("DashTrigger");
            animator.SetBool("IsDashing", true);
        }

        if (dashIcon != null)
        {
            Animator dashIconAnimator = dashIcon.GetComponent<Animator>();
            dashIconAnimator.SetTrigger("Used");
        }

        // 🔥 ВОСПРОИЗВОДИМ ЗВУК РЫВКА
        PlaySound(dashSound);

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
            if (animator != null)
            {
                animator.SetBool("IsDashing", false);
            }
            Debug.Log("Рывок завершен");
        }
    }

    // 🔥 НОВЫЙ МЕТОД ДЛЯ ВОСПРОИЗВЕДЕНИЯ ЗВУКОВ
    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning($"Звук не назначен!");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("AudioSource не найден!");
            return;
        }

        audioSource.PlayOneShot(clip);
        Debug.Log($"Воспроизведен звук: {clip.name}");
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