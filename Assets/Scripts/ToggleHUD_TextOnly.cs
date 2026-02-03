using UnityEngine;
using TMPro;

public class ToggleHUD_TextOnly : MonoBehaviour
{
    [Header("Hands")]
    public OVRHand leftHand;
    public OVRHand rightHand;

    [Header("Gesture (no pinch): hands close + relaxed fingers)")]
    public float handsCloseDistance = 0.18f;
    [Range(0f, 1f)] public float maxPinchAllowed = 0.6f;

    [Header("Toggle")]
    public bool startHidden = true;
    public float toggleCooldown = 0.4f;

    private bool visible;
    private bool wasGesture;
    private float nextAllowedTime;

    private TMP_Text[] texts;

    void Awake()
    {
        texts = GetComponentsInChildren<TMP_Text>(true);

        visible = !startHidden;
        Apply(visible);

        Debug.Log($"[ToggleHUD_TextOnly] Awake. texts={texts.Length}, startHidden={startHidden}");
    }

    void Update()
    {
        if (!leftHand || !rightHand) return;
        if (!leftHand.IsTracked || !rightHand.IsTracked) return;

        bool gestureNow = IsHandsTogetherNoPinch();

        if (Time.time >= nextAllowedTime && gestureNow && !wasGesture)
        {
            visible = !visible;
            Apply(visible);
            nextAllowedTime = Time.time + toggleCooldown;

            Debug.Log("[ToggleHUD_TextOnly] Toggled -> " + (visible ? "VISIBLE" : "HIDDEN"));
        }

        wasGesture = gestureNow;
    }

    bool IsHandsTogetherNoPinch()
    {
        if (Vector3.Distance(leftHand.transform.position, rightHand.transform.position) > handsCloseDistance)
            return false;

        // no pinches on either hand
        bool leftNoPinch =
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index)  < maxPinchAllowed &&
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) < maxPinchAllowed &&
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring)   < maxPinchAllowed &&
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky)  < maxPinchAllowed;

        bool rightNoPinch =
            rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index)  < maxPinchAllowed &&
            rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) < maxPinchAllowed &&
            rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring)   < maxPinchAllowed &&
            rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky)  < maxPinchAllowed;

        return leftNoPinch && rightNoPinch;
    }

    void Apply(bool show)
    {
        
        foreach (var t in texts)
            if (t) t.enabled = show;
    }
}
