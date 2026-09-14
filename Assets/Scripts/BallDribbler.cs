using UnityEngine;

/// <summary>
/// Put this on the basketball alongside GrabbableObject and Rigidbody.
/// Pressing the dribble button (wired up in VRHandGrabber) while holding the ball triggers
/// one bounce-and-auto-catch cycle. The catch is intentionally forgiving: a generous radius
/// plus a light horizontal magnet pull mean the player doesn't need precise hand positioning
/// or timing to keep a dribble rhythm going - they just need to keep pressing the button.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GrabbableObject))]
public class BallDribbler : MonoBehaviour
{
    private enum DribbleState { Idle, Bouncing }

    [Header("Dribble Feel")]
    [Tooltip("Downward speed applied when you start a dribble bounce.")]
    public float dribbleDownForce = 4.5f;
    [Tooltip("How much of the hand's recent horizontal velocity carries into the bounce, so dribbling while walking/strafing feels natural.")]
    public float horizontalCarryThrough = 0.4f;
    [Tooltip("How close (horizontally, in meters) the ball needs to be to the hand on the way up to auto-catch. Larger = more forgiving.")]
    public float autoCatchRadius = 0.5f;
    [Tooltip("How close in height the ball needs to be to the hand to complete the catch.")]
    public float autoCatchHeightWindow = 0.35f;
    [Tooltip("Horizontal pull-toward-hand speed while the ball is rising and inside the catch radius - purely an assist, not full physics.")]
    public float magnetPullStrength = 6f;

    private Rigidbody rb;
    private GrabbableObject grabbable;
    private DribbleState state = DribbleState.Idle;
    private VRHandGrabber owningHand;

    public bool IsDribbling => state == DribbleState.Bouncing;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<GrabbableObject>();
    }

    /// <summary>
    /// Called by VRHandGrabber when the hand holding this ball presses the dribble button.
    /// Ignored if a bounce is already in progress or the ball isn't currently held.
    /// </summary>
    public void TryStartDribble(VRHandGrabber hand)
    {
        if (state == DribbleState.Bouncing) return;
        if (!grabbable.IsGrabbed) return;

        owningHand = hand;
        state = DribbleState.Bouncing;

        Vector3 handVelocity = hand.GetSmoothedVelocity();
        Vector3 horizontalCarry = Vector3.ProjectOnPlane(handVelocity, Vector3.up) * horizontalCarryThrough;

        grabbable.Release(Vector3.down * dribbleDownForce + horizontalCarry, Vector3.zero);
    }

    void FixedUpdate()
    {
        if (state != DribbleState.Bouncing || owningHand == null) return;

        // Only try to catch once the ball is on its way back up (post-bounce),
        // so we don't snatch it out of the air on the way down.
        if (rb.linearVelocity.y <= 0f) return;

        Vector3 handPos = owningHand.transform.position;
        Vector3 ballPos = transform.position;

        float horizontalDist = Vector3.Distance(
            new Vector3(ballPos.x, 0f, ballPos.z),
            new Vector3(handPos.x, 0f, handPos.z));

        if (horizontalDist > autoCatchRadius)
        {
            // Player moved their hand away - let the ball play out as a normal free ball
            // instead of forcing a catch. They can re-grab it normally afterward.
            state = DribbleState.Idle;
            owningHand = null;
            return;
        }

        // Gentle horizontal assist toward the hand while it rises nearby.
        Vector3 pulledXZ = Vector3.MoveTowards(
            new Vector3(ballPos.x, 0f, ballPos.z),
            new Vector3(handPos.x, 0f, handPos.z),
            magnetPullStrength * Time.fixedDeltaTime);
        rb.MovePosition(new Vector3(pulledXZ.x, ballPos.y, pulledXZ.z));

        float heightDiff = Mathf.Abs(handPos.y - ballPos.y);
        if (heightDiff <= autoCatchHeightWindow)
        {
            CatchBall();
        }
    }

    void CatchBall()
    {
        state = DribbleState.Idle;
        grabbable.Grab(owningHand);
        owningHand.NotifyDribbleCaught(grabbable);
        owningHand.SendHaptic(0.25f, 0.05f);
        owningHand = null;
    }
}
