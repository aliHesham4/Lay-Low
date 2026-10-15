using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class HidingSpot : MonoBehaviour
{
    [Header("Visual swap")]
    public GameObject normalVisual;
    public GameObject hiddenVisual;
    [Tooltip("Uncheck this for bathtubs so the player mesh stays visible inside.")]
    public bool hidePlayerMesh = true;

    [Header("Bathtub Settings (Laying Down)")]
    public bool layFlatInside = false;
    [Tooltip("If the feet and head are swapped, change X to -90 or Y to 180.")]
    public Vector3 layFlatRotation = new Vector3(90f, 0f, 0f);

    [Header("Player Positions (Optional)")]
    public Transform insidePoint; // Place inside the bathtub (UPRIGHT, just lower Y)
    public Transform exitPoint;   // Place on the floor outside the bathtub

    [Header("Camera (same two objects for every hiding spot)")]
    public CinemachineCamera fpvCamera;
    public GameObject hideCamera;

    [Header("Close-up camera placement")]
    public Transform cameraAnchor;
    public Vector3 cameraOffset = new Vector3(0f, 1.5f, -2.5f);
    public float lookAtHeight = 0.5f;

    [Header("Looking around while hidden")]
    public float lookSpeed = 90f;
    public float maxYaw = 60f;

    [Header("AI hider (optional)")]
    public Transform approachPoint;

    public bool IsFree => occupant == null;
    public Hider Occupant => occupant;
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

[Header("On-screen prompt (Press H)")]
    [Tooltip("Optional. Where the prompt floats. Defaults to just above the trigger box.")]
    public Transform promptAnchor;

    void Update()
    {
        if (humanInRange != null && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (occupant == null) TryEnter(humanInRange);
            else if (occupant == humanInRange) Exit(humanInRange);
        }

        if (occupant != null && occupant.isHumanControlled && switchedCamera)
            HandleHideLook();

        UpdatePrompt();
    }

    void OnDisable()
    {
        HidePrompt.Hide(this);
    }

    // Shows "Press [H] to hide", "Press [H] to come out" or "A player is hiding here"
    void UpdatePrompt()
    {
        bool show = humanInRange != null && !humanInRange.IsCaught && Time.timeScale > 0f;
        if (!show)
        {
            HidePrompt.Hide(this);
            return;
        }

        HidePrompt.Mode mode;
        if (occupant == null) mode = HidePrompt.Mode.Hide;
        else if (occupant == humanInRange) mode = HidePrompt.Mode.ComeOut;
        else mode = HidePrompt.Mode.Occupied;

        HidePrompt.Instance.Show(this, GetPromptPosition(), mode);
    }

    Vector3 GetPromptPosition()
    {
        if (promptAnchor != null) return promptAnchor.position;

        if (triggers != null && triggers.Length > 0 && triggers[0] != null)
        {
            Bounds b = triggers[0].bounds;
            return new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        }
        return transform.position + Vector3.up * 0.6f;
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
        h.SetHidden(true, hidePlayerMesh);

        if (insidePoint != null)
        {
            h.transform.position = insidePoint.position;
            // 1. Keep the invisible physics root standing strictly upright
            h.transform.rotation = Quaternion.Euler(0f, insidePoint.eulerAngles.y, 0f);
        }

        // 2. Tilt ONLY the visual mesh backward to lay down
        if (layFlatInside)
        {
            Animator anim = h.GetComponentInChildren<Animator>();
            if (anim != null) anim.transform.localRotation = Quaternion.Euler(layFlatRotation);
        }

        if (normalVisual != null) normalVisual.SetActive(false);
        if (hiddenVisual != null) hiddenVisual.SetActive(true);

        if (h.isHumanControlled && hideCamera != null)
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

        if (exitPoint != null)
        {
            h.transform.position = exitPoint.position;
        }

        // Snap the visual mesh back upright so the character walks normally
        if (layFlatInside)
        {
            Animator anim = h.GetComponentInChildren<Animator>();
            if (anim != null) anim.transform.localRotation = Quaternion.identity;
        }

        h.transform.rotation = Quaternion.Euler(0f, h.transform.eulerAngles.y, 0f);

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
            if ((c.ClosestPoint(pos) - pos).sqrMagnitude <= 0.04f) return true;
        }
        return false;
    }

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

        if (hideCamera != null)
            hideCamera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
    }
}