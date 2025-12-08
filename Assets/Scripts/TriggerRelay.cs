using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    public LocomotionTechnique locomotion; // drag the OVRCameraRig (with LocomotionTechnique) here

    void OnTriggerEnter(Collider other)
    {
        if (locomotion) locomotion.AvatarTriggerEnter(other);
    }
}
