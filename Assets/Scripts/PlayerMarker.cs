using UnityEngine;

// The through-walls outline around a player, in that player's colour (P1 blue, P2 red, P3 green, P4 pink).
// Never drawn on the player you are controlling, and switched off while the player hides or once caught.
[RequireComponent(typeof(Hider))]
public class PlayerMarker : MonoBehaviour
{
    [Tooltip("The player's visible model. Leave empty to use the first child with an Animator / skinned mesh.")]
    public Transform model;
    public float thickness = 3f;
    [Range(0f, 1f)] [Tooltip("0 = only the border, no filled body.")] public float fillAlpha = 0.35f;
    [Tooltip("While this player hides, the hiding spot is highlighted in their colour instead.")]
    public bool highlightHidingSpot = true;
    [Header("Fade when you get close")]
    [Tooltip("Full strength while you are farther than this from them.")]
    public float fadeStartDistance = 6f;
    [Tooltip("Completely gone once you are this close.")]
    public float fadeEndDistance = 1.5f;
    [Tooltip("For spots without their own model (fridge): meshes this close to the spot are highlighted.")]
    public float spotSearchRadius = 1f;
    [Range(0f, 1f)] public float opacity = 0.75f;
    public const string MarkerLayer = "PlayerOutline";
    public Material maskMaterial;      // Custom/DistractionMask
    public Material outlineMaterial;   // Custom/DistractionOutlineUI

    Hider hider;
    DistractionOutline outline;
    DistractionOutline spotOutline;
    HidingSpot shownSpot;
    static HidingSpot[] spots;
    static Hider human;

    void Start()
    {
        hider = GetComponent<Hider>();
        if (model == null)
        {
            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
            model = smr != null ? smr.transform.parent : transform;
        }
        // its own object, so it never mixes with an outline the player might already have
        GameObject holder = new GameObject("PlayerMarkerOutline");
        holder.transform.SetParent(transform, false);
        outline = holder.AddComponent<DistractionOutline>();
        outline.boundsRoot = model;
        outline.color = PlayerColors.For(hider);
        outline.thickness = thickness;
        outline.fillAlpha = fillAlpha;
        outline.maskMaterial = maskMaterial;
        outline.outlineMaterial = outlineMaterial;
        outline.layerName = MarkerLayer; // drawn separately from the appliance outlines
        outline.opacity = opacity;

        // a second outline for the hiding spot this player is in (rebuilt for each spot)
        GameObject sh = new GameObject("PlayerSpotOutline");
        sh.transform.SetParent(transform, false);
        spotOutline = sh.AddComponent<DistractionOutline>();
        spotOutline.color = outline.color;
        spotOutline.thickness = thickness;
        spotOutline.fillAlpha = fillAlpha;
        spotOutline.maskMaterial = maskMaterial;
        spotOutline.outlineMaterial = outlineMaterial;
        spotOutline.layerName = MarkerLayer;
        spotOutline.opacity = opacity;

        TintStoneMarker(outline.color);
    }

// re-reads this player's colour (e.g. gold while holding the golden knife, back to normal after)
    public void RefreshColor()
    {
        if (hider == null) hider = GetComponent<Hider>();
        Color c = PlayerColors.For(hider);
        if (outline != null) outline.SetColor(c);
        if (spotOutline != null) spotOutline.SetColor(c);
        TintStoneMarker(c);
    }


    // the stone target marker (and its beam) in this player's colour
    void TintStoneMarker(Color c)
    {
        GameObject marker = null;
        AIStoneBehaviour ai = GetComponent<AIStoneBehaviour>();
        if (ai != null) marker = ai.targetIndicator;
        PlayerStoneThrower human = GetComponent<PlayerStoneThrower>();
        if (marker == null && human != null) marker = human.targetIndicator;
        if (marker == null) return;

        foreach (Renderer r in marker.GetComponentsInChildren<Renderer>(true))
        {
            MaterialPropertyBlock b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            Material m = r.sharedMaterial;
            if (m == null) continue;
            if (m.HasProperty("_AoeColor")) { Color a = c; a.a = m.GetColor("_AoeColor").a; b.SetColor("_AoeColor", a); }
            if (m.HasProperty("_BaseColor")) b.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) b.SetColor("_Color", c);
            r.SetPropertyBlock(b);
        }
    }

    HidingSpot SpotOf(Hider h)
    {
        if (spots == null || spots.Length == 0 || spots[0] == null)
            spots = FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
        foreach (HidingSpot s in spots) if (s != null && s.Occupant == h) return s;
        return null;
    }

    void LateUpdate()
    {
        if (outline == null || hider == null) return;
        bool show = !hider.isHumanControlled && !hider.IsCaught && !hider.IsHidden && BlackoutRound.MarkersAllowed; // blackout: only while spectating

        // the closer you (the human player) get, the fainter the marker, until it disappears
        float fade = MarkerFade();
        if (fade <= 0.02f) show = false;
        outline.SetFade(fade);
        spotOutline.SetFade(fade);
        outline.SetVisible(show);

        // hiding: light up the spot they are in (other players only, never your own)
        HidingSpot spot = null;
        if (highlightHidingSpot && spotOutline != null && BlackoutRound.MarkersAllowed && !hider.isHumanControlled && !hider.IsCaught && hider.IsHidden)
            spot = SpotOf(hider);
        if (spot != shownSpot)
        {
            spotOutline.SetVisible(false);
            shownSpot = spot;
            if (spot != null)
            {
                spotOutline.boundsRoot = spot.transform;
                spotOutline.extraRenderers.Clear();
                // some spots (the fridge) keep their model outside the spot object: outline what sits right there
                // (only meshes named like the spot, e.g. 'Fridge_Hide' -> anything under a '...fridge...' object)
                string stem = spot.name.Split('_', ' ')[0].ToLowerInvariant();
                if (spot.GetComponentsInChildren<MeshRenderer>(false).Length == 0)
                    foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || r.GetComponentInParent<Hider>() != null || r.name == "OutlineMask") continue;
                        bool named = false;
                        for (Transform a = r.transform; a != null && !named; a = a.parent) named = a.name.ToLowerInvariant().Contains(stem);
                        if (!named) continue;
                        Vector3 c = r.bounds.center - spot.transform.position; c.y = 0f;
                        if (c.magnitude < spotSearchRadius && r.bounds.extents.magnitude < 2f) spotOutline.extraRenderers.Add(r);
                    }
                spotOutline.SetVisible(true);
            }
        }
        if (spot != null) spotOutline.SetVisible(fade > 0.02f);
    }

    float MarkerFade()
    {
        if (human == null || !human.isHumanControlled)
        {
            human = null;
            foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
                if (h.isHumanControlled) { human = h; break; }
        }
        if (human == null || human == hider || human.IsCaught) return 1f;
        float d = Vector3.Distance(human.transform.position, transform.position);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, d));
    }
}
