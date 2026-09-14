using UnityEngine;

/// <summary>
/// Put on the basketball. Respawns it at spawnPoint if it falls below resetYThreshold
/// (e.g. off the edge of the court) and isn't currently being held.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GrabbableObject))]
public class BallReset : MonoBehaviour
{
    public Transform spawnPoint;
    public float resetYThreshold = -5f;

    private Rigidbody rb;
    private GrabbableObject grabbable;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<GrabbableObject>();
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
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;
    }
}
