using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerStoneThrower : MonoBehaviour
{
    [Header("References")]
    public PlayerHUD playerHUD;
    public GameObject stonePrefab;
    public Transform handTransform;
    public Animator animator;
    public GameObject targetIndicator;

    [Tooltip("Leave empty to auto-find Camera.main, or drag your Main Camera here")]
    public Camera mainCamera;

    [Header("Throw Settings")]
    public float arcHeight = 3f;
    public float maxThrowDistance = 15f;
    public LayerMask floorMask = Physics.DefaultRaycastLayers; // To prevent aiming on walls

    private bool hasStone = false;
    private bool isTargeting = false;
    private Collider playerCollider;

    void Start()
    {
        if (targetIndicator != null) targetIndicator.SetActive(false);
        if (mainCamera == null) mainCamera = Camera.main;

        // Grab the player's collider to prevent the stone from hitting them
        playerCollider = GetComponent<Collider>();
        if (playerCollider == null) playerCollider = GetComponentInParent<Collider>();
    }

    public void CollectStone()
    {
        hasStone = true;
        if (playerHUD != null) playerHUD.ShowStoneInBag();
    }

    void Update()
    {
        if (!hasStone) return;

        // Enter targeting mode
        if (!isTargeting && Keyboard.current.tKey.wasPressedThisFrame)
        {
            isTargeting = true;
            if (targetIndicator != null) targetIndicator.SetActive(true);
        }

        if (isTargeting)
        {
            MoveIndicatorWithMouse();

            // Left click to confirm the throw
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isTargeting = false;
                if (targetIndicator != null) targetIndicator.SetActive(false);
                StartCoroutine(ThrowStoneRoutine(targetIndicator.transform.position));
            }

            // Right click to cancel targeting
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                isTargeting = false;
                if (targetIndicator != null) targetIndicator.SetActive(false);
            }
        }
    }

    void MoveIndicatorWithMouse()
    {
        if (mainCamera == null) return;

        // Shoot a ray from the mouse cursor into the 3D world
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask))
        {
            Vector3 targetPos = hit.point;

            // Clamp how far the player can aim
            Vector3 offset = targetPos - transform.position;
            offset.y = 0;
            if (offset.magnitude > maxThrowDistance)
            {
                targetPos = transform.position + offset.normalized * maxThrowDistance;

                // Snap back to the floor height after clamping
                if (Physics.Raycast(targetPos + Vector3.up * 5f, Vector3.down, out RaycastHit clampHit, 10f, floorMask))
                {
                    targetPos = clampHit.point;
                }
            }

            targetIndicator.transform.position = targetPos + Vector3.up * 0.05f;
        }
    }

    IEnumerator ThrowStoneRoutine(Vector3 targetPos)
    {
        if (animator != null) animator.SetTrigger("Throw");

        yield return new WaitForSeconds(0.4f); // Adjust this to match your animation release frame

        hasStone = false;
        if (playerHUD != null) playerHUD.EmptyBag();

        if (stonePrefab != null && handTransform != null)
        {
            GameObject stone = Instantiate(stonePrefab, handTransform.position, Quaternion.identity);

            // FIX: Tell the physics engine to ignore collisions between the stone and the player
            Collider stoneCollider = stone.GetComponent<Collider>();
            if (stoneCollider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(stoneCollider, playerCollider);
            }

            Rigidbody rb = stone.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 velocity = CalculateTrajectory(handTransform.position, targetPos, arcHeight);
                rb.linearVelocity = velocity;
            }
        }
    }

    Vector3 CalculateTrajectory(Vector3 origin, Vector3 target, float height)
    {
        float gravity = Physics.gravity.y;
        float displacementY = target.y - origin.y;
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);

        float timeUp = Mathf.Sqrt(Mathf.Abs(-2 * height / gravity));
        float timeDown = Mathf.Sqrt(Mathf.Abs(2 * (displacementY - height) / gravity));

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(Mathf.Abs(-2 * gravity * height));
        Vector3 velocityXZ = displacementXZ / (timeUp + timeDown);

        return velocityXZ + velocityY;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CollectibleStone"))
        {
            CollectStone();
            Destroy(other.gameObject);
        }
    }
}