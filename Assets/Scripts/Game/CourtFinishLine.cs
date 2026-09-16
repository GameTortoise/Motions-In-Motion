using UnityEngine;
public sealed class CourtFinishLine : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var delivery = other.GetComponentInParent<CourtDelivery>();
        if (delivery != null && CourtTrial.Instance != null) CourtTrial.Instance.Arrive(delivery.team);
    }
}
