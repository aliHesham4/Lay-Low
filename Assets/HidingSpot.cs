using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class HidingSpot : MonoBehaviour
{
    [Header("Visual swap")]
    public GameObject normalVisual;   // optional
    public GameObject hiddenVisual;

    [Header("Camera (same two objects for every hiding spot)")]
    public CinemachineCamera fpvCamera;   // your CM_FPV
    public GameObject hideCamera;         // the ONE shared CM_Hide

    [Header("Close-up camera placement")]
    public Transform cameraAnchor;
    public Vector3 cameraOffset = new Vector3(0f, 1.5f, -2.5f);
    public float lookAtHeight = 0.5f;

    [Header("Looking around while hidden")]
    public float lookSpeed = 90f;   // degrees per second
    public float maxYaw = 60f;      // how far left/right from the starting view

    [Header("AI hider (optional)")]
    [Tooltip("Where the AI should stand to hide. Defaults to the centre of the trigger.")]
    public Transform approachPoint;

    public bool IsFree => occupant == null;
    public Hider Occupant => occupant; // The enemy will use this to check inside
    private Hider occupant;
    private Hider humanInRange;
    private bool switchedCamera = false;
    private Quaternion baseRotation;
    private float yaw = 0f;
    private Collider[] triggers;

    void Awake()
    {
        triggers = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => c.isTrigger);
    }

    void Start()
    {
        if (normalVisual != null) normalVisual.SetActive(true);
        if (hiddenVisual != null) hiddenVisual.SetActive(false);
        if (hideCamera != null) hideCamera.SetActive(false);
    }

    void Update()
    {
        if (humanInRange != null && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (occupant == null) TryEnter(humanInRange);
            else if (occupant == humanInRange) Exit(humanInRange);
            // otherwise someone else is already hiding here, so do nothing
        }

        if (occupant != null && occupant.isHumanControlled && switchedCamera)
            HandleHideLook();
    }

    void OnTriggerEnter(Collider other)
    {
        Hider h = other.GetComponentInParent<Hider>();
        if (h != null && h.isHumanControlled) humanInRange = h;
    }

    void OnTriggerExit(Collider other)
    {
        Hider h = other.GetComponentInParent<Hider>();
        if (h != null && h == humanInRange && occupant != h) humanInRange = null;
    }

    public bool TryEnter(Hider h)
    {
        if (h == null || h.IsCaught || occupant != null) return false;

        occupant = h;
        h.SetHidden(true);

        if (normalVisual != null) normalVisual.SetActive(false);
        if (hiddenVisual != null) hiddenVisual.SetActive(true);

        if (h.isHumanControlled && hideCamera != null && fpvCamera != null && fpvCamera.IsLive)
        {
            PlaceHideCamera();
            hideCamera.SetActive(true);
            switchedCamera = true;
        }
        return true;
    }

    public void Exit(Hider h)
    {
        if (occupant == null || occupant != h) return;

        occupant = null;
        h.SetHidden(false);

        if (normalVisual != null) normalVisual.SetActive(true);
        if (hiddenVisual != null) hiddenVisual.SetActive(false);

        if (switchedCamera && hideCamera != null)
        {
            hideCamera.SetActive(false);
            switchedCamera = false;
        }
    }

    // ---- helpers for the AI hider ----
    public Vector3 GetApproachPoint()
    {
        if (approachPoint != null) return approachPoint.position;
        if (triggers != null && triggers.Length > 0 && triggers[0] != null) return triggers[0].bounds.center;
        return transform.position;
    }

    public bool IsReachedBy(Vector3 pos)
    {
        if (triggers == null) return false;
        foreach (Collider c in triggers)
        {
            if (c == null) continue;
            if ((c.ClosestPoint(pos) - pos).sqrMagnitude <= 0.04f) return true;   // within 0.2 m of the trigger
        }
        return false;
    }

    // ---- camera ----
    void PlaceHideCamera()
    {
        if (cameraAnchor != null)
        {
            hideCamera.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
        }
        else
        {
            Vector3 camPos = transform.TransformPoint(cameraOffset);
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            hideCamera.transform.position = camPos;
            hideCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);
        }

        // Remember the starting view so looking around is relative to it
        baseRotation = hideCamera.transform.rotation;
        yaw = 0f;
    }

    void HandleHideLook()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float yawInput = 0f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) yawInput += 1f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) yawInput -= 1f;

        yaw = Mathf.Clamp(yaw + yawInput * lookSpeed * Time.deltaTime, -maxYaw, maxYaw);

        // Rotate around world up, so the view stays level and turns in place
        hideCamera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 camPos = cameraAnchor != null ? cameraAnchor.position : transform.TransformPoint(cameraOffset);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(camPos, 0.15f);
        Gizmos.DrawLine(camPos, transform.position + Vector3.up * lookAtHeight);
    }
}