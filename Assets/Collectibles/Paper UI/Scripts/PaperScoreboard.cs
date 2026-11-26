using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One player's line on the scoreboard (filled in by the RoundManager)
public class ScoreLine
{
    public enum Result { Survived, SurvivedWithKnife, Caught, Stabbed }
    public string name;
    public Sprite face;       // round face for the rows
    public Sprite portrait;   // close-up for the winner's polaroid (falls back to the face)
    public Color colour;      // the player's HUD colour
    public Result result;
    public string stabbedBy;  // who stabbed them (Round 4)
    public int gained;        // points this round
    public int total;         // total after this round
    public string history;    // one letter per round so far: s = survived, c = caught, k = stabbed
}

// The scoreboard: a notebook page taped over the frozen house (paper-craft style, see the design boards).
//  ShowRound: why the round ended, one row per player (+1 stickers, CAUGHT / STABBED stamps), what's next.
//  ShowFinal: the winner's polaroid with a WINNER rosette, final scores with each round's result, [R] to play again.
// Everything animates on unscaled time, because the house is frozen while it shows.
public class PaperScoreboard : MonoBehaviour
{
    public static readonly string[] RoundNames = { "You have a visitor", "Something is wrong with this house", "Lights out", "Sacrifices should be made" };

    static PaperScoreboard current;

    public static void ShowRound(int round, int totalRounds, string headline, bool goldHeadline, string subtitle, List<ScoreLine> lines)
    {
        PaperScoreboard b = Create();
        b.BuildRound(round, totalRounds, headline, goldHeadline, subtitle, lines);
    }

    public static void ShowFinal(List<ScoreLine> standings, List<ScoreLine> winners, int totalRounds)
    {
        PaperScoreboard b = Create();
        b.BuildFinal(standings, winners, totalRounds);
    }

    public static void Hide() { if (current != null) Destroy(current.gameObject); }

    static PaperScoreboard Create()
    {
        Hide();
        PaperToast.ClearAll(); // no toasts peeking out from under the scoreboard
        GameObject g = new GameObject("Scoreboard", typeof(Canvas), typeof(CanvasScaler));
        Canvas c = g.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 80; // over the HUD (45) and the old round UI (50), under the round intro shutter (500)
        PaperUI.Scaler(g.GetComponent<CanvasScaler>());
        current = g.AddComponent<PaperScoreboard>();
        current.born = Time.unscaledTime;
        return current;
    }

    void OnDestroy() { if (current == this) current = null; }

    // ---------------- little timeline: things appear at set times ----------------
    class Beat
    {
        public RectTransform r; public CanvasGroup g; public float at, dur; public int kind;
        public Vector2 home; public Vector3 homeScale;
        public TextMeshProUGUI number; public int from, to;
    }
    const int Fade = 0, SlideUp = 1, Slap = 2, Thump = 3, RowIn = 4, Drop = 5, Count = 6;
    readonly List<Beat> beats = new List<Beat>();
    float born;
    Image scrim;

    Beat Add(RectTransform r, float at, float dur, int kind)
    {
        Beat b = new Beat { r = r, at = at, dur = dur, kind = kind, home = r.anchoredPosition, homeScale = r.localScale };
        b.g = r.GetComponent<CanvasGroup>();
        if (b.g == null) b.g = r.gameObject.AddComponent<CanvasGroup>();
        b.g.blocksRaycasts = false;
        b.g.alpha = 0f;
        beats.Add(b);
        return b;
    }

    void Update()
    {
        float t = Time.unscaledTime - born;
        if (scrim != null) scrim.color = new Color(16f / 255f, 6f / 255f, 14f / 255f, 0.92f * Mathf.Clamp01(t / 0.3f)); // (the project blends UI in linear space, so it needs a high alpha to look this dark)
        foreach (Beat b in beats)
        {
            float k = Mathf.Clamp01((t - b.at) / Mathf.Max(0.01f, b.dur));
            if (b.kind == Count)
            {
                // the total shows the old score until the sticker lands, then ticks up with a bump
                int v = t >= b.at ? b.to : b.from;
                if (b.number.text != v.ToString()) b.number.text = v.ToString();
                float p = (t - b.at) / 0.3f;
                b.r.localScale = b.homeScale * (p > 0f && p < 1f ? 1f + 0.35f * Mathf.Sin(p * Mathf.PI) : 1f);
                continue;
            }
            if (t < b.at) { b.g.alpha = 0f; continue; }
            switch (b.kind)
            {
                case Fade: b.g.alpha = k; break;
                case SlideUp:
                    b.g.alpha = Mathf.Clamp01(k * 3f);
                    b.r.anchoredPosition = b.home + new Vector2(0f, -420f * (1f - PaperUI.EaseOutBack(k)));
                    break;
                case Slap:
                    b.g.alpha = Mathf.Clamp01(k * 4f);
                    b.r.localScale = b.homeScale * Mathf.LerpUnclamped(2.2f, 1f, PaperUI.EaseOutBack(k));
                    break;
                case Thump:
                    b.g.alpha = Mathf.Clamp01(k * 4f);
                    b.r.localScale = b.homeScale * Mathf.Lerp(2.4f, 1f, PaperUI.EaseOut(k));
                    break;
                case RowIn:
                    b.g.alpha = k;
                    b.r.anchoredPosition = b.home + new Vector2(-30f * (1f - PaperUI.EaseOut(k)), 0f);
                    break;
                case Drop:
                    b.g.alpha = Mathf.Clamp01(k * 3f);
                    b.r.anchoredPosition = b.home + new Vector2(0f, 300f * (1f - PaperUI.EaseOutBack(k)));
                    break;
            }
        }
    }

