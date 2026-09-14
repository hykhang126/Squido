using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResetBallZone : MonoBehaviour
{
    void OnCollisionEnter(Collision other)
    {
        if (other.collider.CompareTag("Basketball"))
        {
            if (other.collider.TryGetComponent<BallReset>(out var ballReset))
            {
                ballReset.ResetBall();
            }
        }
    }
}
