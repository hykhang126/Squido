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
    private VRHandGrabber currentHolder;

    public bool IsGrabbed => isGrabbed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Grab(VRHandGrabber grabber)
    {
        currentHolder = grabber;
        isGrabbed = true;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// Immediately drops the ball with no velocity, unlinking it from whichever hand is
    /// holding it (if any). Used by BallReset so a reset doesn't get dragged right back
    /// into the player's hand on the next FollowHand call.
    /// </summary>
    public void ForceDrop()
    {
        if (currentHolder != null)
        {
            currentHolder.ClearHeldReference(this);
            currentHolder = null;
        }

        isGrabbed = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Called every FixedUpdate by the hand while grabbed. Uses MovePosition/MoveRotation
    /// instead of parenting so the physics engine handles collisions correctly while held.
    /// </summary>
    public void FollowHand(Vector3 targetPos, Quaternion targetRot, float posSpeed, float rotSpeed)
    {
        if (!isGrabbed) return;

        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, posSpeed * Time.fixedDeltaTime);
        Quaternion newRot = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.fixedDeltaTime);

        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);
    }

    /// <summary>
    /// Normal release (grip let go, or end of a dribble bounce being caught calls Grab instead).
    /// </summary>
    public void Release(Vector3 velocity, Vector3 angularVelocity)
    {
        currentHolder = null;
        isGrabbed = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = velocity;
        rb.angularVelocity = angularVelocity;
    }

    /// <summary>
    /// Launch is a boosted release: same physics path as Release, but the hand computes
    /// a stronger, direction-guaranteed velocity before calling this.
    /// </summary>
    public void Launch(VRHandGrabber hand, float speedMultiplier, float minSpeed)
    {
        Vector3 handVelocity = hand.GetSmoothedVelocity();
        Vector3 angularVelocity = hand.GetSmoothedAngularVelocity();

        Vector3 direction;
        if (handVelocity.magnitude > 0.05f)
        {
            direction = handVelocity.normalized;
        }
        else
        {
            // Hand was basically still - fall back to where the controller is pointing
            // so pressing Launch always does something meaningful.
            direction = hand.transform.forward;
        }

        float speed = Mathf.Max(handVelocity.magnitude * speedMultiplier, minSpeed);

        Release(direction * speed, angularVelocity);
    }
}
