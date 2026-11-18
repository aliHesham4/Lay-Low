using UnityEngine;

// AI use of the ice gun (sabotage): freeze the other player the killer is closest to, so he can catch them.
// She waits for a good moment (someone near the killer); if none comes she fires anyway after a while.
[RequireComponent(typeof(AIHider))]
public class AIIceGunBehaviour : MonoBehaviour
{
    public EnemyVision enemy;
    [Tooltip("The other players she may freeze (filled automatically if empty).")]
    public Hider[] otherPlayers;

    [Tooltip("Seconds she waits after picking the gun up before she may use it.")]
    public Vector2 thinkTime = new Vector2(2f, 5f);
    [Tooltip("Best moment: the victim is within this distance of the killer.")]
    public float victimNearKiller = 8f;
    [Tooltip("Fire anyway after holding the gun this long.")]
    public float useAnywayAfter = 25f;
    [Tooltip("Never freeze someone this close to her (the killer would be right there too).")]
    public float keepAwayFromMe = 4f;

    AIHider ai;
    Hider hider;
    IceGunHolder holder;
    float thinkTimer;
    float heldFor;
    bool hadGun;

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        if (enemy == null) enemy = ai.enemy;
        if (otherPlayers == null || otherPlayers.Length == 0) otherPlayers = FindObjectsByType<Hider>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (holder == null) holder = GetComponent<IceGunHolder>();
        if (hider.IsCaught) return;

        bool has = holder != null && holder.HasIceGun;
        if (has && !hadGun) { thinkTimer = Random.Range(thinkTime.x, thinkTime.y); heldFor = 0f; }
        hadGun = has;
        if (!has) return;

        heldFor += Time.deltaTime;
        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        if (ai.InDanger) return; // busy getting away
        if (!ai.IsWandering && !ai.IsHiddenNow) return; // it needs no walking, so from a hiding spot is fine

        float killerDist;
        Hider victim = PickVictim(out killerDist);
        if (victim == null) { thinkTimer = 2f; return; }
        if (killerDist > victimNearKiller && heldFor < useAnywayAfter) { thinkTimer = 1f; return; }

        if (!holder.Shoot(victim)) thinkTimer = 2f;
    }

    // the other player (human or AI) the killer is closest to
    Hider PickVictim(out float killerDist)
    {
        Hider best = null;
        killerDist = float.MaxValue;
        foreach (Hider h in otherPlayers)
        {
            if (h == null || h == hider || h.IsCaught || IceSlow.IsFrozen(h.gameObject)) continue;
            if (Vector3.Distance(transform.position, h.transform.position) < keepAwayFromMe) continue;
            float d = enemy != null ? Vector3.Distance(enemy.transform.position, h.transform.position) : 0f;
            if (d < killerDist) { killerDist = d; best = h; }
        }
        return best;
    }
}
