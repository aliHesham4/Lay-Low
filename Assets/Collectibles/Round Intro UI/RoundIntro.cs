using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The round-intro overlay: a cozy roller blind that a chubby hand pulls shut, then the paper-craft
// "ROUND N" card with each round's own beat (stamp + doorbell, caution tapes + swinging 2,
// bulb + flicker + neon lights-out, tombstone rising).
//
// Flow (driven by RoundManager):
//   CloseShutter(onClosed)  -> the hand pulls the blind down; onClosed fires once it is shut.
//   ShowRound(round, onDone) -> waits until the blind is shut (or starts shut on a fresh launch),
//                               opens it, plays the round card, then calls onDone (the round begins).
// Everything runs on unscaled time, so it keeps playing while the game is frozen (Time.timeScale = 0).
// All positions are in the 1280x720 design space of the "Lay Low – Round Intro Cards" canvas;
// all timings are the canvas keyframes (percent of a 9 s loop).
public class RoundIntro : MonoBehaviour
{
    public static RoundIntro Instance { get; private set; }

    [Header("Artwork (Assets/Collectibles/Round Intro UI)")]
    public Sprite vignette, titleBase, number1, number2, number3, number4;
    public Sprite shutterSlats, shutterRail, shutterCord, hand, rollerBox;
    public Sprite stamp, stampBell, tape1, tape2;
    public Sprite banner, bulbCord, bulbGlow, bulbGlass, neonRound, neon3, neonBanner;
    public Sprite tombstone;

    [Header("Timing")]
    [Tooltip("The blind stays shut at least this long, even if the next round is ready sooner.")]
    public float minClosedSeconds = 1.0f;
    [Tooltip("Sorting order of the overlay canvas (above every HUD).")]
    public int sortingOrder = 500;

    [Header("Sounds (optional)")]
    public AudioClip shutterSlam, shutterOpen, popSound, stampSound, doorbellSound,
                     tapeSound, creakSound, flickerSound, lightsOutSound, rumbleSound;
    [Range(0f, 1f)] public float volume = 0.9f;

    public bool IsShut { get; private set; }
    public bool IsPlaying { get; private set; }

    const float LOOP = 9f;              // the design's loop length
    const float CLOSED_AT = 0.25f * LOOP; // 25%: the blind is down and holding

    // ---------------- built UI ----------------
    RectTransform root, shake, shutter, handRT, cordRT, card, numberRT, bulbRT, tombRT, tape1RT, tape2RT, stampRT, bellRT, bannerRT;
    CanvasGroup cardGroup, boxGroup, vignetteGroup, neonGroup, bulbGroup, handGroup, stampGroup, bannerGroup, tombGroup;
    Image numberImg, blackout, bulbGlowImg, bulbGlassImg;
    GameObject r1, r2, r3, r4;
    AudioSource audioSrc;
    Coroutine running;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        // a fresh launch starts behind a shut blind; RoundManager opens it once round 1 is ready
        SetClosedPose();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ======================= public API =======================

    public void CloseShutter(Action onClosed)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CloseRoutine(onClosed));
    }

    public void ShowRound(int round, Action onDone)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShowRoutine(Mathf.Clamp(round, 1, 4), onDone));
    }

