using UnityEngine;

/// <summary>
/// Place on an empty parent above the hoop. Assign two child trigger colliders:
/// - topTrigger: a thin disc/cylinder trigger just above the rim
/// - bottomTrigger: a thin disc/cylinder trigger just below the rim (inside the net)
/// A point is only scored if the ball enters top THEN bottom within maxTransitTime,
/// which filters out the ball bouncing up into the rim from underneath or rolling around the rim.
/// </summary>
public class HoopScoreZone : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public Collider topTrigger;
    public Collider bottomTrigger;

    [Header("Timing")]
    [Tooltip("Max seconds allowed between entering the top trigger and the bottom trigger to count as a score.")]
    public float maxTransitTime = 0.6f;

    [Header("Feedback")]
    public AudioSource swishSfx;

    private float topEnterTime = -10f;
    private bool ballPassedTop;

    void OnEnable()
    {
        var top = topTrigger.gameObject.GetComponent<HoopSubTrigger>();
        if (top == null) top = topTrigger.gameObject.AddComponent<HoopSubTrigger>();
        top.Init(this, true);

        var bottom = bottomTrigger.gameObject.GetComponent<HoopSubTrigger>();
        if (bottom == null) bottom = bottomTrigger.gameObject.AddComponent<HoopSubTrigger>();
        bottom.Init(this, false);
    }

    public void NotifyTopEnter()
    {
        topEnterTime = Time.time;
        ballPassedTop = true;
    }

    public void NotifyBottomEnter()
    {
        if (ballPassedTop && Time.time - topEnterTime <= maxTransitTime)
        {
            Score();
        }
        ballPassedTop = false;
    }

    void Score()
    {
        if (swishSfx != null) swishSfx.Play();
        ScoreManager.Instance?.AddPoint();
    }
}
