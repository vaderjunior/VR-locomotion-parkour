using UnityEngine;

public class AirballOnly : MonoBehaviour
{
    [SerializeField] private Camera cam;

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // Face camera, but keep upright (no tilt)
        Vector3 toCam = cam.transform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
    }
}
