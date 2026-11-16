using UnityEngine;

// Added at runtime to the FPV arms copy (needs to sit next to its Animator to receive the IK callback).
public class FirstPersonArmsIK : MonoBehaviour
{
    [HideInInspector] public FirstPersonArms owner;
    Animator anim;

    void Awake() { anim = GetComponent<Animator>(); }

    void OnAnimatorIK(int layerIndex)
    {
        if (owner != null && anim != null) owner.ApplyIK(anim);
    }

    void LateUpdate()
    {
        if (owner != null && anim != null) owner.HideBody(anim);
    }
}
