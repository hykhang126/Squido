using UnityEngine;

/// <summary>
/// Put this on the basketball along with a Rigidbody and a Collider (non-trigger, for physics).
/// The ball should be tagged "Basketball" for the hoop detector to recognize it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour
{
    private Rigidbody rb;
    private bool isGrabbed;

    public bool IsGrabbed => isGrabbed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Grab(VRHandGrabber grabber)
    {
        isGrabbed = true;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// Called every FixedUpdate by the hand while grabbed. Uses MovePosition/MoveRotation
    /// instead of parenting so the physics engine handles collisions correctly while held
    /// (e.g. the ball won't clip through the backboard if you smash it into one).
    /// </summary>
    public void FollowHand(Vector3 targetPos, Quaternion targetRot, float posSpeed, float rotSpeed)
    {
        if (!isGrabbed) return;

        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, posSpeed * Time.fixedDeltaTime);
        Quaternion newRot = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.fixedDeltaTime);

        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);
    }

    public void Release(Vector3 velocity, Vector3 angularVelocity)
    {
        isGrabbed = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = velocity;
        rb.angularVelocity = angularVelocity;
    }
}
