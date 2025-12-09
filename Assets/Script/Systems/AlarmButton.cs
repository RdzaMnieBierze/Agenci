using UnityEngine;

public class AlarmButton : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 2.0f;

    public Vector3 Position => transform.position;

    public bool CanInteract(Vector3 agentPosition)
    {
        return Vector3.Distance(agentPosition, transform.position) <= interactionDistance;
    }

    public void Interact()
    {
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.TriggerAlarm(transform.position);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}