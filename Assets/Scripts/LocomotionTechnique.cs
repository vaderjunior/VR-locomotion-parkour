using UnityEngine;

public class LocomotionTechnique : MonoBehaviour
{
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    [Range(0, 5)] public float translationGain = 1.0f;
    public GameObject hmd;

    [SerializeField] private float leftTriggerValue;
    [SerializeField] private float rightTriggerValue;
    [SerializeField] private Vector3 startPos;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool isIndexTriggerDown;

    public ParkourCounter parkourCounter;
    public string stage;
    public SelectionTaskMeasure selectionTaskMeasure;

    // ---------- avatar & camera ----------
    [Header("Avatar")]
    public Transform avatarRoot;
    public Rigidbody avatarBody;

    [Header("Third-person camera follow")]
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, -3f);
    public float followLerp = 10f;

    // ---------- left-hand tilt movement ----------
    [Header("Left-hand Tilt Movement")]
    public float maxSpeed = 9f;
    public float moveAccel = 15f;
    public float moveDrag = 4f;
    public float tiltForMaxSpeed = 0.3f;
    public float tiltDeadzone = 0.03f;
    public float tiltGain = 1.5f;

    Quaternion leftNeutralWorld = Quaternion.identity;
    Vector3 upNeutralWorld = Vector3.up;
    Vector3 targetHorizVel = Vector3.zero;

    [Header("Calibration")]
    public float neutralLockDuration = 0.35f;
    float neutralLockUntil = 0f;

    // ---------- hand tracking ----------
    [Header("Hand Tracking (for gestures / poses)")]
    public bool useHandTracking = true;
    public OVRHand leftHand;
    public OVRHand rightHand;

    bool wasFistLastFrame = false;

    // ---------- right-hand swirl / lift ----------
    [Header("Right-hand Swirl / Whip Lift")]
    public float maxLiftAccel = 25f;
    public float swirlForMax = 4f;
    public float minSwirlSpeed = 0.8f;
    public float liftDecayPerSec = 2.0f;

    float liftGauge = 0f;
    Vector3 prevRightVelDir;
    bool havePrevRightVel = false;
    Vector3 prevRightHandPos;
    bool prevRightHandPosValid = false;
    float rightIdleTime = 0f;

    // ---------- left-hand activation zone ----------
    [Header("Left-hand Activation Zone")]
    public bool restrictLeftHandToZone = true;
    public Vector3 leftHandZoneCenterLocal = new Vector3(0f, -0.25f, 0.45f);
    public Vector3 leftHandZoneHalfExtents = new Vector3(0.35f, 0.25f, 0.25f);
    public GameObject leftHandZoneVisual;

    // ---------- right-hand activation zone ----------
    [Header("Right-hand Activation Zone")]
    public bool restrictHandToZone = true;
    public Vector3 handZoneCenterLocal = new Vector3(0f, -0.25f, 0.45f);
    public Vector3 handZoneHalfExtents = new Vector3(0.35f, 0.25f, 0.25f);
    public GameObject handZoneVisual;

    // =================== helpers ===================

    Quaternion HeadYawOnly()
    {
        if (!hmd) return Quaternion.identity;
        Vector3 e = hmd.transform.eulerAngles;
        return Quaternion.Euler(0f, e.y, 0f);
    }

    Quaternion LeftRotationWorld()
    {
        if (useHandTracking && leftHand && leftHand.IsTracked)
        {
            return leftHand.transform.rotation;
        }
        else
        {
            Quaternion local = OVRInput.GetLocalControllerRotation(leftController);
            return transform.rotation * local;
        }
    }

    bool IsLeftFist()
    {
        if (!useHandTracking || !leftHand || !leftHand.IsTracked) return false;

        const float thresh = 0.8f;
        bool index  = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index)  > thresh;
        bool middle = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) > thresh;
        bool ring   = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring)   > thresh;
        bool pinky  = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky)  > thresh;

        return index && middle && ring && pinky;
    }

    void CalibrateLeftNeutral()
    {
        leftNeutralWorld = LeftRotationWorld();
        upNeutralWorld = leftNeutralWorld * Vector3.up;
    }

    bool IsRightHandInZone()
    {
        if (!restrictHandToZone) return true;
        if (!hmd || !rightHand) return false;

        Quaternion yawOnly = HeadYawOnly();
        Vector3 headPos = hmd.transform.position;

        Vector3 toHand = rightHand.transform.position - headPos;
        Vector3 local = Quaternion.Inverse(yawOnly) * toHand;

        Vector3 diff = local - handZoneCenterLocal;
        bool inside =
            Mathf.Abs(diff.x) <= handZoneHalfExtents.x &&
            Mathf.Abs(diff.y) <= handZoneHalfExtents.y &&
            Mathf.Abs(diff.z) <= handZoneHalfExtents.z;

        if (handZoneVisual)
        {
            handZoneVisual.transform.position = headPos + yawOnly * handZoneCenterLocal;
            handZoneVisual.transform.rotation = yawOnly;
            handZoneVisual.transform.localScale = handZoneHalfExtents * 2f;
            handZoneVisual.SetActive(true);
        }

        return inside;
    }

    bool IsLeftHandInZone()
    {
        if (!restrictLeftHandToZone) return true;
        if (!hmd || !leftHand) return false;

        Quaternion yawOnly = HeadYawOnly();
        Vector3 headPos = hmd.transform.position;

        Vector3 toHand = leftHand.transform.position - headPos;
        Vector3 local = Quaternion.Inverse(yawOnly) * toHand;

        Vector3 diff = local - leftHandZoneCenterLocal;
        bool inside =
            Mathf.Abs(diff.x) <= leftHandZoneHalfExtents.x &&
            Mathf.Abs(diff.y) <= leftHandZoneHalfExtents.y &&
            Mathf.Abs(diff.z) <= leftHandZoneHalfExtents.z;

        if (leftHandZoneVisual)
        {
            leftHandZoneVisual.transform.position = headPos + yawOnly * leftHandZoneCenterLocal;
            leftHandZoneVisual.transform.rotation = yawOnly;
            leftHandZoneVisual.transform.localScale = leftHandZoneHalfExtents * 2f;
            leftHandZoneVisual.SetActive(true);
        }

        return inside;
    }

    

    void Start()
    {
        if (!avatarBody && avatarRoot) avatarBody = avatarRoot.GetComponent<Rigidbody>();
        if (!avatarBody) Debug.LogWarning("LocomotionTechnique: assign AvatarRoot & AvatarBody.");

        CalibrateLeftNeutral();
    }

    void Update()
    {
        
        leftTriggerValue  = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,  leftController);
        rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, rightController);
        isIndexTriggerDown = (leftTriggerValue > 0.7f) || (rightTriggerValue > 0.7f);

        // --- calibration: X button OR left fist ---
        bool xPressed  = OVRInput.GetDown(OVRInput.Button.Three);
        bool fistNow   = IsLeftFist();
        bool fistPress = fistNow && !wasFistLastFrame;
        wasFistLastFrame = fistNow;

        if (xPressed || fistPress)
        {
            CalibrateLeftNeutral();
            neutralLockUntil = Time.time + neutralLockDuration;

            targetHorizVel = Vector3.zero;
            if (avatarBody)
            {
                var v = avatarBody.linearVelocity;
                v.x = v.z = 0f;
                avatarBody.linearVelocity = v;
                avatarBody.angularVelocity = Vector3.zero;
            }

            liftGauge = 0f;
            havePrevRightVel = false;
            prevRightHandPosValid = false;
        }

        // =========================================================
        // PART 1: LEFT-HAND TILT (Horizontal Movement)
        // =========================================================
        
        bool isLeftTracked = leftHand &&
                             leftHand.IsTracked &&
                             leftHand.HandConfidence == OVRHand.TrackingConfidence.High;

        bool isLeftInZone = IsLeftHandInZone();

        if (Time.time < neutralLockUntil || !isLeftTracked || !isLeftInZone)
        {
            targetHorizVel = Vector3.zero;
        }
        else
        {
            // Calculate tilt normally
            Quaternion qLeft = LeftRotationWorld();
            Vector3 upWorld = qLeft * Vector3.up;

            Vector3 tiltWorld = new Vector3(
                upWorld.x - upNeutralWorld.x,
                0f,
                upWorld.z - upNeutralWorld.z
            );

            float tiltMag = new Vector2(tiltWorld.x, tiltWorld.z).magnitude;

            if (tiltMag < tiltDeadzone)
            {
                targetHorizVel = Vector3.zero;
            }
            else
            {
                float speed01 = Mathf.Clamp01((tiltMag - tiltDeadzone) / Mathf.Max(0.001f, tiltForMaxSpeed));
                speed01 = Mathf.Clamp01(speed01 * tiltGain);
                speed01 = speed01 * speed01; // smooth curve

                Vector3 dirWorld = tiltWorld.normalized; 
                targetHorizVel = dirWorld * (maxSpeed * translationGain * speed01);
            }
        }

        // =========================================================
        // PART 2: RIGHT HAND SWIRL (Vertical Lift)
        // =========================================================
        
        Vector3 vWorldR = Vector3.zero;
        bool processSwirl = false;

        bool handInZone = IsRightHandInZone();

        // Check tracking
        if (useHandTracking && rightHand && rightHand.IsTracked && handInZone)
        {
            Vector3 currentPos = rightHand.transform.position;
            
            if (!prevRightHandPosValid)
            {
                // First frame of tracking:  set position, not calculate velocity
                prevRightHandPos = currentPos;
                prevRightHandPosValid = true;
            }
            else
            {
                // Calculate velocity manually
                Vector3 delta = currentPos - prevRightHandPos;
                
                // Ignore If hand moved > 0.5 meters in 1 frame(not desired, MAJOR BUG1)
                if (delta.magnitude < 0.5f) 
                {
                    vWorldR = delta / Mathf.Max(Time.deltaTime, 1e-4f);
                    processSwirl = true;
                }
                else 
                {
                    // Glitch detected: Reset previous pos to current so avatar don't jump next frame
                    vWorldR = Vector3.zero;
                }

                prevRightHandPos = currentPos;
            }
        }
        else if (useHandTracking && rightHand && rightHand.IsTracked && !handInZone)
        {
            // Hand tracked but outside the activation zone → treated as idle, decay lift
            prevRightHandPosValid = false;
            havePrevRightVel = false;
            rightIdleTime += Time.deltaTime;
            liftGauge = Mathf.Max(0f, liftGauge - (liftDecayPerSec * 2f * Time.deltaTime));
        }
        else if (!useHandTracking)
        {
            // Controller fallback
            Vector3 vLocalR = OVRInput.GetLocalControllerVelocity(rightController);
            vWorldR = transform.TransformDirection(vLocalR);
            processSwirl = true;
        }
        else 
        {
            // Hand resync
            if (rightHand) prevRightHandPos = rightHand.transform.position;
            liftGauge = Mathf.Max(0f, liftGauge - (liftDecayPerSec * 2f * Time.deltaTime));
        }

        // Right hand logic
        if (processSwirl)
        {
            float speed = vWorldR.magnitude;
            float swirl01 = 0f;

            if (speed > minSwirlSpeed)
            {
                Vector3 dir = vWorldR.normalized;
                float swirlRate = 0f;
                if (havePrevRightVel)
                {
                    float c = Mathf.Clamp(Vector3.Dot(prevRightVelDir, dir), -1f, 1f);
                    float ang = Mathf.Acos(c);
                    swirlRate = ang / Mathf.Max(Time.deltaTime, 1e-4f);
                }
                prevRightVelDir = dir;
                havePrevRightVel = true;

                float swirlRate01 = Mathf.Clamp01(swirlRate / Mathf.Max(0.01f, swirlForMax));
                float speedWeight = Mathf.InverseLerp(minSwirlSpeed, minSwirlSpeed * 1.5f, speed);
                swirl01 = swirlRate01 * speedWeight;

                rightIdleTime = 0f;
            }
            else
            {
                havePrevRightVel = false;
                rightIdleTime += Time.deltaTime;
                if (rightIdleTime > 0.15f)
                {
                    liftGauge = 0f; 
                }
            }

            
            liftGauge = Mathf.Max(liftGauge * Mathf.Exp(-liftDecayPerSec * Time.deltaTime), swirl01);
        }

        // Respawn(maybe i should remove for final build)
        if (OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four))
        {
            if (parkourCounter && parkourCounter.parkourStart && avatarRoot)
            {
                avatarRoot.position = parkourCounter.currentRespawnPos;
                avatarBody.linearVelocity = Vector3.zero;
            }
        }
    }

    void FixedUpdate()
    {
        if (!avatarBody) return;

        // Lift
        if (liftGauge > 0.02f)
        {
            float upAccel = liftGauge * maxLiftAccel;
            avatarBody.AddForce(Vector3.up * upAccel, ForceMode.Acceleration);
        }

        // clamp up speed
        const float maxUpSpeed = 7f;
        Vector3 vel = avatarBody.linearVelocity;
        
        // Only clamp + (up) speed, never clamp falling speed(Major Bug)
        if (vel.y > maxUpSpeed) vel.y = maxUpSpeed;
        
        avatarBody.linearVelocity = vel;

        
        Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
        Vector3 accel = (targetHorizVel - horiz) * moveAccel - horiz * moveDrag;
        avatarBody.AddForce(accel, ForceMode.Acceleration);

        if (targetHorizVel.sqrMagnitude > 0.04f)
        {
            Vector3 face = targetHorizVel; face.y = 0f;
            avatarBody.MoveRotation(
                Quaternion.Slerp(avatarBody.rotation,
                                 Quaternion.LookRotation(face),
                                 0.12f)
            );
        }
    }

    void LateUpdate()
    {
        if (!avatarRoot) return;

        
        Vector3 offsetWorld =
            -avatarRoot.forward * Mathf.Abs(cameraOffset.z) + 
            Vector3.up * cameraOffset.y;

        Vector3 desiredPos = avatarRoot.position + offsetWorld;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            1f - Mathf.Exp(-followLerp * Time.deltaTime)
        );

        // align rig yaw with avatar yaw
        Quaternion targetYaw = Quaternion.Euler(0f, avatarRoot.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetYaw,
            1f - Mathf.Exp(-followLerp * Time.deltaTime)
        );
    }


    
    public void AvatarTriggerEnter(Collider other)
    {
        if (other.CompareTag("banner"))
        {
            stage = other.gameObject.name;
            if (parkourCounter) parkourCounter.isStageChange = true;
        }
        else if (other.CompareTag("objectInteractionTask"))
        {
            if (!selectionTaskMeasure || !hmd) return;

            selectionTaskMeasure.isTaskStart = true;
            selectionTaskMeasure.scoreText.text = "";
            selectionTaskMeasure.partSumErr = 0f;
            selectionTaskMeasure.partSumTime = 0f;

            float tempValueY = other.transform.position.y > 0 ? 12 : 0;
            Vector3 tmpTarget = new(hmd.transform.position.x, tempValueY, hmd.transform.position.z);
            selectionTaskMeasure.taskUI.transform.LookAt(tmpTarget);
            selectionTaskMeasure.taskUI.transform.Rotate(new Vector3(0, 180f, 0));
            selectionTaskMeasure.taskStartPanel.SetActive(true);
        }
        else if (other.CompareTag("coin"))
        {
            if (parkourCounter) parkourCounter.coinCount += 1;
            var src = avatarRoot ? avatarRoot.GetComponent<AudioSource>() : null;
            if (src) src.Play();
            other.gameObject.SetActive(false);
        }
    }
}
