using UnityEngine;
using TMPro;

public class ObjectInteractionInitiatorPinchStart : MonoBehaviour
{
    [Header("References")]
    public PlayerModeManager modeManager;
    public SelectionTaskMeasure selectionTask;

    [Header("Hands (OVR)")]
    public OVRHand leftHand;
    public OVRHand rightHand;

    [Header("UI")]
    public GameObject taskStartPanel;
    public TMP_Text startPanelText;

    [Range(0.1f, 1f)] public float pinchStrengthThreshold = 0.7f;

    private bool insideZone;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entered interaction zone: " + other.name);
        insideZone = true;
        ShowHint(true);
    }

    void OnTriggerExit(Collider other)
    {
        insideZone = false;
        ShowHint(false);
    }

    void Update()
    {
        if (!insideZone) return;
        if (!modeManager || modeManager.CurrentMode != PlayerMode.Locomotion) return;
        if (!selectionTask || selectionTask.isCountdown) return;

        if (IsIndexPinch(leftHand) || IsIndexPinch(rightHand))
        {
            StartTask();
        }
    }

    void StartTask()
    {
        Debug.Log("Pinch detected -> starting interaction task");
        ShowHint(false);
        var loco = modeManager.GetComponent<LocomotionTechnique>();
        if (loco && loco.avatarBody)
        {
        loco.avatarBody.linearVelocity = Vector3.zero;
        loco.avatarBody.angularVelocity = Vector3.zero;
        }
        modeManager.SetMode(PlayerMode.ObjectInteraction);

        selectionTask.isTaskStart = true;
        selectionTask.isTaskEnd = false;
        selectionTask.StartOneTask();
    }

    bool IsIndexPinch(OVRHand hand)
    {
        if (!hand || !hand.IsTracked) return false;
        bool pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        float strength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        return pinching && strength >= pinchStrengthThreshold;
    }

    void ShowHint(bool show)
    {
        if (taskStartPanel) taskStartPanel.SetActive(show);
        if (startPanelText) startPanelText.text = show ? "Pinch to start" : "";
    }
}
