using UnityEngine;

// Player 2's use of the feather: after holding it a while she tickles the human player from wherever she
// is, so the killer runs to the human's spot. She only does it when she is calm and the killer is not near her.
[RequireComponent(typeof(AIHider))]
public class AIFeatherBehaviour : MonoBehaviour
{
    public EnemyVision enemy;
    [Tooltip("The human player(s) she may tickle.")]
    public Hider[] otherPlayers;

    [Tooltip("Seconds she waits after picking the feather up before she uses it.")]
    public Vector2 thinkTime = new Vector2(6f, 16f);
    [Tooltip("She will not use it while the killer is closer than this to her.")]
    public float minKillerDistance = 6f;
    [Tooltip("Never tickle someone this close to her: the killer would come for her too.")]
    public float keepAwayFromMe = 4f;

    AIHider ai;
    Hider hider;
    FeatherHolder holder;
    AIStoneBehaviour stones;
    AICollectibleSeeker seeker;
    float thinkTimer;
    bool hadFeather;

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        stones = GetComponent<AIStoneBehaviour>();
        seeker = GetComponent<AICollectibleSeeker>();
        if (enemy == null) enemy = ai.enemy;
    }

    void Update()
    {
        if (holder == null) holder = GetComponent<FeatherHolder>();
        if (hider.IsCaught) return;

        bool has = holder != null && holder.HasFeather;
        if (has && !hadFeather) thinkTimer = Random.Range(thinkTime.x, thinkTime.y);
        hadFeather = has;
        if (!has) return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        if ((stones != null && stones.Busy) || (seeker != null && seeker.Busy)) return;
        // a tickle needs no walking, so she can do it from a hiding spot too
        if (ai.InDanger || ai.externalControl) return;
        if (!ai.IsWandering && !ai.IsHiddenNow) return;
        if (enemy != null && enemy.enabled && Vector3.Distance(transform.position, enemy.transform.position) < minKillerDistance) return;

        Hider victim = PickVictim();
        if (victim == null) { thinkTimer = 3f; return; }

        holder.Tickle(victim);
        thinkTimer = 5f;
    }

    // Sabotage: the other player (human or AI) the killer is closest to, so the laugh brings him
    // straight onto them. Hidden players count too (the laugh gives their spot away).
    Hider PickVictim()
    {
        Hider best = null;
        float bestScore = float.MaxValue;
        if (otherPlayers == null) return null;
        foreach (Hider h in otherPlayers)
        {
            if (h == null || h == hider || h.IsCaught) continue;
            float toMe = Vector3.Distance(transform.position, h.transform.position);
            if (toMe < keepAwayFromMe) continue;
            float score = enemy != null ? Vector3.Distance(enemy.transform.position, h.transform.position) : toMe;
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best;
    }
}