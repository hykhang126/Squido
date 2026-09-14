using UnityEngine;

/// <summary>
/// Put on the basketball. Respawns it at spawnPoint either automatically (if it falls below
/// resetYThreshold) or on demand when a hand presses the index trigger (see VRHandGrabber).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GrabbableObject))]
public class BallReset : MonoBehaviour
{
    /// <summary>
    /// Simple single-ball lookup so any hand's trigger press can find "the" ball to reset.
    /// Fine for a single-basketball scene; if you ever add multiple balls, swap this for
    /// a per-ball reference passed in some other way.
    /// </summary>
    public static BallReset Instance { get; private set; }

    public Transform spawnPoint;
    public float resetYThreshold = -5f;

    private Rigidbody rb;
    private GrabbableObject grabbable;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<GrabbableObject>();
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (grabbable.IsGrabbed) return;

        if (transform.position.y < resetYThreshold)
        {
            ResetBall();
        }
    }

    public void ResetBall()
    {
        // Drop out of whichever hand is holding it (if any) first, so it doesn't just
        // get dragged straight back to the hand on the next FollowHand call.
        if (grabbable.IsGrabbed)
        {
            grabbable.ForceDrop();
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;
    }
}
