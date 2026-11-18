using UnityEngine;
using UnityEngine.AI;

// Generalized collectible seeker for the AI hider:
// Scans the map by tag for any collectible type she doesn't currently hold (Stone, Feather, Party Horn, Door Lock),
// walks to the closest safe one, and grabs nearby Peppers along the way.
[RequireComponent(typeof(AIHider))]
[RequireComponent(typeof(NavMeshAgent))]
public class AICollectibleSeeker : MonoBehaviour
{
    [Header("References")]
    public EnemyVision enemy;
    [Tooltip("Other players (used to leave nearby pickups for human players).")]
    public Hider[] otherPlayers;

    [Header("Collectible Tags")]
    public string stoneTag = "CollectibleStone";
    public string featherTag = "CollectibleFeather";
    public string whistleTag = "CollectibleWhistle";
    public string doorLockTag = "CollectibleDoorLock";
    public string iceGunTag = "CollectibleIceGun";

    [Header("Seeking Rules")]
    [Tooltip("If ticked, she will not seek another collectible while already carrying one.")]
    public bool oneCollectibleAtATime = false;
    public float seekRadius = 60f;
    [Tooltip("Seconds between looking for a collectible.")]
    public float seekInterval = 1.5f;
    [Tooltip("She won't look for collectibles during the first seconds of the game (min, max).")]
    public Vector2 firstSeekDelay = new Vector2(1f, 4f);
    [Range(0f, 1f)]
    [Tooltip("Chance she actually goes for a collectible each time she notices one.")]
    public float seekChance = 1f;
    [Tooltip("Seconds after using an item before she looks for another collectible.")]
    public float cooldownAfterUse = 12f;

    [Header("Safety & Sharing")]
    [Tooltip("If a human player is this close to a collectible she leaves it for them.")]
    public float leaveForHumanRadius = 2f;
    [Tooltip("She will not go for a collectible that lies this close to the killer.")]
    public float killerNearItemRadius = 9f;
    [Tooltip("She drops the idea of a collectible when the killer gets this close to HER.")]
    public float abortCollectDistance = 12f;
    [Tooltip("Killer must be farther than this before she steps out of a hiding spot for an item.")]
    public float leaveHideMinKillerDist = 14f;
    [Tooltip("Max distance she will leave a hiding spot to go grab a collectible.")]
    public float leaveHideMaxItemDist = 22f;
    [Tooltip("If the killer is within this distance of the direct line between her and the item, skip it.")]
    public float corridorBlockRadius = 5.5f;

    [Header("Instants (Pepper)")]
    [Tooltip("While walking normally she grabs an instant item (pepper) that is this close. Collectibles come first.")]
    public float instantGrabRadius = 2.5f;

    enum Mode { Idle, GoingToCollectible, GoingToInstant }
    enum ItemType { None, Stone, Feather, Whistle, DoorLock, IceGun }

    Mode mode = Mode.Idle;
    ItemType targetType = ItemType.None;

    AIHider ai;
    Hider hider;
    NavMeshAgent agent;

    AIStoneBehaviour stones;
    FeatherHolder featherHolder;
    WhistleHolder whistleHolder;
    DoorLockHolder doorLockHolder;
    IceGunHolder iceGunHolder;

    GameObject targetItem;
    PepperCollectible instantTarget;

    float nextSeek;
    float repathTimer;
    float instantUntil;
    float defaultStoppingDistance;
    bool hadAnyItem;

    public bool Busy => mode != Mode.Idle;

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        agent = GetComponent<NavMeshAgent>();
        stones = GetComponent<AIStoneBehaviour>();

