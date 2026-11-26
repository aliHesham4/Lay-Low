using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

// Click sounds for every menu button (main menu, setup, characters, pause menu, settings).
// Two Kenney interface sounds (CC0), kept in Assets/Resources/UI Sounds:
//   Click        - any button, toggle or tab
//   Confirm      - PLAY and "Start the night"
// It starts by itself in every scene; nothing needs to be placed.
public class UISounds : MonoBehaviour
{
    static UISounds instance;
    AudioClip click, confirm;
    AudioSource source;
    readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("UI Sounds");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UISounds>();
    }

    void Awake()
    {
        click = Resources.Load<AudioClip>("UI Sounds/Click");
        confirm = Resources.Load<AudioClip>("UI Sounds/Confirm");
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;   // still heard in the pause menu
    }

    void Update()
    {
        Mouse m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame || EventSystem.current == null) return;

        // what UI element is under the mouse
        PointerEventData p = new PointerEventData(EventSystem.current) { position = m.position.ReadValue() };
        hits.Clear();
        EventSystem.current.RaycastAll(p, hits);
        if (hits.Count == 0) return;

        Selectable sel = hits[0].gameObject.GetComponentInParent<Selectable>();
        if (sel == null || !sel.IsInteractable()) return;

        source.PlayOneShot(Pick(sel.transform), 0.8f);
    }

    AudioClip Pick(Transform t)
    {
        string n = t.name;
        if (n == "Play" || n.Contains("Start the night")) return confirm != null ? confirm : click;
        return click;
    }
}