    // =====================================================================
    //  End of a round
    // =====================================================================
    void BuildRound(int round, int totalRounds, string headline, bool gold, string subtitle, List<ScoreLine> lines)
    {
        RectTransform root = Root();

        // "ROUND 2 OF 4 · OVER" on a strip of tape
        RectTransform tapeLabel = TapeLabel(root, "ROUND " + round + " OF " + totalRounds + " · OVER", 26f);
        Add(tapeLabel, 0.05f, 0.25f, Fade);

        PaperHeadline.Create(root, headline, 84f, 64f, gold, born + 0.15f);
        float sheetTop = 170f;
        if (!string.IsNullOrEmpty(subtitle))
        {
            TextMeshProUGUI sub = PaperUI.Text(root, "Subtitle", subtitle, 22f, PaperUI.Light, true, TextAlignmentOptions.Top);
            PaperUI.Place(sub.rectTransform, 0f, 152f, 1280f, 36f);
            Add(sub.rectTransform, 0.6f, 0.3f, Fade);
            sheetTop = 196f;
        }

        // the notebook page
        float w = 640f, h = 52f + lines.Count * 70f + 24f;
        RectTransform sheet = Sheet(root, (1280f - w) * 0.5f, sheetTop, w, h, -1f);
        Add(sheet, 0.35f, 0.45f, SlideUp);
        Header(sheet, "WHO MADE IT OUT", "THIS ROUND · TOTAL", 80f, 24f, 26f, w);

        float rowAt = 0.8f;
        for (int i = 0; i < lines.Count; i++)
        {
            ScoreLine l = lines[i];
            bool out_ = l.result == ScoreLine.Result.Caught || l.result == ScoreLine.Result.Stabbed;
            RectTransform row = PaperUI.Place(PaperUI.Rect("Row " + l.name, sheet), 0f, 52f + i * 70f, w, 70f);
            Add(row, rowAt, 0.3f, RowIn);

            Face(row, l, 80f, 9f, 52f, 4f, out_);

            TextMeshProUGUI name = PaperUI.Text(row, "Name", l.name, 22f, out_ ? PaperUI.Hex("1d1322", 0.55f) : PaperUI.Ink);
            float up = PaperUI.LineHeight(name, 1.2f);
            PaperUI.Place(name.rectTransform, 148f, 10f - up, 300f, 30f);
            name.ForceMeshUpdate();
            if (out_)
            {
                // struck out in red marker
                PaperShape strike = PaperUI.Shape(row, "Strike", PaperUI.Hex("c8171f", 0.85f), 2f);
                PaperUI.Place(strike.rectTransform, 144f, 22f, name.preferredWidth + 10f, 4f);
                PaperUI.Rotate(strike.rectTransform, -2f);
            }
            Outcome(row, l, 148f, 40f, rowAt + 0.2f);

            // +1 sticker (or a faint +0) and the total
            float totX = w - 24f - 52f;
            if (l.gained > 0)
            {
                RectTransform st = Sticker(row, "+" + l.gained, totX - 16f - 46f, 12f, 46f, 8f);
                Add(st, rowAt + 0.25f, 0.25f, Slap);
            }
            else
            {
                TextMeshProUGUI zero = PaperUI.Text(row, "Zero", "+0", 18f, PaperUI.Hex("1d1322", 0.3f), true, TextAlignmentOptions.Center);
                PaperUI.Place(zero.rectTransform, totX - 16f - 46f, 20f, 46f, 30f);
            }
            PaperShape tag = PaperUI.Shape(row, "Total", PaperUI.Ink, 10f);
            PaperUI.Place(tag.rectTransform, totX, 12f, 52f, 46f);
            TextMeshProUGUI num = PaperUI.Text(tag.transform, "Number", (l.total - l.gained).ToString(), 26f, PaperUI.Light, true, TextAlignmentOptions.Center);
            PaperUI.Stretch(num.rectTransform);
            num.rectTransform.offsetMax = new Vector2(0f, -2f);
            if (out_) tag.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            tag.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tag.rectTransform.anchoredPosition += new Vector2(26f, -23f);
            beats.Add(new Beat { r = tag.rectTransform, at = rowAt + 0.3f, dur = 0.3f, kind = Count, number = num, from = l.total - l.gained, to = l.total, homeScale = Vector3.one });

            rowAt += 0.25f;
        }

        // what's next, with the 4 round tabs
        if (round < totalRounds)
        {
            RectTransform next = NextUp(root, round, totalRounds, 628f);
            Add(next, rowAt + 0.35f, 0.3f, Slap);
        }
    }