// Testing: freeze the intro of a round at a moment of the 9 s design loop (e.g. from the editor).
    public void Preview(int round, float seconds)
    {
        if (running != null) { StopCoroutine(running); running = null; }
        SetRound(round);
        Pose(seconds, round);
    }

    // Testing: hide the overlay again after a Preview.
    public void EndPreview()
    {
        HideAll();
        IsShut = false;
        IsPlaying = false;
    }


    // ======================= sequences =======================

    float closedSince;

    IEnumerator CloseRoutine(Action onClosed)
    {
        IsPlaying = true;
        SetRound(0);
        Play(shutterSlam, 0.68f);
        for (float t = 0f; t < CLOSED_AT; t += Time.unscaledDeltaTime)
        {
            Pose(t, 0);
            yield return null;
        }
        SetClosedPose();
        onClosed?.Invoke();
    }

    IEnumerator ShowRoutine(int round, Action onDone)
    {
        IsPlaying = true;
        if (!IsShut) { yield return CloseRoutine(null); }
        while (Time.realtimeSinceStartup - closedSince < minClosedSeconds) yield return null;

        SetRound(round);
        ScheduleSounds(round);
        for (float t = CLOSED_AT; t < LOOP; t += Time.unscaledDeltaTime)
        {
            Pose(t, round);
            yield return null;
        }
        Pose(LOOP, round);
        HideAll();
        IsShut = false;
        IsPlaying = false;
        running = null;
        onDone?.Invoke();
    }

    void SetClosedPose()
    {
        root.gameObject.SetActive(true);
        Pose(CLOSED_AT, 0);
        IsShut = true;
        closedSince = Time.realtimeSinceStartup;
    }

    void HideAll()
    {
        root.gameObject.SetActive(false);
    }

    // ======================= the timeline =======================
    // t = seconds into the 9 s design loop; every track is a list of (percent, value) keyframes.

    void Pose(float t, int round)
    {
        root.gameObject.SetActive(true);
        float p = Mathf.Clamp(t / LOOP * 100f, 0f, 100f);

        // ---- blind, cord, hand, roller box ----
        float sy = Eval(p, EaseInOut, 0, -1188, 2, -686, 3.8f, -686, 9, 0, 25, 0, 27.5f, -700, 28.5f, -680, 30, -686, 33, -1188, 100, -1188);
        shutter.anchoredPosition = new Vector2(0f, -sy);
        cordRT.localEulerAngles = new Vector3(0, 0, -Eval(p, EaseInOut, 0, 9, 1.2f, -7, 2.4f, 4, 3.6f, 0, 27, 0, 28.6f, -12, 29.8f, 9, 31, -5, 32.2f, 0, 100, 0));
        float hy = Eval(p, EaseInOut, 0, 620, 1, 620, 3.8f, 0, 9.5f, 0, 100, 0);
        handRT.anchoredPosition = HandBase + new Vector2(0f, -hy);
        handGroup.alpha = Eval(p, EaseInOut, 0, 1, 9.5f, 1, 10, 0, 100, 0);
        boxGroup.alpha = Eval(p, EaseInOut, 0, 0, 1.5f, 1, 30.5f, 1, 33, 0, 100, 0);

        // ---- the card ----
        float s = Eval(p, Bezier(.2f, 1.5f, .4f, 1f), 0, 0, 31, 0, 37, 1.16f, 41, .95f, 45, 1, 94, 1, 100, .9f);
        card.localScale = new Vector3(s, s, 1f);
        cardGroup.alpha = Eval(p, Bezier(.2f, 1.5f, .4f, 1f), 0, 0, 31, 0, 37, 1, 45, 1, 94, 1, 100, 0);
        vignetteGroup.alpha = round == 0 ? 0f : Eval(p, Linear, 0, 0, 30, 0, 33, 1, 94, 1, 100, 0);

        // ---- shake (y flipped: the design's y runs down) ----
        Vector2 sh = Vector2.zero;
        switch (round)
        {
            case 0: sh = Shake(p, ShakeClose); break;
            case 1: sh = Shake(p, Shake1); break;
            case 2: sh = Shake(p, Shake2); break;
            case 3: sh = Shake(p, Shake3); break;
            case 4: sh = Shake(p, Shake4); break;
        }
        shake.anchoredPosition = new Vector2(sh.x, -sh.y);

        // ---- each round's own beat ----
        if (round == 1)
        {
            float ss = Eval(p, Bezier(.45f, 0f, .55f, 1f), 0, 2.1f, 46, 2.1f, 50, .97f, 53, 1, 100, 1);
            stampRT.localScale = new Vector3(ss, ss, 1f);
            stampGroup.alpha = Eval(p, Bezier(.45f, 0f, .55f, 1f), 0, 0, 46, 0, 50, 1, 53, 1, 100, 1);
            bellRT.localEulerAngles = new Vector3(0, 0, -Eval(p, EaseInOut, 0, 0, 58, 0, 60, 22, 62, -18, 64, 12, 66, 0, 70, 0, 72, -22, 74, 18, 76, -12, 78, 0, 100, 0));
        }
        else if (round == 2)
        {
            var tb = Bezier(.6f, 0f, .4f, 1f);
            tape1RT.anchoredPosition = Tape1Base + new Vector2(Eval(p, tb, 0, -1600, 46, -1600, 49, 22, 51, 0, 100, 0), 0f);
            tape2RT.anchoredPosition = Tape2Base + new Vector2(Eval(p, tb, 0, 1600, 53, 1600, 56, -22, 58, 0, 100, 0), 0f);
            float rot = Eval(p, EaseInOut, 0, -6, 60, -6, 63, 30, 66, -27, 69, 25, 72, -23, 75, 21, 78, -19, 81, 17, 84, -15, 87, 13, 90, -11, 93, 9, 96.5f, -6, 100, -6);
            numberRT.localEulerAngles = new Vector3(0, 0, -(rot + 6f)); // the artwork already leans -6°
        }
        else if (round == 3)
        {
            float by = Eval(p, Bezier(.3f, 1.5f, .5f, 1f), 0, -420, 38, -420, 42, 0, 100, 0);
            float br = Eval(p, Bezier(.3f, 1.5f, .5f, 1f), 0, 0, 38, 0, 42, 10, 44.5f, -8, 47, 5, 49.5f, -2, 52, 0, 100, 0);
            bulbRT.anchoredPosition = BulbBase + new Vector2(0f, -by);
            bulbRT.localEulerAngles = new Vector3(0, 0, -br);
            bulbGroup.alpha = Eval(p, Linear, 0, 1, 94, 1, 100, 0);
            float bs = Eval(p, Bezier(.5f, 0f, .75f, 0f), 0, 2.6f, 46, 2.6f, 50, .94f, 53, 1, 100, 1);
            bannerRT.localScale = new Vector3(bs, bs, 1f);
            bannerGroup.alpha = Eval(p, Bezier(.5f, 0f, .75f, 0f), 0, 0, 46, 0, 50, 1, 53, 1, 100, 1);
            float bright = Eval(p, Linear, 0, 1, 56, 1, 56.6f, .25f, 57.2f, 1, 58, .4f, 58.6f, 1, 61, 1, 61.6f, .15f, 62.2f, 1, 63, .5f, 63.5f, 1, 68.9f, 1, 69, 0, 100, 0); // stays dark to the end: the lit card never comes back
            blackout.color = new Color(0f, 0f, 0f, 1f - bright);
            bulbGlowImg.color = new Color(1f, 1f, 1f, Eval(p, Linear, 0, 1, 56, 1, 56.6f, .1f, 57.2f, 1, 58, .2f, 58.6f, 1, 61, 1, 61.6f, .05f, 62.2f, 1, 63, .3f, 63.5f, 1, 68.9f, 1, 69, 0, 100, 0));
            float dark = Eval(p, Linear, 0, 0, 56, 0, 56.6f, .8f, 57.2f, 0, 58, .6f, 58.6f, 0, 61, 0, 61.6f, .85f, 62.2f, 0, 63, .5f, 63.5f, 0, 68.9f, 0, 69, 1, 100, 1);
            bulbGlassImg.color = Color.Lerp(Color.white, Color.black, dark);
            neonGroup.alpha = p >= 69f ? Eval(p, Linear, 0, 1, 94, 1, 100, 0) : 0f; // snaps on with the lights out, then fades out with the card
        }
        else if (round == 4)
        {
            float ty = Eval(p, Linear, 0, 270, 47, 270, 51, 215, 55, 160, 59, 105, 63, 50, 67, -6, 69, 0, 100, 0);
            float tr = Eval(p, Linear, 0, 0, 47, 0, 51, -1.4f, 55, 1.1f, 59, -.9f, 63, .7f, 67, -.3f, 69, 0, 100, 0);
            tombRT.anchoredPosition = TombBase + new Vector2(0f, -ty);
            tombRT.localEulerAngles = new Vector3(0, 0, -tr);
            tombGroup.alpha = Eval(p, Linear, 0, 1, 94, 1, 100, 0);
        }
    }

    // ---------------- shake tables: (percent, x, y) ----------------
    static readonly float[] ShakeClose = { 0,0,0, 7.8f,0,0, 8.4f,0,9, 9.2f,0,-5, 10.5f,0,0, 100,0,0 };
    static readonly float[] Shake1 = { 0,0,0, 7.8f,0,0, 8.4f,0,9, 9.2f,0,-5, 10.5f,0,0, 49.6f,0,0, 50.2f,-6,4, 51,5,-4, 51.8f,-3,-2, 52.6f,2,2, 53.8f,-1,1, 55,0,0, 100,0,0 };
    static readonly float[] Shake2 = { 0,0,0, 7.8f,0,0, 8.4f,0,9, 9.2f,0,-5, 10.5f,0,0, 48.6f,0,0, 49.2f,-13,7, 50,10,-6, 50.8f,-5,3, 51.6f,0,0, 55.6f,0,0, 56.2f,13,-7, 57,-10,6, 57.8f,5,-3, 58.6f,0,0, 100,0,0 };
    static readonly float[] Shake3 = { 0,0,0, 7.8f,0,0, 8.4f,0,9, 9.2f,0,-5, 10.5f,0,0, 49.6f,0,0, 50.2f,-9,5, 51,7,-5, 51.8f,-3,2, 53,0,0, 100,0,0 };
    static readonly float[] Shake4 = { 0,0,0, 7.8f,0,0, 8.4f,0,9, 9.2f,0,-5, 10.5f,0,0, 47,0,0, 49,-2,1, 51,2,-2, 53,-3,2, 55,3,-1, 57,-3,2, 59,2,-2, 61,-3,1, 63,2,-1, 65,-2,1, 67.5f,0,6, 68.5f,0,-2, 69.5f,0,0, 100,0,0 };

    static Vector2 Shake(float p, float[] k)
    {
        for (int i = 0; i + 3 < k.Length; i += 3)
        {
            if (p <= k[i + 3])
            {
                float u = Mathf.InverseLerp(k[i], k[i + 3], p);
                return new Vector2(Mathf.Lerp(k[i + 1], k[i + 4], u), Mathf.Lerp(k[i + 2], k[i + 5], u));
            }
        }
        return Vector2.zero;
    }

    // ---------------- keyframe evaluation (CSS-style: easing per segment) ----------------
    static float Eval(float p, Func<float, float> ease, params float[] k)
    {
        if (p <= k[0]) return k[1];
        for (int i = 0; i + 3 < k.Length; i += 2)
        {
            if (p <= k[i + 2])
            {
                float span = k[i + 2] - k[i];
                float u = span <= 0f ? 1f : (p - k[i]) / span;
                return Mathf.LerpUnclamped(k[i + 1], k[i + 3], ease(u));
            }
        }
        return k[k.Length - 1];
    }

    static float Linear(float u) { return u; }
    static readonly Func<float, float> EaseInOut = Bezier(.42f, 0f, .58f, 1f);

    // CSS cubic-bezier(x1, y1, x2, y2)
    static Func<float, float> Bezier(float x1, float y1, float x2, float y2)
    {
        return u =>
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            float s = u;
            for (int i = 0; i < 8; i++)
            {
                float x = B(s, x1, x2) - u;
                float d = dB(s, x1, x2);
                if (Mathf.Abs(x) < 1e-5f || Mathf.Abs(d) < 1e-6f) break;
                s -= x / d;
            }
            s = Mathf.Clamp01(s);
            return B(s, y1, y2);
        };
    }
    static float B(float s, float a, float b) { float o = 1 - s; return 3 * o * o * s * a + 3 * o * s * s * b + s * s * s; }
    static float dB(float s, float a, float b) { float o = 1 - s; return 3 * o * o * a + 6 * o * s * (b - a) + 3 * s * s * (1 - b); }

    // ======================= building the UI =======================
    // Design coordinates: x right, y DOWN, origin at the top-left of the 1280x720 frame.

    Vector2 HandBase, Tape1Base, Tape2Base, BulbBase, TombBase;

    void Build()
    {
        var canvasGO = new GameObject("Round Intro Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<CoverScaler>();

        root = Frame("Round Intro", canvasGO.transform);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.pivot = new Vector2(.5f, .5f);
        root.anchoredPosition = Vector2.zero;
        canvasGO.GetComponent<CoverScaler>().target = root;

        shake = Frame("Shake", root);

        vignetteGroup = Group(Img("Vignette", shake, vignette, 0, 0, 1328, 768).gameObject);

        // ---- the card (pops from 50% 45%) ----
        card = Frame("Card", shake, 640, 324);
        cardGroup = Group(card.gameObject);
        Img("Title", card, titleBase, 21, 67.9f, 1307, 506.1f);
        numberImg = Img("Number", card, number1, 427, 240, 427, 334, 640.5f, 378.7f);
        numberRT = numberImg.rectTransform;

        // round 1: the stamp (designed 290 px, shown at 250 px, centred at 877,509, tilted -12°)
        r1 = new GameObject("Round 1 Stamp", typeof(RectTransform));
        r1.transform.SetParent(card, false);
        Full((RectTransform)r1.transform);
        stampRT = Frame("Stamp", r1.transform, 877, 509);
        stampGroup = Group(stampRT.gameObject);
        var tilt = Frame("Tilt", stampRT, 877, 509);
        tilt.localEulerAngles = new Vector3(0, 0, 12f);
        const float k = 250f / 290f; // design scale of the stamp
        // stamp art box: (638,308) 386x386 around the 290 px stamp at (686,356) -> rescale around its centre (831,501) to (877,509)
        Img("Stamp Art", tilt, stamp, 877 - 193 * k, 509 - 193 * k, 386 * k, 386 * k);
        var bellBox = new Rect(877 + (742.8f - 831f) * k, 509 + (393.9f - 501f) * k, 176.3f * k, 199.7f * k);
        float bellPivotX = 877 + (686 + 145f - 831f) * k, bellPivotY = 509 + (356 + 91.5f - 501f) * k;
        bellRT = Img("Bell", tilt, stampBell, bellBox.x, bellBox.y, bellBox.width, bellBox.height, bellPivotX, bellPivotY).rectTransform;

        // round 2: the caution tapes (rest tilt -3° / -2°)
        r2 = new GameObject("Round 2 Tapes", typeof(RectTransform));
        r2.transform.SetParent(card, false);
        Full((RectTransform)r2.transform);
        tape1RT = Img("Tape 1", r2.transform, tape1, 0, 0, 1408, 162, 640, 75).rectTransform;
        tape1RT.localEulerAngles = new Vector3(0, 0, 3f);
        tape2RT = Img("Tape 2", r2.transform, tape2, 0, 568, 1408, 174, 640, 655).rectTransform;
        tape2RT.localEulerAngles = new Vector3(0, 0, 2f);
        Tape1Base = tape1RT.anchoredPosition; Tape2Base = tape2RT.anchoredPosition;

        // round 3: the banner (inside the card), then blackout, neon and the bulb on top
        r3 = new GameObject("Round 3 Lights Out", typeof(RectTransform));
        r3.transform.SetParent(shake, false);
        Full((RectTransform)r3.transform);
        var r3banner = new GameObject("Round 3 Banner", typeof(RectTransform));
        r3banner.transform.SetParent(card, false);
        Full((RectTransform)r3banner.transform);
        bannerRT = Img("Banner", r3banner.transform, banner, 292, 512, 696, 188, 640, 606).rectTransform;
        bannerRT.localEulerAngles = new Vector3(0, 0, 3f);
        bannerGroup = Group(bannerRT.gameObject);
        blackout = Img("Blackout", r3.transform, null, -400, -400, 2080, 1520);
        blackout.color = new Color(0, 0, 0, 0);
        var neon = Frame("Neon", r3.transform);
        neonGroup = Group(neon.gameObject);
        Img("Neon ROUND", neon, neonRound, 21, 94, 1307, 480);
        Img("Neon 3", neon, neon3, 427, 240, 427, 334);
        var nb = Img("Neon Banner", neon, neonBanner, 292, 512, 696, 188, 640, 606).rectTransform;
        nb.localEulerAngles = new Vector3(0, 0, 3f);
        bulbRT = Frame("Bulb", r3.transform, 1020, 0);
        bulbGroup = Group(bulbRT.gameObject);
        BulbBase = bulbRT.anchoredPosition;
        bulbGlowImg = Img("Glow", bulbRT, bulbGlow, 852, 102, 336, 336);
        Img("Cord", bulbRT, bulbCord, 950, 0, 140, 274);
        bulbGlassImg = Img("Glass", bulbRT, bulbGlass, 934, 172, 172, 182);
        r3banner.transform.SetAsLastSibling();
        // keep the banner with the round-3 objects when switching rounds
        r3bannerGO = r3banner;

        // round 4: the tombstone rises out of the ground (pivot: bottom centre of the stone)
        r4 = new GameObject("Round 4 Tombstone", typeof(RectTransform));
        r4.transform.SetParent(shake, false);
        Full((RectTransform)r4.transform);
        tombRT = Img("Tombstone", r4.transform, tombstone, 292, 508, 696, 296, 640, 756).rectTransform;
        tombGroup = Group(tombRT.gameObject);
        TombBase = tombRT.anchoredPosition;

        // ---- the blind (on top of everything) ----
        shutter = Frame("Blind", shake);
        Img("Slats", shutter, shutterSlats, 0, 0, 1328, 738);
        Img("Rail", shutter, shutterRail, 0, 628.9f, 1338.2f, 148.1f);
        cordRT = Img("Cord", shutter, shutterCord, 562, 668, 156, 526, 640, 716).rectTransform;
        var handImg = Img("Hand", shutter, hand, 472, 1072, 336, 536);
        handRT = handImg.rectTransform;
        handGroup = Group(handImg.gameObject);
        HandBase = handRT.anchoredPosition;
        boxGroup = Group(Img("Roller Box", shake, rollerBox, 0, 0, 1328, 78).gameObject);

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false; audioSrc.spatialBlend = 0f; audioSrc.ignoreListenerPause = true;
    }

    GameObject r3bannerGO;

    void SetRound(int round)
    {
        numberImg.sprite = round == 2 ? number2 : round == 3 ? number3 : round == 4 ? number4 : number1;
        numberRT.localEulerAngles = Vector3.zero;
        r1.SetActive(round == 1);
        r2.SetActive(round == 2);
        r3.SetActive(round == 3); r3bannerGO.SetActive(round == 3);
        r4.SetActive(round == 4);
        if (round != 3) blackout.color = new Color(0, 0, 0, 0);
    }

    // a full 1280x720 design frame whose pivot sits at design point (px, py)
    RectTransform Frame(string name, Transform parent, float px = 0f, float py = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(1280f, 720f);
        rt.pivot = new Vector2(px / 1280f, 1f - py / 720f);
        rt.anchoredPosition = new Vector2(px, -py);
        return rt;
    }

    static void Full(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(1280f, 720f);
        rt.anchoredPosition = Vector2.zero;
    }

    // an image covering design box (x, y, w, h), rotating/scaling around design point (px, py)
    Image Img(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, float px = float.NaN, float py = float.NaN)
    {
        if (float.IsNaN(px)) { px = x + w * .5f; py = y + h * .5f; }
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.pivot = new Vector2((px - x) / w, 1f - (py - y) / h);
        rt.anchoredPosition = new Vector2(px, -py);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite == null) img.color = Color.black;
        return img;
    }

    static CanvasGroup Group(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false; g.interactable = false;
        return g;
    }

    // ---------------- sounds, cued to the same beats ----------------
    void Play(AudioClip clip, float delay)
    {
        if (clip != null) StartCoroutine(PlayLater(clip, delay));
    }

    IEnumerator PlayLater(AudioClip clip, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        audioSrc.PlayOneShot(clip, volume);
    }

    void ScheduleSounds(int round)
    {
        float S(float pct) { return pct / 100f * LOOP - CLOSED_AT; } // seconds after the blind starts opening
        Play(shutterOpen, 0f);
        Play(popSound, S(32f));
        if (round == 1) { Play(stampSound, S(49.5f)); Play(doorbellSound, S(58f)); }
        if (round == 2) { Play(tapeSound, S(48.5f)); Play(tapeSound, S(55.5f)); Play(creakSound, S(60f)); }
        if (round == 3) { Play(flickerSound, S(56f)); Play(lightsOutSound, S(69f)); }
        if (round == 4) { Play(rumbleSound, S(47f)); }
    }
}

// Scales the 1280x720 design frame so it always covers the whole screen (like CSS object-fit: cover).
public class CoverScaler : MonoBehaviour
{
    public RectTransform target;
    void LateUpdate()
    {
        if (target == null) return;
        float s = Mathf.Max(Screen.width / 1280f, Screen.height / 720f);
        target.sizeDelta = new Vector2(1280f, 720f);
        target.localScale = new Vector3(s, s, 1f);
    }
}
