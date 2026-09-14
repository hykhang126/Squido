using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Attach to each hand anchor (LeftHandAnchor / RightHandAnchor under OVRCameraRig,
/// or an XR Origin's hand transforms). Requires a SphereCollider (isTrigger = true)
/// sized roughly to the hand/palm.
///
/// Uses Unity's built-in UnityEngine.XR InputDevice API directly - this ships with
/// Unity itself (no Meta/Oculus package, no XR Interaction Toolkit needed).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class VRHandGrabber : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Header("Setup")]
    public Handedness hand = Handedness.Right;

    [Header("Velocity Smoothing")]
    [Tooltip("How many FixedUpdate frames to average for release velocity. Higher = smoother but less snappy.")]
    public int velocitySampleCount = 5;

    [Header("Feel")]
    public float followPositionSpeed = 30f;
    public float followRotationSpeed = 30f;
    public float hapticAmplitude = 0.4f;
    public float hapticDuration = 0.08f;

    private GrabbableObject currentGrabbable;
    private GrabbableObject nearbyGrabbable;

    private readonly Queue<Vector3> positionHistory = new Queue<Vector3>();
    private readonly Queue<Quaternion> rotationHistory = new Queue<Quaternion>();

    private InputDevice device;
    private bool deviceValid;
    private bool wasGripPressed;

    void OnEnable()
    {
        // Try immediately, and also whenever devices connect/disconnect.
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
            // Device stopped responding (e.g. unplugged) - re-acquire next frame.
            deviceValid = false;
            return;
        }

        if (gripPressed && !wasGripPressed) TryGrab();
        if (!gripPressed && wasGripPressed) ReleaseGrab();

        wasGripPressed = gripPressed;
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
        currentGrabbable.Grab(this);

        SendHaptic(hapticAmplitude, hapticDuration);
    }

    void ReleaseGrab()
    {
        if (currentGrabbable == null) return;

        Vector3 releaseVelocity = ComputeAverageVelocity();
        Vector3 releaseAngularVelocity = ComputeAverageAngularVelocity();

        currentGrabbable.Release(releaseVelocity, releaseAngularVelocity);
        currentGrabbable = null;

        SendHaptic(hapticAmplitude * 0.5f, hapticDuration);
    }

    void SendHaptic(float amplitude, float duration)
    {
        if (!deviceValid) return;
        if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    Vector3 ComputeAverageVelocity()
    {
        if (positionHistory.Count < 2) return Vector3.zero;

        Vector3[] positions = positionHistory.ToArray();
        Vector3 totalDelta = positions[positions.Length - 1] - positions[0];
        float totalTime = Time.fixedDeltaTime * (positions.Length - 1);

        return totalTime > 0f ? totalDelta / totalTime : Vector3.zero;
    }

    Vector3 ComputeAverageAngularVelocity()
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
