using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

// Fast Play mode: Unity is set to NOT reload all scripts every time you press Play (saves ~20 s).
// The catch is that 'static' values would survive from your previous play session, so this puts every
// static value the game relies on back to its starting state before the scene loads, exactly as a full
// reload would. If you add a new 'static' field that holds game state, add it to the list below.
static class StaticStateReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAll()
    {
        // type name, field name, value to put back (Clear = empty the list/dictionary)
        Set("RoundManager", "<Instance>k__BackingField", null);
        Set("RoundManager", "currentRound", 1);
        Clear("RoundManager", "scores");
        Set("HidePrompt", "instance", null);
        Set("HidePrompt", "rounded", null);
        Set("DistractionGate", "MinBreak", 10f);
        Set("DistractionGate", "active", false);
        Set("DistractionGate", "lastEnd", -999f);
        Clear("DistractionOutlineSystem", "systems");
        Set("DoorSwing", "movers", null);
        Set("DoorSwing", "moversFoundAt", -99f);
        Set("PlayerMarker", "spots", null);
        Set("PlayerMarker", "human", null);
        Set("IceSlow", "frostTex", null);
        Clear("LockableDoorway", "all");
        Clear("EnvironmentalHazard", "all");
        Set("HazardAlertUI", "instance", null);
        // Cartoon FX / stylised water packages
        Clear("CartoonFX.CFXR_Effect+CameraShake", "s_CameraShakes");
        Set("CartoonFX.CFXR_Effect", "GlobalStartFrameOffset", 0);
        Set("Bitgem.VFX.StylisedWater.WaterVolumeHelper", "instance", null);
    }

    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static FieldInfo Field(string typeName, string field)
    {
        Type t = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType(typeName, false);
            if (t != null) break;
        }
        return t != null ? t.GetField(field, Flags) : null;
    }

    static void Set(string typeName, string field, object value)
    {
        FieldInfo f = Field(typeName, field);
        if (f != null && !f.IsInitOnly) f.SetValue(null, value);
    }

    static void Clear(string typeName, string field)
    {
        FieldInfo f = Field(typeName, field);
        if (f == null) return;
        IDictionary d = f.GetValue(null) as IDictionary;
        if (d != null) { d.Clear(); return; }
        IList l = f.GetValue(null) as IList;
        if (l != null) l.Clear();
    }
}
