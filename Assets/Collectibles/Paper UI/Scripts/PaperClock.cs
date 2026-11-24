using TMPro;
using UnityEngine;

// The round timer: a paper wall clock top-right, under the last player card.
// Kraft rim, cream face, a red slice that is the time left (the hand sweeps with it), and the time on a torn timeTag.
// Last 10 seconds: the timeTag turns red and the clock jolts every second.
// Lights out (Round 3 blackout): dark face with a neon green rim, hand and digits.
public class PaperClock : MonoBehaviour
{
    static readonly Color Kraft = PaperUI.Hex("b9874e");
    static readonly Color Neon = PaperUI.Hex("b6ff9e");
    static readonly Color NeonText = PaperUI.Hex("eaffe6");
    // the board's see-through slices as they look over the rim colour: red .7 / .85 on kraft, green .18 on the dark rim
    static readonly Color SliceNormal = PaperUI.Hex("c4392d");
    static readonly Color SliceUrgent = PaperUI.Hex("c62826");
    static readonly Color SliceDark = PaperUI.Hex("263d2e");

    RectTransform clock, hand, timeTag;
    PaperShape rim, face, slice, hub, tagPaper, glow1, glow2;
    PaperShape[] ticks = new PaperShape[4];
    TextMeshProUGUI digits;
    int lastSecond = -1;
    float joltAt = -9f;
    bool? wasDark, wasUrgent;

