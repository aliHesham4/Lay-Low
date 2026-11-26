using UnityEngine;

// What the main menu hands to the game: how many hiders, each seat's name and character, and the character
// you play as. Seats are always filled from Player 1 up: making a seat empty moves everyone after it up one
// (empty Player 2, and Player 3 becomes Player 2, Player 4 becomes Player 3). Saved in PlayerPrefs.
public static class MatchSetup
{
    public const int Seats = 4;
    public enum Seat { You = 0, AI = 1, Empty = 2 }

    // the roster, in the order the Characters page shows it
    public static readonly string[] CharacterNames = { "Pyjama Man", "Chubby Man", "Marly The Bride", "Grandpa Will" };
    // the character each player object wears in the game scene (Player1..Player4)
    public static readonly int[] SceneCharacter = { 0, 2, 1, 3 };

    const string Key = "LayLow.";

    // ---------------- hiders ----------------
    public static int Hiders
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(Key + "Hiders", Seats), 2, Seats); }
        set { PlayerPrefs.SetInt(Key + "Hiders", Mathf.Clamp(value, 2, Seats)); }
    }

    public static Seat GetSeat(int seat)
    {
        if (seat == 0) return Seat.You;
        return seat < Hiders ? Seat.AI : Seat.Empty;
    }

    // Empty on a seat: that player leaves and everyone after them moves up one seat
    public static void RemoveSeat(int seat)
    {
        if (seat <= 0 || seat >= Hiders || Hiders <= 2) return;
        string[] names = new string[Seats]; int[] chars = new int[Seats];
        for (int i = 0; i < Seats; i++) { names[i] = CustomName(i); chars[i] = CharacterInSeat(i); }
        string leftName = names[seat]; int leftChar = chars[seat];
        for (int i = seat; i < Seats - 1; i++) { names[i] = names[i + 1]; chars[i] = chars[i + 1]; }
        names[Seats - 1] = leftName; chars[Seats - 1] = leftChar;
        for (int i = 0; i < Seats; i++) { PlayerPrefs.SetString(Key + "Name" + i, names[i]); PlayerPrefs.SetInt(Key + "SeatChar" + i, chars[i]); }
        Hiders = Hiders - 1;
    }

    // AI on an empty seat: the next empty seat fills up
    public static void AddSeat() { Hiders = Hiders + 1; }

    // ---------------- names ----------------
    public static string DefaultName(int seat) { return "Player " + (seat + 1); }

    // what the player typed for this seat ("" = not changed, so the seat's own "Player N" is used)
    static string CustomName(int seat)
    {
        string s = (PlayerPrefs.GetString(Key + "Name" + seat, "") ?? "").Trim();
        for (int i = 0; i < Seats; i++) if (s == DefaultName(i)) return "";
        return s;
    }

    public static string GetName(int seat)
    {
        string s = CustomName(seat);
        return s.Length > 0 ? s : DefaultName(seat);
    }

    public static void SetName(int seat, string name)
    {
        PlayerPrefs.SetString(Key + "Name" + seat, (name ?? "").Trim());
    }

    // the names actually used in the game: blank names fall back to "Player N", repeats get a number
    public static string[] FinalNames()
    {
        string[] n = new string[Seats];
        for (int i = 0; i < Seats; i++) n[i] = GetName(i);
        for (int i = 0; i < Seats; i++)
            for (int j = 0; j < i; j++)
                if (string.Equals(n[i], n[j], System.StringComparison.OrdinalIgnoreCase)) { n[i] = n[i] + " " + (i + 1); break; }
        return n;
    }

    // ---------------- characters ----------------
    // which character sits in each seat (always a full set: every character exactly once)
    public static int CharacterInSeat(int seat)
    {
        int[] all = SeatCharacters();
        return all[Mathf.Clamp(seat, 0, Seats - 1)];
    }

    public static int[] SeatCharacters()
    {
        int[] c = new int[Seats]; bool[] used = new bool[CharacterNames.Length]; bool ok = true;
        for (int i = 0; i < Seats; i++)
        {
            c[i] = PlayerPrefs.GetInt(Key + "SeatChar" + i, SceneCharacter[i]);
            if (c[i] < 0 || c[i] >= CharacterNames.Length || used[c[i]]) { ok = false; break; }
            used[c[i]] = true;
        }
        if (!ok) for (int i = 0; i < Seats; i++) c[i] = SceneCharacter[i];
        return c;
    }

    // the character you play as (seat 1). Picking another one swaps it with the seat that had it.
    public static int Character
    {
        get { return CharacterInSeat(0); }
        set
        {
            int[] c = SeatCharacters();
            int to = Mathf.Clamp(value, 0, CharacterNames.Length - 1);
            for (int i = 1; i < Seats; i++) if (c[i] == to) { c[i] = c[0]; break; }
            c[0] = to;
            for (int i = 0; i < Seats; i++) PlayerPrefs.SetInt(Key + "SeatChar" + i, c[i]);
        }
    }

    public static void Save() { PlayerPrefs.Save(); }
}
