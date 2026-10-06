using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0.8f;
    public float gravity = -9.81f;

    [Header("Turning")]
    public float turnSpeed = 180f; // degrees per second while holding Left/Right

    private CharacterController controller;
    private Animator animator;
    private float verticalVelocity = 0f;
    private bool isMoving = false;

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

        bool upPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
        bool downPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
        bool rightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
        bool leftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;

        // Left/Right ONLY rotate the character — held down, it keeps turning smoothly.
        if (rightPressed) transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime, Space.World);
        if (leftPressed) transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime, Space.World);

        // Up/Down walk forward/backward in whatever direction the character is CURRENTLY facing.
        float moveInput = 0f;
        if (upPressed) moveInput += 1f;
        if (downPressed) moveInput -= 1f;

        isMoving = Mathf.Abs(moveInput) > 0.01f;

        Vector3 moveDirection = transform.forward * moveInput;

        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = (moveDirection * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(motion * Time.deltaTime);

        if (animator != null)
            animator.SetFloat("Speed", isMoving ? 1f : 0f);
    }
}