using UnityEngine;

// Temporary speed boost on a player (human or AI). Added automatically by SpeedBoost.Apply.
public class SpeedBoost : MonoBehaviour
{
    PlayerMovement movement;
    AIHider ai;
    float originalSpeed;
    float timeLeft;
    float boostSpeed;

    public bool Active { get { return timeLeft > 0f; } }

    public static void Apply(GameObject target, float speed, float duration)
    {
        SpeedBoost b = target.GetComponent<SpeedBoost>();
        if (b == null) b = target.AddComponent<SpeedBoost>();
        b.Begin(speed, duration);
    }

    void Begin(float speed, float duration)
    {
        boostSpeed = speed;
        if (!Active)
        {
            movement = GetComponent<PlayerMovement>();
            ai = GetComponent<AIHider>();
            if (movement != null) originalSpeed = movement.moveSpeed;
        }
        timeLeft = duration; // picking up another pepper just restarts the 5 seconds
        if (movement != null) movement.moveSpeed = boostSpeed;
        if (ai != null) ai.speedOverride = boostSpeed;
    }

    void Update()
    {
        if (timeLeft <= 0f) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) End();
    }

    void End()
    {
        timeLeft = 0f;
        if (movement != null) movement.moveSpeed = originalSpeed;
        if (ai != null) ai.speedOverride = 0f;
    }

    void OnDisable() { if (Active) End(); }
}
