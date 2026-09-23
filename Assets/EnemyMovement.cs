using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    public float moveSpeed = 0.5f;
    public float turnSpeed = 720f; // how fast the character snaps to face new direction
    public float gravity = -9.81f;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Keyboard inputs
        float vertical = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1f;

        float horizontal = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;

        // Build a world-space direction from input (top-down: X = left/right, Z = forward/back)
        // Build a world-space direction from input (top-down: X = left/right, Z = forward/back)
        Vector3 inputDir = new Vector3(-horizontal, 0f, -vertical);

        if (inputDir.sqrMagnitude > 0.001f)
        {
            inputDir.Normalize();

            // Rotate the character to face the direction it's moving
            Quaternion targetRotation = Quaternion.LookRotation(inputDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

            // Move in that world direction (not transform.forward, since we're not tank-turning)
            Vector3 move = inputDir * moveSpeed;
            controller.Move(move * Time.deltaTime);
        }

        // Apply constant gravity
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Keeps character grounded firmly
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Send speed to Animator (triggers walk animation)
        if (animator != null)
        {
            animator.SetFloat("Speed", inputDir.sqrMagnitude > 0.001f ? 1f : 0f);
        }
    }
}