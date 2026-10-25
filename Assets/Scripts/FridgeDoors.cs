using System.Collections;
using UnityEngine;

// Animates the fridge doors for the fridge hiding spot.
//  - A hider gets in: the door swings open, then closes almost all the way (left slightly ajar) with light leaking out.
//  - The hider comes out: the door swings open, then closes fully and the light goes off.
//  - The killer opens it: the door swings open, and closes again if nobody was inside.
[RequireComponent(typeof(HidingSpot))]
public class FridgeDoors : MonoBehaviour
{
    [Header("Door (the whole fridge front hangs under this hinge, placed on its right edge)")]
    public Transform hinge;
    [Tooltip("How far the door swings open, in degrees.")]
    public float openAngle = 55f;
    [Tooltip("+1 or -1: which way the hinge turns so the door swings OUT into the room.")]
    public float openDirection = -1f;
    [Tooltip("How far the doors stay open while someone hides inside.")]
    public float ajarAngle = 14f;
    public float swingSpeed = 420f; // degrees per second

    [Header("Light that leaks out")]
    public Light innerLight;
    public Light spillLight;
    public float innerIntensity = 0.3f;
    public float spillIntensity = 0.3f;

    HidingSpot spot;
    float angle;        // current opening, 0..openAngle
    float targetAngle;
    Coroutine routine;

    void Awake()
    {
        spot = GetComponent<HidingSpot>();
        Apply();
    }

    void Update()
    {
        if (!Mathf.Approximately(angle, targetAngle))
        {
            angle = Mathf.MoveTowards(angle, targetAngle, swingSpeed * Time.deltaTime);
            Apply();
        }
    }

    void Apply()
    {
        if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, angle * Mathf.Sign(openDirection), 0f);

        float open01 = Mathf.Clamp01(angle / Mathf.Max(1f, ajarAngle));
        if (innerLight != null) { innerLight.enabled = angle > 1.5f; innerLight.intensity = innerIntensity * open01; }
        if (spillLight != null) { spillLight.enabled = angle > 1.5f; spillLight.intensity = spillIntensity * open01; }
    }

    void Run(IEnumerator r)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(r);
    }

    // ---- called by HidingSpot ----

    public void PlayEnter() { Run(EnterRoutine()); }
    public void PlayExit() { Run(ExitRoutine()); }
    public void EnemyOpens(float duration) { Run(EnemyRoutine(duration)); }

    IEnumerator EnterRoutine()
    {
        targetAngle = openAngle;
        yield return new WaitUntil(() => Mathf.Approximately(angle, openAngle));
        yield return new WaitForSeconds(0.1f);
        targetAngle = ajarAngle; // stays slightly open, light spills out
    }

    IEnumerator ExitRoutine()
    {
        targetAngle = openAngle;
        yield return new WaitUntil(() => Mathf.Approximately(angle, openAngle));
        yield return new WaitForSeconds(0.3f);
        targetAngle = 0f;
    }

    IEnumerator EnemyRoutine(float duration)
    {
        targetAngle = openAngle;
        yield return new WaitForSeconds(duration + 0.3f);
        // empty: shut it again. If somebody was inside, Exit() has already taken over.
        if (spot == null || spot.IsFree) targetAngle = 0f;
    }
}
