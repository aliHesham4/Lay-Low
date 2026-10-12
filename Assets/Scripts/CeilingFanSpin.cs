using UnityEngine;

// Spins the GameObject around its own local axis, in place.
public class CeilingFanSpin : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second.")]
    public float degreesPerSecond = 180f;

    [Tooltip("Local axis to rotate around.")]
    public Vector3 axis = Vector3.up;

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
