using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float turnSpeed = 180f;

    [Header("Jumping & Gravity")]
    public float jumpHeight = 0.5f;       // Height for a small jump
    public float gravity = -19.62f;       // Heavy gravity so jump isn't floaty
    private float verticalVelocity = 0f;

    private CharacterController controller;
    private Animator animator;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        if (Keyboard.current == null) return;

        // 1. Read Inputs
        bool upPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
        bool downPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
        bool rightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
        bool leftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
        bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;

        // 2. Turning Rotation
        if (rightPressed) transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime, Space.World);
        if (leftPressed) transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime, Space.World);

        // 3. Forward / Backward Motion (Mesh Direction Inverted)
        float moveInput = 0f;
        if (upPressed) moveInput -= 1f;
        if (downPressed) moveInput += 1f;

        bool isMoving = Mathf.Abs(moveInput) > 0.01f;
        Vector3 moveDirection = transform.forward * moveInput;

        // 4. Grounded Check & Ground Snapping
        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Keeps player glued to ground/slopes
        }

        // 5. Jump Input
        if (jumpPressed && isGrounded)
        {
            // Velocity formula v = sqrt(h * -2 * g)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 6. Apply Gravity over time
        verticalVelocity += gravity * Time.deltaTime;

        // 7. Execute Total Movement
        Vector3 motion = (moveDirection * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(motion * Time.deltaTime);

        // 8. Update Animator Parameter
        if (animator != null)
        {
            animator.SetFloat("Speed", isMoving ? 1f : 0f);
        }
    }
}