# Airbending Locomotion + Interaction (VR Parkour)

A hand-tracked VR locomotion + interaction project inspired by **airbending** (Avatar: The Last Airbender).

- **Left-hand tilt** = move (direction is mapped to your view / head yaw)
- **Right-hand swirl** = jump (one clean jump, with cooldown)
- Includes the parkour coin loop + the **T-shape object interaction task** with mode switching

Project website / final report page:  
https://vaderjunior.github.io/HCI_IARVR_Blog/

---

## Demo

[Watch demo video (MP4)](_Manu_IARVR_Final.mp4)
---

## How to Start

```bash
git clone https://github.com/vaderjunior/HCI_IARVR_Blog.git
```

- Open the Unity project folder in Unity (the VRParkour project you worked in).
- Main movement logic is in `LocomotionTechnique.cs`.
- Mode switching is handled by `PlayerModeManager` (Locomotion ↔ Interaction).
- Build an APK for Quest (Android) and install it (SideQuest / adb), or test in the Unity Editor.

---

## Controls (Final)

### Locomotion Mode
- **Left hand tilt** (inside the activation zone) → move
- **Right hand swirl** → jump (one impulse)
- **Left fist** → recalibrate neutral (optional)

### Interaction Mode (T-shape task)
- **Index pinch** → start task
- **Left index pinch + wrist rotate** → rotate the object
- **Right index pinch + tilt** → move object in XZ plane
- **Right middle pinch + hand up/down** → move object in Y
- **Hold both index pinches (~1s)** → finish and return to locomotion

---

## Tech

- Unity (course version / LTS)
- Meta XR All-in-One SDK
- Meta Quest (hand tracking enabled)

---

## License

MIT (change if needed)