        if (enemy == null) enemy = ai.enemy;
        defaultStoppingDistance = agent.stoppingDistance;
        nextSeek = Time.time + Random.Range(firstSeekDelay.x, firstSeekDelay.y);
    }

    void Update()
    {
        if (hider.IsCaught) { Release(); return; }

        if (featherHolder == null) featherHolder = GetComponent<FeatherHolder>();
        if (whistleHolder == null) whistleHolder = GetComponent<WhistleHolder>();
        if (doorLockHolder == null) doorLockHolder = GetComponent<DoorLockHolder>();
        if (iceGunHolder == null) iceGunHolder = GetComponent<IceGunHolder>();

        bool holdingAny = HasStone() || HasFeather() || HasWhistle() || HasDoorLock() || HasIceGun();
        if (hadAnyItem && !holdingAny)
            nextSeek = Time.time + cooldownAfterUse;
        hadAnyItem = holdingAny;

        if ((stones != null && stones.Busy) || (whistleHolder != null && whistleHolder.IsBlowing))
        {
            if (mode != Mode.Idle) Release();
            return;
        }

        if (mode == Mode.Idle && ai.externalControl) return;

        // Only look for collectibles when off cooldown and the killer isn't hovering nearby
        bool canSeekNow = Time.time >= nextSeek && !ai.InDanger && KillerDist() >= abortCollectDistance;
        ItemType bestType = ItemType.None;
        GameObject bestCollectible = canSeekNow ? FindBestCollectible(out  bestType) : null;
        bool wantedItemOnMap = bestCollectible != null;

        // Only suppress idle hiding if a safe collectible is actually close enough to be worth the risk
        float itemDist = wantedItemOnMap ? Vector3.Distance(transform.position, bestCollectible.transform.position) : 999f;
        ai.suppressIdleHide = wantedItemOnMap && itemDist <= leaveHideMaxItemDist;

        if (wantedItemOnMap && ai.IsHiddenNow &&
            KillerDist() > leaveHideMinKillerDist &&
            itemDist <= leaveHideMaxItemDist)
        {
            ai.LeaveHidingSpot();
        }

        if (HandleInstants(wantedItemOnMap)) return;

        UpdateSeeking(bestCollectible, bestType);
    }

    // ---------------- Instants (Pepper) ----------------
    bool HandleInstants(bool collectibleWanted)
    {
        if (mode == Mode.GoingToInstant)
        {
            if (instantTarget == null || ai.InDanger || hider.IsCaught || Time.time > instantUntil)
            {
                Release();
                return false;
            }
            return true;
        }

        if (mode != Mode.Idle || !ai.IsWandering || ai.InDanger) return false;

        // Wanting a collectible comes first: only grab a pepper she practically touches
        float radius = collectibleWanted ? 1.2f : instantGrabRadius;
        PepperCollectible best = null;
        float bestDist = radius;
        foreach (PepperCollectible pc in FindObjectsByType<PepperCollectible>(FindObjectsSortMode.None))
        {
            if (pc == null) continue;
            float d = Vector3.Distance(transform.position, pc.transform.position);
            if (d < bestDist) { bestDist = d; best = pc; }
        }
        if (best == null) return false;

        instantTarget = best;
        instantUntil = Time.time + 4f;
        mode = Mode.GoingToInstant;
        ai.externalControl = true;
        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.speed = ai.wanderSpeed;
        MoveTo(best.transform.position);
        return true;
    }

    // ---------------- Generalized Collectible Seeking ----------------
    void UpdateSeeking(GameObject candidate, ItemType candidateType)
    {
        if (mode == Mode.Idle)
        {
            if (Time.time < nextSeek) return;
            nextSeek = Time.time + seekInterval;

            if (!ai.IsWandering || ai.InDanger) return;
            if (candidate == null) return;
            if (Random.value > seekChance) return;

            targetItem = candidate;
            targetType = candidateType;
            mode = Mode.GoingToCollectible;
            repathTimer = 0f;

            ai.externalControl = true;
            agent.isStopped = false;
            agent.stoppingDistance = 0f;
            agent.speed = ai.wanderSpeed;
            MoveTo(targetItem.transform.position);
        }
        else if (mode == Mode.GoingToCollectible)
        {
            // Abort if picked up, destroyed, already holding that type, danger, killer too close, or human took priority
            if (targetItem == null || !targetItem.activeInHierarchy ||
                AlreadyHasTargetType() ||
                ai.InDanger || KillerDist() < abortCollectDistance ||
                IsNearKiller(targetItem.transform.position) ||
                IsNearHuman(targetItem.transform.position))
            {
                Release();
                nextSeek = Time.time + seekInterval;
                return;
            }

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = 0.5f;
                MoveTo(targetItem.transform.position);
            }
        }
    }

    GameObject FindBestCollectible(out ItemType chosenType)
    {
        chosenType = ItemType.None;
        if (oneCollectibleAtATime && (HasStone() || HasFeather() || HasWhistle() || HasDoorLock() || HasIceGun()))
            return null;

        GameObject best = null;
        float bestDist = seekRadius;

        if (!HasStone() && (stones == null || !stones.OnSeekCooldown))
            EvaluateTag(stoneTag, ItemType.Stone, ref best, ref bestDist, ref chosenType);

        if (!HasFeather())
            EvaluateTag(featherTag, ItemType.Feather, ref best, ref bestDist, ref chosenType);

        if (!HasWhistle())
            EvaluateTag(whistleTag, ItemType.Whistle, ref best, ref bestDist, ref chosenType);

        if (!HasDoorLock())
            EvaluateTag(doorLockTag, ItemType.DoorLock, ref best, ref bestDist, ref chosenType);

        if (!HasIceGun())
            EvaluateTag(iceGunTag, ItemType.IceGun, ref best, ref bestDist, ref chosenType);

        return best;
    }

    void EvaluateTag(string tag, ItemType type, ref GameObject best, ref float bestDist, ref ItemType chosenType)
    {
        if (string.IsNullOrEmpty(tag)) return;
        GameObject[] items;
        try { items = GameObject.FindGameObjectsWithTag(tag); }
        catch (UnityException) { return; }

        foreach (GameObject item in items)
            ConsiderCandidate(item, type, ref best, ref bestDist, ref chosenType);
    }

    void ConsiderCandidate(GameObject item, ItemType type, ref GameObject best, ref float bestDist, ref ItemType chosenType)
    {
        if (item == null || !item.activeInHierarchy) return;
        Vector3 p = item.transform.position;
        if (IsNearKiller(p) || IsNearHuman(p)) return;

        float myDist = Vector3.Distance(transform.position, p);
        if (myDist >= bestDist) return;

        // leave it to another AI player who is closer to it, so they don't both run to the same item
        foreach (AICollectibleSeeker other in FindObjectsByType<AICollectibleSeeker>(FindObjectsSortMode.None))
        {
            if (other == this || other.hider == null || other.hider.IsCaught || other.hider.IsHidden) continue;
            if (other.targetItem == item) return;
            if (Vector3.Distance(other.transform.position, p) < myDist - 0.5f) return;
        }

        // Don't race the killer to an item or cross directly through his patrol corridor
        if (enemy != null)
        {
            Vector3 kPos = enemy.transform.position;
            float killerToItem = Vector3.Distance(kPos, p);
            if (killerToItem < myDist + 2f) return;
            if (DistanceToSegment(kPos, transform.position, p) < corridorBlockRadius) return;
        }

        bestDist = myDist;
        best = item;
        chosenType = type;
    }

    // ---------------- Helpers ----------------
    bool HasStone() => stones != null && stones.HasStone;
    bool HasFeather() => featherHolder != null && featherHolder.HasFeather;
    bool HasWhistle() => whistleHolder != null && whistleHolder.HasWhistle;
    bool HasDoorLock() => doorLockHolder != null && doorLockHolder.HasLock;
    bool HasIceGun() => iceGunHolder != null && iceGunHolder.HasIceGun;

    bool AlreadyHasTargetType()
    {
        switch (targetType)
        {
            case ItemType.Stone: return HasStone();
            case ItemType.Feather: return HasFeather();
            case ItemType.Whistle: return HasWhistle();
            case ItemType.DoorLock: return HasDoorLock();
            case ItemType.IceGun: return HasIceGun();
            default: return false;
        }
    }

    bool IsNearKiller(Vector3 pos)
    {
        // also while he is still in the elevator: items next to his starting point are not worth it
        return enemy != null && Vector3.Distance(pos, enemy.transform.position) < killerNearItemRadius;
    }

    bool IsNearHuman(Vector3 pos)
    {
        if (otherPlayers == null) return false;
        foreach (Hider h in otherPlayers)
        {
            if (h != null && h.isHumanControlled && !h.IsCaught &&
                Vector3.Distance(pos, h.transform.position) < leaveForHumanRadius)
                return true;
        }
        return false;
    }

    float KillerDist()
    {
        if (enemy == null || !enemy.enabled) return 999f;
        return Vector3.Distance(transform.position, enemy.transform.position);
    }

    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = 0f; a.y = 0f; b.y = 0f;
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-4f) return Vector3.Distance(point, a);
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2);
        return Vector3.Distance(point, a + ab * t);
    }

    void MoveTo(Vector3 pos)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void Release()
    {
        if (mode != Mode.Idle)
        {
            mode = Mode.Idle;
            if (ai != null) ai.externalControl = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.stoppingDistance = defaultStoppingDistance;
                agent.isStopped = false;
            }
        }
        targetItem = null;
        targetType = ItemType.None;
        instantTarget = null;
    }
}