    // survived / with the knife / CAUGHT stamp / STABBED stamp + by whom
    void Outcome(RectTransform row, ScoreLine l, float x, float y, float at)
    {
        RectTransform line = PaperUI.Place(PaperUI.Rect("Outcome", row), x, y, 320f, 22f);
        switch (l.result)
        {
            case ScoreLine.Result.Survived:
            case ScoreLine.Result.SurvivedWithKnife:
            {
                bool knife = l.result == ScoreLine.Result.SurvivedWithKnife;
                Image icon = PaperUI.Img(line, "Icon", PaperUI.Icon(knife ? "cleaver_icon" : "tick"));
                PaperUI.Place(icon.rectTransform, 0f, knife ? -1f : 4f, knife ? 22f : 16f, knife ? 22f : 14f);
                TextMeshProUGUI t = PaperUI.Text(line, "Text", knife ? "Survived with the golden knife" : "Made it through the round", 14f,
                                                 knife ? PaperUI.Hex("9a6a00") : PaperUI.Hex("2f8f3f"));
                PaperUI.Place(t.rectTransform, knife ? 28f : 22f, -1f, 290f, 24f);
                break;
            }
            default:
            {
                bool stab = l.result == ScoreLine.Result.Stabbed;
                RectTransform s = Stamp(line, stab ? "STABBED" : "CAUGHT", 0f, -4f);
                Add(s, at, 0.22f, Thump);
                TextMeshProUGUI by = PaperUI.Text(line, "By", stab ? "by " + l.stabbedBy : "by the clown", 13f, PaperUI.Hex("8a7a6a"), false);
                float bx = s.sizeDelta.x + 10f;
                PaperUI.Place(by.rectTransform, bx, 1f, 200f, 22f);
                if (stab)
                {
                    by.ForceMeshUpdate();
                    Image ci = PaperUI.Img(line, "Cleaver", PaperUI.Icon("cleaver_icon"));
                    PaperUI.Place(ci.rectTransform, bx + by.preferredWidth + 5f, 0f, 20f, 20f);
                }
                break;
            }
        }
    }

