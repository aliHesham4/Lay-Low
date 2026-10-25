using UnityEngine;

// Shared by the TV and the vacuum: only one distraction can run at a time, and there must be a
// break of at least MinBreak seconds between one ending and the next one starting.
public static class DistractionGate
{
    public static float MinBreak = 10f;

    static bool active;
    static float lastEnd = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        active = false;
        lastEnd = -999f;
    }

    public static bool CanStart()
    {
        return !active && Time.time - lastEnd >= MinBreak;
    }

    public static void Started() { active = true; }

    public static void Ended()
    {
        active = false;
        lastEnd = Time.time;
    }
}
