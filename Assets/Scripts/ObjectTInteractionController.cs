using UnityEngine;

public class ObjectTInteractionController : MonoBehaviour
{
    public PlayerModeManager modeManager;
    public SelectionTaskMeasure selectionTask;

    public OVRHand leftHand;
    public OVRHand rightHand;

    [Header("Hide avatar during interaction")]
    public GameObject avatarRoot; 
    // private Renderer[] avatarRenderers;
    private SpriteRenderer[] avatarSprites;
    private MeshRenderer[] avatarMeshes;

    [Header("Rotation (Left hand)")]
    public float rotationGain = 1.0f;

    [Header("Translation (Right hand)")]
    [Tooltip("Minimum angle away from neutral before movement starts")]
    public float translationDeadzoneDeg = 6f;

    [Tooltip("How many degrees from neutral corresponds to full speed")]
    public float translationMaxAngleDeg = 35f;

    [Tooltip("Movement speed of the object at full tilt")]
    public float moveSpeed = 0.25f;

    [Tooltip("Multiplier for translation speed")]
    public float translationGain = 1.0f;

    [Tooltip("Smoothing for translation direction ")]
    public float translationSmoothing = 12f;

    [Header("Vertical translation (Right middle pinch)")]
    [Tooltip("Meters moved per meter of hand movement in Y")]
    public float verticalSpeed = 1.0f;

    [Header("Pinch thresholds")]
    [Range(0.1f, 1f)] public float pinchStrengthThreshold = 0.6f;

    [Header("Finish / Exit gesture")]
    [Tooltip("Hold BOTH index pinches for this long to finish and return to locomotion")]
    public float doneHoldSeconds = 0.7f;
    private float doneHoldTimer = 0f;

    // Left rotation state
    private Quaternion prevLeftRot;
    private bool leftPinchActive;

    // Right translation calibration/state
    private bool rightNeutralSet = false;
    private Vector3 rightFwdNeutralFlat = Vector3.forward;
    private Vector3 moveDirSmoothed = Vector3.zero;

    // Right vertical state
    private Vector3 prevRightPos;
    private bool rightMidPinchActive;
    void ForceHideMeshes()
    {
        if (avatarMeshes == null) return;
        foreach (var m in avatarMeshes)
            if (m) m.enabled = false;
    }
    void Awake()
    {
        if (!avatarRoot) return;

        avatarSprites = avatarRoot.GetComponentsInChildren<SpriteRenderer>(true);
        avatarMeshes  = avatarRoot.GetComponentsInChildren<MeshRenderer>(true);

        // Always hide meshes at runtime
        ForceHideMeshes();
    }

    void Update()
    {
        bool inInteraction = modeManager && modeManager.CurrentMode == PlayerMode.ObjectInteraction;

        // Hide/show avatar visuals 
        SetAvatarVisible(!inInteraction);

        if (!inInteraction)
        {
            // Reset so next entry recalibrates
            rightNeutralSet = false;
            leftPinchActive = false;
            rightMidPinchActive = false;
            moveDirSmoothed = Vector3.zero;
            doneHoldTimer = 0f;
            return;
        }

        if (!selectionTask || !selectionTask.objectT) return;

        // Calibrate right hand neutral on entering interaction
        if (!rightNeutralSet)
            CalibrateRightNeutral();

        Transform obj = selectionTask.objectT.transform;

        HandleRotate(obj);
        HandleTranslate(obj);
        HandleDone();
    }

    void HandleRotate(Transform obj)
    {
        bool pinch = IsPinch(leftHand, OVRHand.HandFinger.Index);

        if (pinch && leftHand && leftHand.IsTracked)
        {
            Quaternion cur = leftHand.transform.rotation;

            if (!leftPinchActive)
            {
                leftPinchActive = true;
                prevLeftRot = cur;
                return;
            }

            Quaternion delta = cur * Quaternion.Inverse(prevLeftRot);

            //  POV aligned rotation (apply delta in camera yaw frame)
            Camera cam = Camera.main;
            if (cam)
            {
                Quaternion camYaw = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
                delta = camYaw * delta * Quaternion.Inverse(camYaw);
            }

            if (Mathf.Abs(rotationGain - 1f) > 0.001f)
                delta = Quaternion.Slerp(Quaternion.identity, delta, rotationGain);

            obj.rotation = delta * obj.rotation;
            prevLeftRot = cur;
        }
        else
        {
            leftPinchActive = false;
        }
    }

