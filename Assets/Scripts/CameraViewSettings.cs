using Unity.Cinemachine;
using UnityEngine;

// The third-person camera view the player picks in Settings: Classic, Higher (default) or Over the shoulder.
// The choice is saved (PlayerPrefs) and kept between rounds and matches.
// The pause menu's Settings calls CameraViewSettings.Instance.SetView(index).
[RequireComponent(typeof(CinemachineCamera))]
public class CameraViewSettings : MonoBehaviour
{
    [System.Serializable]
    public class View
    {
        public string name;
        [Tooltip("How far behind the player the camera sits (metres).")]
        public float distance = 4f;
        [Tooltip("How high above the player the camera's shoulder point is (metres).")]
        public float height = 1.97f;
        [Tooltip("Extra rise of the camera arm (metres).")]
        public float armLength = 1.45f;
        [Tooltip("Field of view (degrees).")]
        public float fov = 60f;
        [Tooltip("The camera looks this far in front of the player (metres), so the player sits lower on screen.")]
        public float lookAhead = 0f;
    }

    public View[] views =
    {
        new View { name = "Classic",           distance = 4f, height = 1.97f, armLength = 1.45f, fov = 60f, lookAhead = 0f },
        new View { name = "Higher",            distance = 3f, height = 3.0f,  armLength = 1.5f,  fov = 60f, lookAhead = 0.5f },
        new View { name = "Over the shoulder", distance = 3f, height = 1.2f,  armLength = 0.8f,  fov = 65f, lookAhead = 1.2f },
    };
    [Tooltip("The view used until the player picks one in Settings.")]
    public int defaultView = 1;

    const string PrefKey = "LayLow.CameraView";
    public static CameraViewSettings Instance { get; private set; }
    public int Current { get; private set; }

    CinemachineCamera cam;
    CinemachineThirdPersonFollow follow;
    CinemachineHardLookAt look;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<CinemachineCamera>();
        follow = GetComponent<CinemachineThirdPersonFollow>();
        look = GetComponent<CinemachineHardLookAt>();
        Current = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, defaultView), 0, views.Length - 1);
        Apply();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // called by the pause menu's Settings
    public void SetView(int index)
    {
        if (views.Length == 0) return;
        Current = Mathf.Clamp(index, 0, views.Length - 1);
        PlayerPrefs.SetInt(PrefKey, Current);
        PlayerPrefs.Save();
        Apply();
    }

    void Apply()
    {
        if (views.Length == 0) return;
        View v = views[Current];
        if (follow != null)
        {
            follow.CameraDistance = v.distance;
            Vector3 s = follow.ShoulderOffset; s.y = v.height; follow.ShoulderOffset = s;
            follow.VerticalArmLength = v.armLength;
        }
        if (look != null) look.LookAtOffset = new Vector3(0f, 0f, v.lookAhead);
        LensSettings lens = cam.Lens; lens.FieldOfView = v.fov; cam.Lens = lens;
    }

#if UNITY_EDITOR
    // edits in the Inspector show straight away while playing
    void OnValidate() { if (Application.isPlaying && cam != null) Apply(); }
#endif
}