    // =====================================================================
    //  Match over
    // =====================================================================
    void BuildFinal(List<ScoreLine> standings, List<ScoreLine> winners, int totalRounds)
    {
        RectTransform root = Root();
        PaperHeadline.Create(root, winners.Count > 1 ? "IT'S A TIE!" : "MATCH OVER", 80f, 30f, false, born + 0.15f);

        // the winner's polaroid (one per winner when it's a tie)
        int n = Mathf.Max(1, winners.Count);
        float scale = n == 1 ? 1f : n == 2 ? 0.72f : 0.56f;
        List<RectTransform> pols = new List<RectTransform>();
        for (int i = 0; i < winners.Count; i++)
        {
            float x = n == 1 ? 120f : 40f + i * (360f / n);
            float y = n == 1 ? 170f : 190f + (i % 2) * 40f;
            RectTransform pol = Polaroid(root, winners[i], x, y, scale, (i % 2 == 0 ? -4f : 4f), totalRounds);
            pols.Add(pol);
            Add(pol, 0.45f + i * 0.15f, 0.5f, Drop);
        }

        // final scores
        float w = 660f, h = 50f + standings.Count * 70f + 20f;
        RectTransform sheet = Sheet(root, 500f, 176f, w, h, 1f);
        Add(sheet, 0.35f, 0.45f, SlideUp);
        foreach (RectTransform p in pols) p.SetAsLastSibling(); // the polaroids lie on top of the score sheet
        Header(sheet, "FINAL SCORES", "", 76f, 26f, 24f, w);
        // round numbers over the history cells
        float totX = w - 26f - 54f;
        float histX = totX - 14f - (totalRounds * 33f - 5f);
        for (int r = 0; r < totalRounds; r++)
        {
            TextMeshProUGUI rn = PaperUI.Text(sheet, "Round " + (r + 1), (r + 1).ToString(), 12f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.Top);
            PaperUI.Place(rn.rectTransform, histX + r * 33f, 24f, 28f, 20f);
        }
        TextMeshProUGUI tl = PaperUI.Text(sheet, "Total label", "TOTAL", 12f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.TopRight);
        PaperUI.LetterSpacing(tl, 2f);
        PaperUI.Place(tl.rectTransform, totX - 20f, 24f, 74f, 20f);

        int best = standings.Count > 0 ? standings[0].total : 0;
        int rank = 0, prevTotal = int.MinValue;
        for (int i = 0; i < standings.Count; i++)
        {
            ScoreLine l = standings[i];
            if (l.total != prevTotal) { rank = i + 1; prevTotal = l.total; } // ties share a place
            RectTransform row = PaperUI.Place(PaperUI.Rect("Row " + l.name, sheet), 0f, 50f + i * 70f, w, 70f);
            Add(row, 0.8f + i * 0.22f, 0.3f, RowIn);

            Color medal = rank == 1 ? PaperUI.Gold : rank == 2 ? PaperUI.Hex("d5d9de") : rank == 3 ? PaperUI.Hex("d09452") : PaperUI.Hex("e6d8bb");
            PaperShape m = PaperUI.Shape(row, "Place", medal, 17f);
            PaperUI.Place(m.rectTransform, 76f, 18f, 34f, 34f);
            m.edge = 2f; m.edgeColor = PaperUI.Ink;
            TextMeshProUGUI mt = PaperUI.Text(m.transform, "N", rank.ToString(), 17f, PaperUI.Ink, true, TextAlignmentOptions.Center);
            PaperUI.Stretch(mt.rectTransform); mt.rectTransform.offsetMax = new Vector2(0f, -1f);

            Face(row, l, 124f, 13f, 44f, 3f, false); // the final table always shows the face

            TextMeshProUGUI name = PaperUI.Text(row, "Name", l.name, 21f, PaperUI.Ink);
            PaperUI.Place(name.rectTransform, 182f, 18f, 220f, 34f);

            for (int r = 0; r < totalRounds; r++)
            {
                PaperShape cell = PaperUI.Shape(row, "R" + (r + 1), PaperUI.Hex("1d1322", 0.22f), 7f);
                cell.ring = 2f;
                PaperUI.Place(cell.rectTransform, histX + r * 33f, 21f, 28f, 28f);
                char c = l.history != null && r < l.history.Length ? l.history[r] : ' ';
                string icon = c == 's' ? "tick" : c == 'c' ? "skull_ink" : c == 'k' ? "cleaver_icon" : null;
                if (icon == null) continue;
                Image ic = PaperUI.Img(cell.transform, "Icon", icon == "skull_ink" ? null : PaperUI.Icon(icon));
                if (icon == "skull_ink") { ic.sprite = SkullFor(l); }
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ic.rectTransform.sizeDelta = c == 's' ? new Vector2(16f, 14f) : new Vector2(19f, 19f);
                if (c == 'c') ic.color = PaperUI.Hex("1d1322", 0.85f);
            }

            TextMeshProUGUI tot = PaperUI.Text(row, "Total", l.total.ToString(), 36f, l.total == best ? PaperUI.Red : PaperUI.Ink, true, TextAlignmentOptions.TopRight);
            tot.font = PaperUI.Title;
            PaperUI.Place(tot.rectTransform, totX, 16f, 54f, 44f);
        }

        // [R] to play again
        RectTransform again = PaperUI.Rect("Play again", root);
        PaperShape scrap = PaperUI.Cut(PaperUI.Shape(again, "Scrap", PaperUI.CardDark, 14f));
        RectTransform key = PaperUI.KeyCap(again, "R", false);
        TextMeshProUGUI tx = PaperUI.Text(again, "Text", "to play again", 21f, PaperUI.Light);
        tx.ForceMeshUpdate();
        float aw = 12f + 44f + 14f + tx.preferredWidth + 22f, ah = 64f;
        PaperUI.Place(again, (1280f - aw) * 0.5f, 626f, aw, ah);
        PaperUI.Stretch(scrap.rectTransform);
        PaperUI.Place(key, 12f, 10f, 44f, 44f);
        float up = PaperUI.LineHeight(tx, PaperUI.NormalLine);
        PaperUI.Place(tx.rectTransform, 70f, (ah - 21f * PaperUI.NormalLine) * 0.5f - up, tx.preferredWidth + 4f, 34f);
        again.pivot = new Vector2(0.5f, 0.5f);
        again.anchoredPosition += new Vector2(aw * 0.5f, -ah * 0.5f);
        Add(again, 0.9f + standings.Count * 0.22f + 0.3f, 0.3f, Slap);
    }

