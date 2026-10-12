using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerStoneThrower : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the object with your PlayerHUD script here")]
    public PlayerHUD playerHUD;
    public GameObject stonePrefab;
    public Transform handTransform;
    public Animator animator;

    [Header("Throw Settings")]
    public float arcHeight = 3f;
    private bool hasStone = false;

    public void CollectStone()
    {
        hasStone = true;
        if (playerHUD != null)
        {
            playerHUD.ShowStoneInBag();
        }
    }

    void Update()
    {
        if (hasStone && Mouse.current.leftButton.wasPressedThisFrame)
        {
            StartCoroutine(ThrowStoneRoutine());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CollectibleStone"))
        {
            CollectStone();
            Destroy(other.gameObject);
        }
    }

    IEnumerator ThrowStoneRoutine()
    {
        if (animator != null)
        {
            animator.SetTrigger("Throw");
        }

        yield return new WaitForSeconds(0.6f);

        hasStone = false;
        if (playerHUD != null)
        {
            playerHUD.EmptyBag();
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (stonePrefab != null && handTransform != null)
            {
                GameObject stone = Instantiate(stonePrefab, handTransform.position, Quaternion.identity);
                Rigidbody rb = stone.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    Vector3 velocity = CalculateTrajectory(handTransform.position, hit.point, arcHeight);
                    rb.linearVelocity = velocity;
                }
            }
        }
    }

    Vector3 CalculateTrajectory(Vector3 origin, Vector3 target, float height)
    {
        float gravity = Physics.gravity.y;
        float displacementY = target.y - origin.y;
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);

        float timeUp = Mathf.Sqrt(-2 * height / gravity);
        float timeDown = Mathf.Sqrt(2 * (displacementY - height) / gravity);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * height);
        Vector3 velocityXZ = displacementXZ / (timeUp + timeDown);

        return velocityXZ + velocityY;
    }
}