    void HandleTranslate(Transform obj)
    {
        Camera cam = Camera.main;
        if (!cam || !rightHand || !rightHand.IsTracked) return;

        Quaternion yawOnly = HeadYawOnly(cam);
        Vector3 camFwd = yawOnly * Vector3.forward;
        Vector3 camRight = yawOnly * Vector3.right;
        camFwd.y = 0f;
        camRight.y = 0f;
        camFwd.Normalize();
        camRight.Normalize();

        // --- Right hand: index pinch + tilt (hand forward direction) = camera relative translation ---
        bool rightIndexPinch = IsPinch(rightHand, OVRHand.HandFinger.Index);

        if (rightIndexPinch && rightNeutralSet)
        {
            Vector3 fwd = rightHand.transform.up;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();

            float signedAngle = Vector3.SignedAngle(rightFwdNeutralFlat, fwd, Vector3.up);
            float absAngle = Mathf.Abs(signedAngle);

            if (absAngle >= translationDeadzoneDeg)
            {
                float strength = Mathf.InverseLerp(translationDeadzoneDeg, translationMaxAngleDeg, absAngle);
                strength = Mathf.Clamp01(strength);

                // forward tilt = push away 
                float fwdComp = Vector3.Dot(fwd, camFwd);
                float rightComp = Vector3.Dot(fwd, camRight);

                Vector3 moveDir = camFwd * fwdComp + camRight * rightComp;

                if (moveDir.sqrMagnitude > 1e-6f)
                {
                    moveDir.Normalize();

                    float a = 1f - Mathf.Exp(-translationSmoothing * Time.deltaTime);
                    moveDirSmoothed = Vector3.Lerp(moveDirSmoothed, moveDir, a);

                    obj.position += moveDirSmoothed * (moveSpeed * translationGain * strength * Time.deltaTime);
                }
            }
            else
            {
                float a = 1f - Mathf.Exp(-translationSmoothing * Time.deltaTime);
                moveDirSmoothed = Vector3.Lerp(moveDirSmoothed, Vector3.zero, a);
            }
        }
        else
        {
            float a = 1f - Mathf.Exp(-translationSmoothing * Time.deltaTime);
            moveDirSmoothed = Vector3.Lerp(moveDirSmoothed, Vector3.zero, a);
        }

        // --- Up/down: right middle pinch + hand vertical delta ---
        bool rightMidPinch = IsPinch(rightHand, OVRHand.HandFinger.Middle);
        if (rightMidPinch)
        {
            Vector3 curPos = rightHand.transform.position;

            if (!rightMidPinchActive)
            {
                rightMidPinchActive = true;
                prevRightPos = curPos;
                return;
            }

            float dy = curPos.y - prevRightPos.y;
            obj.position += Vector3.up * (dy * verticalSpeed);

            prevRightPos = curPos;
        }
        else
        {
            rightMidPinchActive = false;
        }
    }

    void HandleDone()
    {
        // Hold BOTH index pinches to finish + return to locomotion
        bool leftDone = IsPinch(leftHand, OVRHand.HandFinger.Index);
        bool rightDone = IsPinch(rightHand, OVRHand.HandFinger.Index);

        if (leftDone && rightDone)
        {
            doneHoldTimer += Time.deltaTime;

            if (doneHoldTimer >= doneHoldSeconds)
            {
                doneHoldTimer = 0f;

                if (selectionTask) selectionTask.EndOneTask();
                if (modeManager) modeManager.SetMode(PlayerMode.Locomotion);
            }
        }
        else
        {
            doneHoldTimer = 0f;
        }
    }

    void CalibrateRightNeutral()
    {
        if (!rightHand || !rightHand.IsTracked) return;

        Vector3 fwd = rightHand.transform.up;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        fwd.Normalize();

        rightFwdNeutralFlat = fwd;
        rightNeutralSet = true;

        moveDirSmoothed = Vector3.zero;
    }

    Quaternion HeadYawOnly(Camera cam)
    {
        Vector3 e = cam.transform.eulerAngles;
        return Quaternion.Euler(0f, e.y, 0f);
    }

    bool IsPinch(OVRHand hand, OVRHand.HandFinger finger)
    {
        if (!hand || !hand.IsTracked) return false;
        return hand.GetFingerIsPinching(finger) &&
               hand.GetFingerPinchStrength(finger) >= pinchStrengthThreshold;
    }

    void SetAvatarVisible(bool visible)
    {
        // ensure meshes never come back
        ForceHideMeshes();

        if (avatarSprites == null || avatarSprites.Length == 0) return;
        foreach (var s in avatarSprites)
            if (s) s.enabled = visible;
    }

    void OnDisable()
    {
        // When leaving interaction mode (this component gets disabled),
        // make sure avatar visuals come back. #Bug fix
        SetAvatarVisible(true);
    }

    void OnDestroy()
    {
        // Safety if object gets destroyed
        SetAvatarVisible(true);
    }

}