    // the skull for the history cells: the HUD's dead sprite if there is one
    Sprite skull;
    Sprite SkullFor(ScoreLine l)
    {
        if (skull == null)
        {
            PlayerHUD hud = FindFirstObjectByType<PlayerHUD>();
            skull = hud != null && hud.deadSprite != null ? hud.deadSprite : PaperUI.Icon("skull");
        }
        return skull;
    }

    RectTransform Polaroid(RectTransform root, ScoreLine l, float x, float y, float scale, float rot, int totalRounds)
    {
        RectTransform p = PaperUI.Place(PaperUI.Rect("Polaroid " + l.name, root), x, y, 320f, 400f);
        p.localScale = Vector3.one * scale;
        PaperShape card = PaperUI.Shape(p, "Card", PaperUI.Cream, 4f);
        PaperUI.Stretch(card.rectTransform);
        card.shadow = true; card.shadowOffset = new Vector2(6f, -9f); card.shadowColor = PaperUI.Shadow;
        PaperShape back = PaperUI.Shape(p, "Photo back", PaperUI.Ink, 0f);
        PaperUI.Place(back.rectTransform, 20f, 20f, 280f, 280f);
        Image photo = PaperUI.Img(back.transform, "Photo", l.portrait != null ? l.portrait : l.face);
        photo.preserveAspect = false;
        PaperUI.Stretch(photo.rectTransform);
        PaperShape strip = PaperUI.Shape(p, "Colour", l.colour, 0f);
        PaperUI.Place(strip.rectTransform, 20f, 292f, 280f, 8f);

        TextMeshProUGUI name = PaperUI.Text(p, "Name", l.name.ToUpperInvariant(), 34f, PaperUI.Ink, true, TextAlignmentOptions.Top);
        name.font = PaperUI.Title;
        PaperUI.Place(name.rectTransform, 0f, 314f, 320f, 40f);
        int survived = 0;
        if (l.history != null) foreach (char c in l.history) if (c == 's') survived++;
        TextMeshProUGUI cap = PaperUI.Text(p, "Caption", "survived " + survived + " of the " + totalRounds + " rounds", 15f, PaperUI.Hex("6e5f52"), false, TextAlignmentOptions.Top);
        PaperUI.Place(cap.rectTransform, 0f, 352f, 320f, 24f);

        PaperShape tape = PaperUI.Shape(p, "Tape", PaperUI.Hex("f6ead0", 0.82f), 0f);
        PaperUI.Place(tape.rectTransform, 110f, -12f, 100f, 28f);
        PaperUI.Rotate(tape.rectTransform, 3f);

        // WINNER rosette
        RectTransform ros = PaperUI.Place(PaperUI.Rect("Rosette", p), 320f - 110f + 34f, -30f, 110f, 150f);
        PaperUI.Rotate(ros, 12f);
        Vector2[] tail = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.84f), new Vector2(0f, 1f) };
        PaperShape t1 = PaperUI.Torn(ros, "Tail L", PaperUI.Red, tail);
        PaperUI.Place(t1.rectTransform, 26f, 70f, 26f, 74f); PaperUI.Rotate(t1.rectTransform, 14f);
        t1.shadow = true; t1.shadowOffset = new Vector2(4f, -6f); t1.shadowColor = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.45f);
        PaperShape t2 = PaperUI.Torn(ros, "Tail R", PaperUI.Hex("a5121a"), tail);
        PaperUI.Place(t2.rectTransform, 58f, 70f, 26f, 74f); PaperUI.Rotate(t2.rectTransform, -12f);
        PaperShape disc = PaperUI.Shape(ros, "Disc", PaperUI.Gold, 55f);
        PaperUI.Place(disc.rectTransform, 0f, 0f, 110f, 110f);
        disc.shadow = true; disc.shadowOffset = new Vector2(4f, -6f); disc.shadowColor = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.45f);
        PaperShape inner = PaperUI.Shape(disc.transform, "Ring", PaperUI.Hex("e0a92a"), 48f);
        inner.ring = 2f; PaperUI.Stretch(inner.rectTransform, 7f);
        PaperShape innerInk = PaperUI.Shape(disc.transform, "Ink ring", PaperUI.Ink, 46f);
        innerInk.ring = 2f; PaperUI.Stretch(innerInk.rectTransform, 9f);
        TextMeshProUGUI the = PaperUI.Text(disc.transform, "The", "THE", 11f, PaperUI.Hex("7a5200"), true, TextAlignmentOptions.Top);
        PaperUI.LetterSpacing(the, 2f);
        PaperUI.Place(the.rectTransform, 0f, 30f, 110f, 16f);
        TextMeshProUGUI win = PaperUI.Text(disc.transform, "Winner", "WINNER", 24f, PaperUI.Ink, true, TextAlignmentOptions.Top);
        win.font = PaperUI.Title;
        PaperUI.Place(win.rectTransform, 0f, 46f, 110f, 30f);
        Add(ros, 1.0f, 0.25f, Slap);

        PaperUI.Rotate(p, rot);
        p.localScale = Vector3.one * scale;
        return p;
    }

    // =====================================================================
    //  shared pieces
    // =====================================================================
    RectTransform Root()
    {
        RectTransform root = PaperUI.Rect("Board", transform);
        PaperUI.Stretch(root);
        scrim = root.gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0f);
        scrim.raycastTarget = false;
        return root;
    }

    static readonly Vector2[] SheetTear = Pts(0, 3, 6, 0, 14, 2.5f, 23, .5f, 31, 3, 40, 0, 49, 2.5f, 58, .5f, 67, 3, 76, 0, 85, 2.5f, 93, .5f, 100, 3, 100, 100, 0, 100);
    static readonly Vector2[] Burst = Pts(50, 0, 61, 12, 76, 6, 79, 22, 95, 25, 89, 40, 100, 50, 89, 60, 95, 75, 79, 78, 76, 94, 61, 88, 50, 100, 39, 88, 24, 94, 21, 78, 5, 75, 11, 60, 0, 50, 11, 40, 5, 25, 21, 22, 24, 6, 39, 12);
    static Vector2[] Pts(params float[] v)
    {
        Vector2[] p = new Vector2[v.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(v[i * 2] / 100f, v[i * 2 + 1] / 100f);
        return p;
    }

    // a ruled notebook page with a red margin and tape on its top corners
    RectTransform Sheet(RectTransform root, float x, float y, float w, float h, float rot)
    {
        RectTransform s = PaperUI.Place(PaperUI.Rect("Sheet", root), x, y, w, h);
        PaperShape paper = PaperUI.Torn(s, "Paper", PaperUI.Cream, SheetTear);
        PaperUI.Stretch(paper.rectTransform);
        paper.shadow = true; paper.shadowOffset = new Vector2(6f, -9f); paper.shadowColor = PaperUI.Shadow;
        for (float ly = 69f; ly < h - 4f; ly += 70f)
        {
            PaperShape rule = PaperUI.Shape(s, "Rule", PaperUI.Hex("1d1322", 0.09f), 0f);
            PaperUI.Place(rule.rectTransform, 0f, ly, w, 2f);
        }
        PaperShape margin = PaperUI.Shape(s, "Margin", PaperUI.Hex("c8171f", 0.35f), 0f);
        PaperUI.Place(margin.rectTransform, 60f, h * 0.03f, 2f, h * 0.97f);
        PaperShape t1 = PaperUI.Shape(s, "Tape L", PaperUI.Hex("f6ead0", 0.82f), 0f);
        PaperUI.Place(t1.rectTransform, -18f, -8f, 90f, 26f); PaperUI.Rotate(t1.rectTransform, -14f);
        PaperShape t2 = PaperUI.Shape(s, "Tape R", PaperUI.Hex("f6ead0", 0.82f), 0f);
        PaperUI.Place(t2.rectTransform, w - 72f, -8f, 90f, 26f); PaperUI.Rotate(t2.rectTransform, 12f);
        PaperUI.Rotate(s, rot);
        return s;
    }

    static void Header(RectTransform sheet, string left, string right, float x, float rightPad, float y, float w)
    {
        TextMeshProUGUI l = PaperUI.Text(sheet, "Header", left, 12f, PaperUI.Hex("8a7a6a"));
        PaperUI.LetterSpacing(l, 2f);
        PaperUI.Place(l.rectTransform, x, y, 300f, 20f);
        if (string.IsNullOrEmpty(right)) return;
        TextMeshProUGUI r = PaperUI.Text(sheet, "Header right", right, 12f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.TopRight);
        PaperUI.LetterSpacing(r, 2f);
        PaperUI.Place(r.rectTransform, w - rightPad - 300f, y, 300f, 20f);
    }

    static void Face(RectTransform row, ScoreLine l, float x, float y, float size, float ringW, bool caught)
    {
        PaperShape ring = PaperUI.Shape(row, "Face", caught ? PaperUI.Light : l.colour, size * 0.5f);
        PaperUI.Place(ring.rectTransform, x, y, size, size);
        ring.edge = 2f; ring.edgeColor = PaperUI.Ink;
        PaperShape back = PaperUI.Shape(ring.transform, "Back", caught ? PaperUI.Ink : PaperUI.CardDark, size * 0.5f - ringW);
        PaperUI.Stretch(back.rectTransform, ringW);
        Sprite s = caught ? null : (l.face != null ? l.face : PaperUI.Icon("p4"));
        if (caught)
        {
            PlayerHUD hud = FindFirstObjectByType<PlayerHUD>();
            s = hud != null && hud.deadSprite != null ? hud.deadSprite : PaperUI.Icon("skull");
        }
        Image img = PaperUI.Img(ring.transform, "Picture", s);
        PaperUI.Stretch(img.rectTransform, caught ? ringW + 7f : ringW);
    }

    static RectTransform Stamp(RectTransform parent, string text, float x, float y)
    {
        RectTransform s = PaperUI.Rect("Stamp " + text, parent);
        PaperShape border = PaperUI.Shape(s, "Border", PaperUI.Hex("c8171f", 0.92f), 6f);
        border.ring = 2.5f;
        PaperUI.Stretch(border.rectTransform);
        TextMeshProUGUI t = PaperUI.Text(s, "Text", text, 14f, PaperUI.Hex("c8171f", 0.92f));
        PaperUI.LetterSpacing(t, 2f);
        t.ForceMeshUpdate();
        float w = t.preferredWidth + 16f + 5f + 2f, h = 14f * PaperUI.NormalLine + 2f + 5f;
        PaperUI.Place(t.rectTransform, 10.5f, 4.5f, w, 24f);
        PaperUI.Place(s, x, y, w, h);
        PaperUI.Rotate(s, -6f);
        return s;
    }

    static RectTransform Sticker(RectTransform parent, string text, float x, float y, float size, float rot)
    {
        PaperShape s = PaperUI.Torn(parent, "Sticker", PaperUI.Gold, Burst);
        PaperUI.Place(s.rectTransform, x, y, size, size);
        s.shadow = true; s.shadowOffset = new Vector2(2f, -3f); s.shadowColor = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.4f);
        TextMeshProUGUI t = PaperUI.Text(s.transform, "Text", text, size * 0.4f, PaperUI.Ink, true, TextAlignmentOptions.Center);
        PaperUI.Stretch(t.rectTransform);
        t.rectTransform.offsetMax = new Vector2(0f, -1f);
        PaperUI.Rotate(s.rectTransform, rot);
        return s.rectTransform;
    }

    static RectTransform TapeLabel(RectTransform root, string text, float y)
    {
        RectTransform r = PaperUI.Rect("Round label", root);
        TextMeshProUGUI t = PaperUI.Text(r, "Text", text, 15f, PaperUI.Ink);
        PaperUI.LetterSpacing(t, 3f);
        t.ForceMeshUpdate();
        float w = t.preferredWidth + 44f, h = 15f * PaperUI.NormalLine + 7f;
        PaperShape tape = PaperUI.Shape(r, "Tape", PaperUI.Hex("f6ead0", 0.82f), 0f);
        PaperUI.Stretch(tape.rectTransform);
        tape.transform.SetAsFirstSibling();
        PaperUI.Place(t.rectTransform, 22f, 5f, w, 26f);
        PaperUI.Place(r, (1280f - w) * 0.5f, y, w, h);
        PaperUI.Rotate(r, -2f);
        return r;
    }

    // "NEXT UP · Round 3 · Lights out" with the four round tabs
    static RectTransform NextUp(RectTransform root, int round, int totalRounds, float y)
    {
        RectTransform r = PaperUI.Rect("Next up", root);
        PaperShape scrap = PaperUI.Cut(PaperUI.Shape(r, "Scrap", PaperUI.CardDark, 14f));
        TextMeshProUGUI lbl = PaperUI.Text(r, "Label", "NEXT UP", 11f, PaperUI.Gold);
        PaperUI.LetterSpacing(lbl, 2f);
        string nm = round < RoundNames.Length ? RoundNames[round] : "";
        TextMeshProUGUI title = PaperUI.Text(r, "Title", "Round " + (round + 1) + (nm.Length > 0 ? " · " + nm : ""), 19f, PaperUI.Light);
        title.ForceMeshUpdate();
        float textW = Mathf.Max(title.preferredWidth, 60f);
        float tabsW = totalRounds * 36f - 6f;
        float w = 18f + textW + 18f + tabsW + 20f, h = 58f;
        PaperUI.Place(r, (1280f - w) * 0.5f, y, w, h);
        PaperUI.Stretch(scrap.rectTransform);
        PaperUI.Place(lbl.rectTransform, 18f, 9f, textW, 18f);
        PaperUI.Place(title.rectTransform, 18f, 21f, textW + 4f, 30f);
        for (int i = 0; i < totalRounds; i++)
        {
            int n = i + 1;
            float tx = 18f + textW + 18f + i * 36f;
            bool done = n <= round, now = n == round + 1;
            if (done || now)
            {
                PaperShape tab = PaperUI.Shape(r, "Tab " + n, now ? PaperUI.Red : PaperUI.Cream, 8f);
                PaperUI.Place(tab.rectTransform, tx, 14f, 30f, 30f);
                if (now) { tab.edge = 2f; tab.edgeColor = PaperUI.Light; }
                TextMeshProUGUI t = PaperUI.Text(tab.transform, "N", n.ToString(), 15f, now ? PaperUI.Light : PaperUI.Ink, true, TextAlignmentOptions.Center);
                PaperUI.Stretch(t.rectTransform); t.rectTransform.offsetMax = new Vector2(0f, -1f);
            }
            else
            {
                Image dash = PaperUI.Img(r, "Tab " + n, PaperUI.Icon("slot"), PaperUI.Hex("f1e4c6", 0.35f));
                PaperUI.Place(dash.rectTransform, tx, 14f, 30f, 30f);
                TextMeshProUGUI t = PaperUI.Text(dash.transform, "N", n.ToString(), 15f, PaperUI.Hex("f1e4c6", 0.45f), true, TextAlignmentOptions.Center);
                PaperUI.Stretch(t.rectTransform); t.rectTransform.offsetMax = new Vector2(0f, -1f);
            }
        }
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition += new Vector2(w * 0.5f, -h * 0.5f);
        return r;
    }
}

