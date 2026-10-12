using UnityEngine;

// One of these in the scene. Announces who was caught first.
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Tooltip("Drag Player1 and Player2 here so the winner can be named.")]
    public Hider[] hiders;

    [Header("After the first catch")]
    [Tooltip("Off = the game keeps running (the other player can still be caught). On = freeze everything after the delay.")]
    public bool freezeGame = false;
    public float freezeDelay = 3f;

    private string message = "";
    private bool ended = false;

    void Awake()
    {
        Instance = this;
    }

    public void ReportCaught(Hider caught)
    {
        if (ended) return;
        ended = true;

        string winner = "";
        foreach (Hider h in hiders)
            if (h != null && h != caught) winner = h.displayName;

        message = caught.displayName + " was caught first!";
        if (winner != "") message += "\n" + winner + " wins!";

        Debug.Log(message);

        if (freezeGame) Invoke(nameof(FreezeGame), freezeDelay);
    }

    void FreezeGame()
    {
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (message == "") return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 36;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 160), message, style);
    }
}