    public static PaperClock Create()
    {
        Canvas c = PaperUI.HudCanvas;
        RectTransform root = PaperUI.Rect("Round Clock", c.transform);
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-30f, -118f);
        root.sizeDelta = new Vector2(96f, 150f);
        PaperClock pc = root.gameObject.AddComponent<PaperClock>();
        pc.Build();
        return pc;
    }

    void Build()
    {
        RectTransform root = (RectTransform)transform;
        clock = PaperUI.Place(PaperUI.Rect("Clock", root), 0f, 0f, 96f, 96f);
        clock.pivot = new Vector2(0.5f, 0.5f); clock.anchoredPosition = new Vector2(48f, -48f);

        glow2 = PaperUI.Shape(clock, "Glow Wide", PaperUI.Hex("2fe04f", 0.12f), 70f);
        PaperUI.Stretch(glow2.rectTransform, -14f);
        glow1 = PaperUI.Shape(clock, "Glow", PaperUI.Hex("5dff6e", 0.3f), 60f);
        PaperUI.Stretch(glow1.rectTransform, -5f);

        rim = PaperUI.Cut(PaperUI.Shape(clock, "Rim", Kraft, 48f));
        PaperUI.Stretch(rim.rectTransform);
        face = PaperUI.Shape(clock, "Face", PaperUI.Cream, 41f);
        PaperUI.Stretch(face.rectTransform, 7f);
        slice = PaperUI.Shape(clock, "Time Left", SliceNormal, 41f);
        PaperUI.Stretch(slice.rectTransform, 7f);

        // 12 o'clock is solid, the other three ticks are faint
        ticks[0] = Tick("Tick 12", 46f, 9f, 4f, 9f);
        ticks[1] = Tick("Tick 6", 46f, 96f - 18f, 4f, 9f);
        ticks[2] = Tick("Tick 9", 9f, 46f, 9f, 4f);
        ticks[3] = Tick("Tick 3", 96f - 18f, 46f, 9f, 4f);

        PaperShape h = PaperUI.Shape(clock, "Hand", PaperUI.Ink, 2f);
        hand = h.rectTransform;
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
        hand.pivot = new Vector2(0.5f, 0f);
        hand.sizeDelta = new Vector2(4f, 36f);
        hand.anchoredPosition = Vector2.zero;
        hub = PaperUI.Shape(clock, "Hub", PaperUI.Ink, 6f);
        hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hub.rectTransform.sizeDelta = new Vector2(12f, 12f);

        // the time on a torn timeTag, 8 px under the clock
        timeTag = PaperUI.Rect("Tag", root);
        timeTag.anchorMin = timeTag.anchorMax = new Vector2(0.5f, 1f);
        timeTag.pivot = new Vector2(0.5f, 1f);
        timeTag.anchoredPosition = new Vector2(0f, -104f);
        tagPaper = PaperUI.Cut(PaperUI.Torn(timeTag, "Paper", PaperUI.Cream, PaperUI.TagTear));
        PaperUI.Stretch(tagPaper.rectTransform);
        digits = PaperUI.Text(timeTag, "Time", "0:00", 24f, PaperUI.Ink, true, TextAlignmentOptions.Top);
        PaperUI.Stretch(digits.rectTransform);
        digits.rectTransform.offsetMax = new Vector2(0f, -4f); // padding-top 4
        timeTag.sizeDelta = new Vector2(80f, 24f * PaperUI.NormalLine + 5f);
        SetLook(false, false);
    }

    PaperShape Tick(string n, float x, float y, float w, float h)
    {
        PaperShape s = PaperUI.Shape(clock, n, PaperUI.Ink, 2f);
        PaperUI.Place(s.rectTransform, x, y, w, h);
        return s;
    }

    // called every frame by the RoundManager
    public void Set(float secondsLeft, float roundLength, bool lightsOut)
    {
        float frac = roundLength > 0f ? Mathf.Clamp01(secondsLeft / roundLength) : 0f;
        bool urgent = secondsLeft <= 10f && secondsLeft > 0f;
        if (wasDark != lightsOut || wasUrgent != urgent) SetLook(lightsOut, urgent);

        slice.pie = frac;
        slice.Refresh();
        hand.localEulerAngles = new Vector3(0f, 0f, -360f * frac);

        int s = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
        string text = (s / 60) + ":" + (s % 60).ToString("00");
        if (digits.text != text)
        {
            digits.text = text;
            digits.ForceMeshUpdate();
            timeTag.sizeDelta = new Vector2(digits.preferredWidth + 32f, 24f * PaperUI.NormalLine + 5f);
        }
        if (urgent && s != lastSecond) joltAt = Time.unscaledTime;
        lastSecond = s;

        // the jolt: a quick shake of the clock and a bump of the timeTag
        float j = (Time.unscaledTime - joltAt) / 0.3f;
        float shake = j < 1f ? Mathf.Sin(j * 30f) * (1f - j) * 7f : 0f;
        clock.localEulerAngles = new Vector3(0f, 0f, shake);
        timeTag.localScale = Vector3.one * (j < 1f ? 1f + 0.15f * (1f - j) : 1f);
    }

    void SetLook(bool dark, bool urgent)
    {
        wasDark = dark; wasUrgent = urgent;
        glow1.gameObject.SetActive(dark);
        glow2.gameObject.SetActive(dark);

        rim.color = dark ? PaperUI.Hex("1a1220") : Kraft;
        rim.edge = dark ? 2f : 3f;
        rim.edgeColor = dark ? Neon : PaperUI.CutEdge;
        rim.shadow = !dark;
        rim.Refresh();
        face.color = dark ? PaperUI.Hex("0b0710") : PaperUI.Cream;
        slice.color = dark ? SliceDark : urgent ? SliceUrgent : SliceNormal;
        Color line = dark ? Neon : PaperUI.Ink;
        for (int i = 0; i < ticks.Length; i++) ticks[i].color = new Color(line.r, line.g, line.b, i == 0 ? 1f : 0.5f);
        hand.GetComponent<PaperShape>().color = line;
        hub.color = line;

        tagPaper.gameObject.SetActive(!dark);
        tagPaper.color = urgent ? PaperUI.Red : PaperUI.Cream;
        digits.color = dark ? NeonText : urgent ? PaperUI.Light : PaperUI.Ink;
        // neon digits glow (TextMeshPro underlay used as a soft glow)
        Material m = digits.fontMaterial;
        if (dark)
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", PaperUI.Hex("5dff6e", 0.9f));
            m.SetFloat("_UnderlayDilate", 0.4f);
            m.SetFloat("_UnderlaySoftness", 0.9f);
            m.SetFloat("_UnderlayOffsetX", 0f);
            m.SetFloat("_UnderlayOffsetY", 0f);
        }
        else m.DisableKeyword("UNDERLAY_ON");
        digits.UpdateMeshPadding();
    }
}
