using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shows a DialogueData one line at a time in a paper dialogue box at the bottom of the screen:
// speaker name tag, optional portrait, and the text typed out letter by letter.
// Space / Enter / left click: finish the typing, then go to the next line. Lines can also move on by themselves.
// When the conversation ends (or is skipped) its story flag is set and onDone is called.
//   DialogueManager.Instance.Play(data, onDone);
// Keeps the data (DialogueData), the logic (this) and the look (the box it builds) apart.
public class DialogueManager : MonoBehaviour
{
    static DialogueManager instance;
    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Dialogue Manager");
                instance = go.AddComponent<DialogueManager>();
                instance.Build();
            }
            return instance;
        }
    }

    public static bool IsOpen { get { return instance != null && instance.playing; } }

    const float LettersPerSecond = 38f;

    CanvasGroup group;
    RectTransform box;
    TextMeshProUGUI nameText, bodyText, hintText, skipText;
    Image portrait;
    RectTransform nameTag;
    bool playing, skipAll, skippable;
    Coroutine running;
    DialogueData current;
    Action doneCallback;

    // Plays a conversation. autoAdvance > 0: each line also moves on by itself that many seconds after it is typed out.
    // skippable: shows "Hold Space to skip" (the caller does the skipping, see ClownIntroCutscene)
    public void Play(DialogueData data, Action onDone, float autoAdvance = 0f, bool skippable = false)
    {
        this.skippable = skippable;
        if (running != null) { StopCoroutine(running); Finish(); }
        if (data == null || data.lines == null || data.lines.Length == 0) { if (onDone != null) onDone(); return; }
        current = data;
        doneCallback = onDone;
        running = StartCoroutine(Run(data, autoAdvance));
    }

    // Ends the conversation at once (e.g. a skipped cutscene). Its flag is still set.
    public void SkipAll() { skipAll = true; }

    IEnumerator Run(DialogueData data, float autoAdvance)
    {
        playing = true; skipAll = false;
        for (int i = 0; i < data.lines.Length && !skipAll; i++)
        {
            DialogueLine line = data.lines[i];
            nameText.text = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker;
            nameTag.gameObject.SetActive(!string.IsNullOrEmpty(line.speaker));
            portrait.gameObject.SetActive(line.portrait != null);
            portrait.sprite = line.portrait;
            bodyText.rectTransform.offsetMin = new Vector2(line.portrait != null ? 150f : 34f, bodyText.rectTransform.offsetMin.y);
            bodyText.text = line.text;
            bodyText.maxVisibleCharacters = 0;
            hintText.text = "Space  ›";
            skipText.gameObject.SetActive(skippable);
            yield return null; // the key that opened this line doesn't skip it

            // typewriter (a press shows the whole line at once)
            float shown = 0f;
            int total = line.text != null ? line.text.Length : 0;
            while (bodyText.maxVisibleCharacters < total && !skipAll)
            {
                if (Pressed()) { bodyText.maxVisibleCharacters = total; yield return null; break; }
                shown += Time.unscaledDeltaTime * LettersPerSecond;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }

            // wait for the next press, or move on by itself
            float waited = 0f;
            while (!skipAll)
            {
                if (Pressed()) break;
                waited += Time.unscaledDeltaTime;
                if (autoAdvance > 0f && waited >= autoAdvance) break;
                yield return null;
            }
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        playing = false;
        running = null;
        if (current != null && !string.IsNullOrEmpty(current.flagToSetWhenFinished))
            StoryStateManager.Instance.SetFlag(current.flagToSetWhenFinished);
        current = null;
        Action cb = doneCallback; doneCallback = null;
        if (cb != null) cb();
    }

    static bool Pressed()
    {
        if (PauseMenu.IsOpen) return false;
        Keyboard k = Keyboard.current; Mouse m = Mouse.current;
        return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) ||
               (m != null && m.leftButton.wasPressedThisFrame);
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, playing ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float y = playing ? 40f : 20f;
        box.anchoredPosition = new Vector2(0f, Mathf.Lerp(box.anchoredPosition.y, y, Time.unscaledDeltaTime * 10f));
    }

    // ---------------- the box: cream torn paper, dark name tag, charcoal text ----------------
    void Build()
    {
        DontDestroyOnLoad(gameObject);
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // above the HUD and prompts, under the pause menu and the shutter
        PaperUI.Scaler(gameObject.AddComponent<CanvasScaler>());
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

        box = PaperUI.Rect("Dialogue Box", transform);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.sizeDelta = new Vector2(860f, 150f);
        box.anchoredPosition = new Vector2(0f, 20f);
        box.localEulerAngles = new Vector3(0f, 0f, -0.6f);

        PaperShape paper = PaperUI.Cut(PaperUI.Torn(box, "Paper", PaperUI.Hex("fbf4e4"), PaperUI.CardTear));
        paper.edgeColor = PaperUI.Hex("e2d3b4"); paper.Refresh();
        PaperUI.Stretch(paper.rectTransform);

        portrait = PaperUI.Img(box, "Portrait", null);
        RectTransform pr = portrait.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f); pr.pivot = new Vector2(0f, 0.5f);
        pr.sizeDelta = new Vector2(104f, 104f); pr.anchoredPosition = new Vector2(30f, -4f);
        portrait.preserveAspect = true;
        portrait.gameObject.SetActive(false);

        // name tag: a dark scrap stuck on the top-left edge
        PaperShape tag = PaperUI.Cut(PaperUI.Torn(box, "Name Tag", PaperUI.CardDark, PaperUI.TagTear));
        nameTag = tag.rectTransform;
        nameTag.anchorMin = nameTag.anchorMax = new Vector2(0f, 1f); nameTag.pivot = new Vector2(0f, 0.5f);
        nameTag.sizeDelta = new Vector2(210f, 42f); nameTag.anchoredPosition = new Vector2(26f, 2f);
        nameTag.localEulerAngles = new Vector3(0f, 0f, 2f);
        nameText = PaperUI.Text(nameTag, "Name", "", 22f, PaperUI.Light, true, TextAlignmentOptions.Center);
        PaperUI.Stretch(nameText.rectTransform);

        bodyText = PaperUI.Text(box, "Text", "", 24f, PaperUI.Hex("2b2427"), false, TextAlignmentOptions.TopLeft);
        RectTransform br = bodyText.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(34f, 26f); br.offsetMax = new Vector2(-34f, -34f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        hintText = PaperUI.Text(box, "Hint", "Space  ›", 15f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.BottomRight);
        RectTransform hr = hintText.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f);
        hr.sizeDelta = new Vector2(160f, 24f); hr.anchoredPosition = new Vector2(-26f, 12f);

        // bottom-left: how to skip a cutscene's conversation
        skipText = PaperUI.Text(box, "Skip", "Hold Space to skip", 15f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.BottomLeft);
        RectTransform sr = skipText.rectTransform;
        sr.anchorMin = sr.anchorMax = Vector2.zero; sr.pivot = Vector2.zero;
        sr.sizeDelta = new Vector2(220f, 24f); sr.anchoredPosition = new Vector2(34f, 12f);
        skipText.gameObject.SetActive(false);
    }
}
