using UnityEngine;

// One line of a conversation: who says it, their face (optional) and the text.
[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public Sprite portrait;
    [TextArea(2, 5)] public string text;
}

// One conversation, stored as an asset so the story can be edited without touching scripts.
// Create via: Assets > Create > Dialogue > Dialogue Data (one asset per conversation, in Assets/Story/Dialogue).
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
    [Tooltip("Story flag set when the conversation finishes (or is skipped), e.g. \"metClown\".")]
    public string flagToSetWhenFinished;
}
