using UnityEngine;
using UnityEngine.SceneManagement;

// One per night scene (Night 1 - Night 4). Each night is its own scene, but they all share the same house:
// this sets which night is being played, then loads the House scene underneath it (additively) and makes the
// house the active scene, so its lighting, skybox and fog are the ones in use.
// The night scene is where that night's own story pieces live (its dialogue, notes, objective, lighting changes).
// The shutter stays shut the whole time; the house's RoundManager opens it once everything is ready.
public class NightScene : MonoBehaviour
{
    [Range(1, 4)] public int night = 1;
    [Tooltip("The shared house scene, loaded underneath this night.")]
    public string houseScene = "House";

    [Header("Shutter card (story recap + objective)")]
    [Tooltip("Recap line shown on the shutter card when you survived the night before (or always, on night 1).")]
    [TextArea(1, 3)] public string recapSurvived = "";
    [Tooltip("Recap line when you were caught the night before. Leave empty to use the line above.")]
    [TextArea(1, 3)] public string recapCaught = "";
    [Tooltip("This night's objective, shown under the recap.")]
    [TextArea(1, 2)] public string objective = "";

    [Header("Objectives (pause menu)")]
    [Tooltip("This night's objectives, each shown/ticked off by story flags. Leave empty to use the built-in list (ObjectiveManager.Defaults).")]
    public Objective[] objectives;

    // The built-in story for each night, used when the night scene's own fields are empty
    // (or when the house is played on its own). [night-1] = { survived, caught, objective }
    static readonly string[][] DefaultStory = {
        new[] { "The family moved into a cheap old house. Nobody told them the clown who lived here never moved out.", "",
                "Survive the first night." },
        new[] { "You made it through the first night. Now the house itself is turning on you.",
                "He found you last night. Tonight the house is helping him.",
                "Survive the hazards until morning." },
        new[] { "Two nights down. Tonight he cuts the power.",
                "He has found you before. Now he cuts the power.",
                "Survive the blackout. Stay away from the flickering lights." },
        new[] { "The last night. He drops a golden knife among you, and trust breaks.",
                "The last night. He drops a golden knife among you, and trust breaks.",
                "Only one of you leaves this house alive. Be that one." } };

    // The recap and objective for a night, picked from how the night before went for the player.
    public static void StoryFor(int night, out string recap, out string goal)
    {
        night = Mathf.Clamp(night, 1, 4);
        string[] d = DefaultStory[night - 1];
        NightScene n = Current != null && Current.night == night ? Current : null;
        bool caughtBefore = night > 1 && StoryStateManager.Instance.HasFlag(StoryStateManager.Caught(night - 1));

        string survived = n != null && !string.IsNullOrEmpty(n.recapSurvived) ? n.recapSurvived : d[0];
        string caught = n != null && !string.IsNullOrEmpty(n.recapCaught) ? n.recapCaught : (string.IsNullOrEmpty(d[1]) ? survived : d[1]);
        recap = caughtBefore ? caught : survived;
        goal = n != null && !string.IsNullOrEmpty(n.objective) ? n.objective : d[2];
    }

    public static string SceneName(int night) { return "Night " + night; }

    // the night scene that is currently playing (null if the house was opened on its own)
    public static NightScene Current { get; private set; }

    void Awake()
    {
        Current = this;
        // a night only starts if the story has reached it; otherwise go to the night the story is on
        StoryStateManager story = StoryStateManager.Instance;
        // (opening a night scene straight from the editor, without the main menu, is allowed for testing)
        if (story.HasFlag("movedIn") && !story.CanStartNight(night))
        {
            Debug.Log("[Story] night " + night + " isn't unlocked yet, going to night " + story.FurthestNight());
            SceneManager.LoadScene(SceneName(story.FurthestNight()));
            return;
        }
        story.SetFlag(StoryStateManager.Started(night));
        RoundManager.BeginNight(night);
        SceneManager.sceneLoaded += OnLoaded;
        if (!SceneManager.GetSceneByName(houseScene).isLoaded)
            SceneManager.LoadSceneAsync(houseScene, LoadSceneMode.Additive);
    }

    // runs before the house's Start(), so every round script already sees the house as the active scene
    void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name != houseScene) return;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.SetActiveScene(s);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        if (Current == this) Current = null;
    }
}