// The big paper-cut headline (Luckiest Guy, like the round cards): red (or gold) letters with a thick cream
// outline and a hard shadow, each letter tilted a little, popping in one by one.
public class PaperHeadline : MonoBehaviour
{
    TextMeshProUGUI text;
    float startAt;
    const float Stagger = 0.05f, PopTime = 0.28f;

    public static PaperHeadline Create(RectTransform parent, string words, float size, float top, bool gold, float startAt)
    {
        TextMeshProUGUI t = PaperUI.Text(parent, "Headline", words.ToUpperInvariant(), size, Color.white, true, TextAlignmentOptions.Top);
        t.font = PaperUI.Title;
        PaperUI.Place(t.rectTransform, 0f, top, 1280f, size * 1.2f);
        t.enableVertexGradient = true;
        t.colorGradient = gold
            ? new VertexGradient(PaperUI.Hex("ffd75a"), PaperUI.Hex("ffd75a"), PaperUI.Hex("d79a17"), PaperUI.Hex("f6c443"))
            : new VertexGradient(PaperUI.Hex("e3262b"), PaperUI.Hex("e3262b"), PaperUI.Hex("93101a"), PaperUI.Hex("c8171f"));
        Material m = t.fontMaterial; // its own copy
        m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.25f);
        m.SetColor(ShaderUtilities.ID_OutlineColor, PaperUI.CutEdge);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.6f);
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(8f / 255f, 2f / 255f, 12f / 255f, 0.55f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.55f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.85f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        t.UpdateMeshPadding();
        PaperHeadline h = t.gameObject.AddComponent<PaperHeadline>();
        h.text = t;
        h.startAt = startAt;
        return h;
    }

    void LateUpdate()
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        System.Random rng = new System.Random(text.text.GetHashCode());
        float now = Time.unscaledTime;
        int visibleIndex = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            float rot = (float)(rng.NextDouble() * 14.0 - 7.0);
            float dy = (float)(rng.NextDouble() * 12.0 - 6.0);
            if (!c.isVisible) continue;
            float k = (now - startAt - visibleIndex * Stagger) / PopTime;
            visibleIndex++;
            float s = k <= 0f ? 0f : (k < 1f ? PaperUI.EaseOutBack(k) : 1f);

            Vector3[] v = info.meshInfo[c.materialReferenceIndex].vertices;
            int vi = c.vertexIndex;
            Vector3 mid = (v[vi] + v[vi + 2]) * 0.5f;
            Matrix4x4 mtx = Matrix4x4.TRS(new Vector3(0f, -dy, 0f), Quaternion.Euler(0f, 0f, -rot), Vector3.one * s);
            for (int j = 0; j < 4; j++) v[vi + j] = mid + mtx.MultiplyPoint3x4(v[vi + j] - mid);
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}
