using UnityEngine;

/// <summary>
/// Auto-attached by HoopScoreZone to its top/bottom trigger colliders.
/// You don't need to add this manually.
/// </summary>
public class HoopSubTrigger : MonoBehaviour
{
    private HoopScoreZone parentZone;
    private bool isTop;

    public void Init(HoopScoreZone zone, bool top)
    {
        parentZone = zone;
        isTop = top;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Basketball")) return;

        if (isTop) parentZone.NotifyTopEnter();
        else parentZone.NotifyBottomEnter();
    }
}
