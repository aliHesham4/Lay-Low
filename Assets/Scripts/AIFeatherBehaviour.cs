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

    AIHider ai;
    Hider hider;
    FeatherHolder holder;
    AIStoneBehaviour stones;
    float thinkTimer;
    bool hadFeather;

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        stones = GetComponent<AIStoneBehaviour>();
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
        if (stones != null && stones.Busy) return;
        if (!ai.IsWandering || ai.InDanger) return;
        if (enemy != null && enemy.enabled && Vector3.Distance(transform.position, enemy.transform.position) < minKillerDistance) return;

        Hider victim = null;
        if (otherPlayers != null)
            foreach (Hider h in otherPlayers)
                if (h != null && h != hider && !h.IsCaught) { victim = h; break; }
        if (victim == null) { thinkTimer = 3f; return; }

        holder.Tickle(victim);
        thinkTimer = 5f;
    }
}
