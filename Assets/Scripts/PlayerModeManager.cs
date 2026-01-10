using UnityEngine;

public enum PlayerMode { Locomotion, ObjectInteraction }

public class PlayerModeManager : MonoBehaviour
{
    [Header("Assign the objects that should be enabled per mode")]
    public Behaviour[] locomotionBehaviours;
    public Behaviour[] interactionBehaviours;

    public PlayerMode CurrentMode { get; private set; } = PlayerMode.Locomotion;

    void Start() => ApplyMode(CurrentMode);

    public void SetMode(PlayerMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ApplyMode(mode);
    }

    void ApplyMode(PlayerMode mode)
    {
        bool locoOn = (mode == PlayerMode.Locomotion);
        bool interactOn = (mode == PlayerMode.ObjectInteraction);

        if (locomotionBehaviours != null)
            foreach (var b in locomotionBehaviours) if (b) b.enabled = locoOn;

        if (interactionBehaviours != null)
            foreach (var b in interactionBehaviours) if (b) b.enabled = interactOn;
    }
}
