using UnityEngine;

// The colour each player is assigned: P1 blue, P2 red, P3 green, P4 pinkish magenta.
public static class PlayerColors
{
    public static readonly Color P1 = new Color(0.20f, 0.55f, 1.00f);
    public static readonly Color P2 = new Color(1.00f, 0.25f, 0.25f);
    public static readonly Color P3 = new Color(0.30f, 0.90f, 0.35f);
    public static readonly Color P4 = new Color(1.00f, 0.35f, 0.80f);
    // whoever holds the golden knife (Round 4) wears gold instead of their own colour
    public static readonly Color Gold = new Color(1.00f, 0.78f, 0.15f);

    public static Color For(Hider h)
    {
        if (h == null) return Color.white;
        if (KnifeHolder.IsHolder(h)) return Gold;
        string n = h.displayName ?? "";
        if (n.EndsWith("1")) return P1;
        if (n.EndsWith("2")) return P2;
        if (n.EndsWith("3")) return P3;
        if (n.EndsWith("4")) return P4;
        return Color.white;
    }
}
