using UnityEngine;

public class SpookyTilt : MonoBehaviour
{
    [Header("Tilt Settings")]
    [Tooltip("How far left and right the painting tilts (in degrees)")]
    public float tiltAngle = 3f;

    [Tooltip("How fast the painting rocks back and forth")]
    public float tiltSpeed = 1.5f;

    [Header("Axis Configuration")]
    [Tooltip("Usually Z (0, 0, 1) for a painting flat against a wall, but it depends on your model's orientation.")]
    public Vector3 tiltAxis = new Vector3(1, 0, 0);

    private Quaternion startRotation;

    void Start()
    {
        // Remember the original rotation so it doesn't snap to 0,0,0 when the game starts
        startRotation = transform.localRotation;
    }

    void Update()
    {
        // Calculate a gentle sine wave between -1 and 1 based on time
        float offset = Mathf.Sin(Time.time * tiltSpeed) * tiltAngle;

        // Apply the rocking motion on top of the original rotation
        transform.localRotation = startRotation * Quaternion.AngleAxis(offset, tiltAxis);
    }
}