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
    public bool IsTargeting { get { return isTargeting; } }
    private Collider playerCollider;
    private Hider hiderRef;

    void Start()
    {
        if (targetIndicator != null) targetIndicator.SetActive(false);
        if (mainCamera == null) mainCamera = Camera.main;
        hiderRef = GetComponent<Hider>();

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
        if (PauseMenu.IsOpen) return; // the pause menu owns the mouse and keys
        if (!hasStone) { if (isTargeting) StopTargeting(); return; }

        // No throwing from inside a hiding spot
        if (hiderRef != null && hiderRef.IsHidden)
        {
            if (isTargeting) StopTargeting();
            return;
        }

        // Enter targeting mode
        if (!isTargeting && Keyboard.current.rKey.wasPressedThisFrame)
        {
            isTargeting = true;
            if (targetIndicator != null) targetIndicator.SetActive(true);
            ShowHint(true);
            return;
        }

        if (isTargeting)
        {
            MoveIndicatorWithMouse();

            // Left click to confirm the throw
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StopTargeting();
                StartCoroutine(ThrowStoneRoutine(targetIndicator.transform.position));
            }
            // T again or right click to cancel (the same keys as the other items)
            else if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame)
            {
                StopTargeting();
            }
        }
    }

    void StopTargeting()
    {
        isTargeting = false;
        if (targetIndicator != null) targetIndicator.SetActive(false);
        ShowHint(false);
    }

    // the paper hint card (centre-bottom) while aiming, like the feather, door lock and ice gun
    GameObject hint;
    void ShowHint(bool on)
    {
        if (hint == null)
        {
            if (!on || playerHUD == null || playerHUD.bagIcon == null) return;
            hint = PaperUI.Hint(playerHUD.bagIcon.canvas.rootCanvas.transform, "Click the floor to throw the rock", "R");
        }
        hint.SetActive(on);
    }

    void OnDisable()
    {
        if (isTargeting) StopTargeting();
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
        if (other.CompareTag("CollectibleStone") && !hasStone) // one rock at a time: a second one stays on the floor
        {
            CollectStone();
            Destroy(other.gameObject);
        }
    }
}