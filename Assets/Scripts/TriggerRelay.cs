using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    public LocomotionTechnique locomotion; // drag OVRCameraRig here in Inspector

    // Called when this object (AvatarRoot) hits a trigger collider
    void OnTriggerEnter(Collider other)
    {
        if (locomotion == null) return;
        locomotion.AvatarTriggerEnter(other);
    }

    // Called when this object (AvatarRoot) hits a normal collider
    void OnCollisionEnter(Collision collision)
    {
        if (locomotion == null) return;
        locomotion.AvatarTriggerEnter(collision.collider);
    }
}
