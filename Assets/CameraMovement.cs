using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraSwitcher : MonoBehaviour
{
    public CinemachineCamera fpvCamera;
    public CinemachineCamera tpvCamera;

    private bool isFirstPerson = true;

    void Start()
    {
        // Set initial camera priorities on game start
        UpdateCameraPriorities();
    }

    void Update()
    {
        // New Input System check
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            ToggleCamera();
        }
        // Legacy Input System fallback
#else
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleCamera();
        }
#endif
    }

    void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        UpdateCameraPriorities();
    }

    void UpdateCameraPriorities()
    {
        if (fpvCamera == null || tpvCamera == null) return;

        if (isFirstPerson)
        {
            fpvCamera.Priority = 20;
            tpvCamera.Priority = 10;
        }
        else
        {
            fpvCamera.Priority = 10;
            tpvCamera.Priority = 20;
        }
    }
}