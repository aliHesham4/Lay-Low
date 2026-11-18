using UnityEngine;
using Unity.Cinemachine;

// Keeps the third-person camera's target group in sync with the players: everyone still in the round
// is framed; caught players drop out (if everyone is caught, all of them are framed again).
[RequireComponent(typeof(CinemachineTargetGroup))]
public class PlayersCameraGroup : MonoBehaviour
{
    [Tooltip("How much room each player takes in the shot (metres).")]
    public float playerRadius = 1.2f;
    [Tooltip("Height of the point the camera frames on each player.")]
    public float aimHeight = 0.9f;

    CinemachineTargetGroup group;
    Hider[] players;
    Transform[] aims;

    void Awake()
    {
        group = GetComponent<CinemachineTargetGroup>();
        players = FindObjectsByType<Hider>(FindObjectsSortMode.InstanceID);
        aims = new Transform[players.Length];
        group.Targets.Clear();
        for (int i = 0; i < players.Length; i++)
        {
            // a point at chest height that follows the player, so the shot isn't aimed at their feet
            GameObject a = new GameObject("CameraGroupPoint " + players[i].name);
            a.transform.SetParent(players[i].transform, false);
            a.transform.localPosition = Vector3.up * aimHeight;
            aims[i] = a.transform;
            group.AddMember(a.transform, 1f, playerRadius);
        }
    }

    void LateUpdate()
    {
        if (players == null) return;
        bool anyFree = false;
        foreach (Hider h in players) if (h != null && !h.IsCaught) { anyFree = true; break; }

        for (int i = 0; i < players.Length && i < group.Targets.Count; i++)
        {
            Hider h = players[i];
            float w = (h != null && (!h.IsCaught || !anyFree)) ? 1f : 0f;
            CinemachineTargetGroup.Target t = group.Targets[i];
            if (Mathf.Abs(t.Weight - w) > 0.001f || Mathf.Abs(t.Radius - playerRadius) > 0.001f)
            {
                t.Weight = Mathf.MoveTowards(t.Weight, w, Time.deltaTime * 2f); // ease in/out instead of a jump
                t.Radius = playerRadius;
                group.Targets[i] = t;
            }
        }
    }
}
