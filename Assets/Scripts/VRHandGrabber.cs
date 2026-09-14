using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Attach to each hand anchor (Left Controller / Right Controller). Requires a SphereCollider
/// (isTrigger = true) sized roughly to the hand/palm.
///
/// Uses Unity's built-in UnityEngine.XR InputDevice API directly - no Meta/Oculus package,
/// no XR Interaction Toolkit.
///
/// Controls (read per-hand, so they work symmetrically on either controller):
/// - Grip (gripButton): grab / release nearby ball
/// - Primary button (A on right, X on left): launch the held ball with a speed boost
/// - Secondary button (B on right, Y on left): start a dribble bounce on the held ball
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class VRHandGrabber : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Header("Setup")]
    public Handedness hand = Handedness.Right;

    [Header("Velocity Smoothing")]
    [Tooltip("How many FixedUpdate frames to average for release/launch velocity. Higher = smoother but less snappy.")]
    public int velocitySampleCount = 5;

    [Header("Feel")]
    public float followPositionSpeed = 30f;
    public float followRotationSpeed = 30f;
    public float hapticAmplitude = 0.4f;
    public float hapticDuration = 0.08f;

    [Header("Launch (A / X button)")]
    [Tooltip("Multiplies your hand's current speed when you launch the ball, for a punchier throw than a natural release.")]
    public float launchSpeedMultiplier = 1.6f;
    [Tooltip("Minimum launch speed even if your hand is nearly still when you press launch, so it never fizzles.")]
    public float minLaunchSpeed = 4f;

    private GrabbableObject currentGrabbable;
    private GrabbableObject nearbyGrabbable;
    private BallDribbler currentDribbler;

    private readonly Queue<Vector3> positionHistory = new Queue<Vector3>();
    private readonly Queue<Quaternion> rotationHistory = new Queue<Quaternion>();

    private InputDevice device;
    private bool deviceValid;
    private bool wasGripPressed;
    private bool wasPrimaryPressed;
    private bool wasSecondaryPressed;
    private bool wasTriggerPressed;

    void OnEnable()
    {
        TryInitDevice();
        InputDevices.deviceConnected += OnDeviceConnected;
    }

    void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
    }

    void OnDeviceConnected(InputDevice connectedDevice)
    {
        TryInitDevice();
    }

    void TryInitDevice()
    {
        InputDeviceCharacteristics characteristics =
            (hand == Handedness.Left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
            | InputDeviceCharacteristics.Controller;

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count > 0)
        {
            device = devices[0];
            deviceValid = device.isValid;
        }
    }

    void Update()
    {
        if (!deviceValid)
        {
            TryInitDevice();
            if (!deviceValid) return;
        }

        if (!device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed))
        {
            deviceValid = false;
            return;
        }

        if (gripPressed && !wasGripPressed) TryGrab();
        if (!gripPressed && wasGripPressed) ReleaseGrab();
        wasGripPressed = gripPressed;

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed))
        {
            if (primaryPressed && !wasPrimaryPressed) TryLaunch();
            wasPrimaryPressed = primaryPressed;
        }

        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryPressed))
        {
            if (secondaryPressed && !wasSecondaryPressed) TryDribble();
            wasSecondaryPressed = secondaryPressed;
        }

        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed))
        {
            if (triggerPressed && !wasTriggerPressed) TryResetBall();
            wasTriggerPressed = triggerPressed;
        }
    }

    void FixedUpdate()
    {
        RecordVelocitySample();

        if (currentGrabbable != null)
        {
            currentGrabbable.FollowHand(transform.position, transform.rotation, followPositionSpeed, followRotationSpeed);
        }
    }

    void RecordVelocitySample()
    {
        positionHistory.Enqueue(transform.position);
        rotationHistory.Enqueue(transform.rotation);

        if (positionHistory.Count > velocitySampleCount)
        {
            positionHistory.Dequeue();
            rotationHistory.Dequeue();
        }
    }

    void TryGrab()
    {
        if (currentGrabbable != null || nearbyGrabbable == null) return;

        currentGrabbable = nearbyGrabbable;
        currentDribbler = currentGrabbable.GetComponent<BallDribbler>();
        currentGrabbable.Grab(this);

        SendHaptic(hapticAmplitude, hapticDuration);
    }

    void ReleaseGrab()
    {
        if (currentGrabbable == null) return;

        Vector3 releaseVelocity = GetSmoothedVelocity();
        Vector3 releaseAngularVelocity = GetSmoothedAngularVelocity();

        currentGrabbable.Release(releaseVelocity, releaseAngularVelocity);
        currentGrabbable = null;
        currentDribbler = null;

        SendHaptic(hapticAmplitude * 0.5f, hapticDuration);
    }

    void TryLaunch()
    {
        if (currentGrabbable == null) return;

        currentGrabbable.Launch(this, launchSpeedMultiplier, minLaunchSpeed);
        currentGrabbable = null;
        currentDribbler = null;

        SendHaptic(1f, 0.1f);
    }

    void TryDribble()
    {
        if (currentDribbler == null) return;

        currentDribbler.TryStartDribble(this);
        currentGrabbable = null; // ball leaves the hand's direct grip while it bounces

        SendHaptic(hapticAmplitude * 0.6f, 0.05f);
    }

    void TryResetBall()
    {
        if (BallReset.Instance == null) return;

        BallReset.Instance.ResetBall();
        SendHaptic(0.3f, 0.1f);
    }

    /// <summary>
    /// Called by BallDribbler once a dribbled ball auto-catches back into this hand.
    /// </summary>
    public void NotifyDribbleCaught(GrabbableObject g)
    {
        currentGrabbable = g;
        currentDribbler = g.GetComponent<BallDribbler>();
    }

    /// <summary>
    /// Called by GrabbableObject.ForceDrop() (e.g. from a ball reset) so this hand stops
    /// thinking it's still holding an object that was just yanked away from it.
    /// </summary>
    public void ClearHeldReference(GrabbableObject g)
    {
        if (currentGrabbable == g)
        {
            currentGrabbable = null;
            currentDribbler = null;
        }
    }

    public void SendHaptic(float amplitude, float duration)
    {
        if (!deviceValid) return;
        if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    public Vector3 GetSmoothedVelocity()
    {
        if (positionHistory.Count < 2) return Vector3.zero;

        Vector3[] positions = positionHistory.ToArray();
        Vector3 totalDelta = positions[positions.Length - 1] - positions[0];
        float totalTime = Time.fixedDeltaTime * (positions.Length - 1);

        return totalTime > 0f ? totalDelta / totalTime : Vector3.zero;
    }

    public Vector3 GetSmoothedAngularVelocity()
    {
        if (rotationHistory.Count < 2) return Vector3.zero;

        Quaternion[] rotations = rotationHistory.ToArray();
        Quaternion deltaRot = rotations[rotations.Length - 1] * Quaternion.Inverse(rotations[0]);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        float totalTime = Time.fixedDeltaTime * (rotations.Length - 1);
        if (totalTime <= 0f) return Vector3.zero;

        return axis * (angle * Mathf.Deg2Rad / totalTime);
    }

    void OnTriggerEnter(Collider other)
    {
        GrabbableObject g = other.GetComponent<GrabbableObject>();
        if (g != null) nearbyGrabbable = g;
    }

    void OnTriggerExit(Collider other)
    {
        GrabbableObject g = other.GetComponent<GrabbableObject>();
        if (g == nearbyGrabbable) nearbyGrabbable = null;
    }
}
