using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float turnSpeed = 200f;
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

        // Rotation
        transform.Rotate(0f, horizontal * turnSpeed * Time.deltaTime, 0f);

        // Forward/Backward movement
        Vector3 move = transform.forward * vertical * moveSpeed;
        controller.Move(move * Time.deltaTime);

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
            animator.SetFloat("Speed", Mathf.Abs(vertical));
        }
    }
}