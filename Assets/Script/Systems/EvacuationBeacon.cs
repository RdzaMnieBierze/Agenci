
using UnityEngine;

public class EvacuationBeacon : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float visibilityRange = 20f;
    [SerializeField] private bool isActive = true;
    
    [Header("Navigation Chain")]
    [Tooltip("Optional: Next beacon in the evacuation path. Leave empty if this is the final exit.")]
    [SerializeField] private EvacuationBeacon nextBeacon;

    public Vector3 Position => transform.position;
    public float VisibilityRange => visibilityRange;
    public bool IsActive => isActive;
    public EvacuationBeacon NextBeacon => nextBeacon;
    public bool IsFinalExit => nextBeacon == null;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        //Gizmos.DrawWireSphere(transform.position, visibilityRange);
        
        // Draw arrow to next beacon
        if (nextBeacon != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 direction = nextBeacon.Position - transform.position;
            Gizmos.DrawLine(transform.position, nextBeacon.Position);
            
            // Draw arrowhead
            Vector3 arrowHead = nextBeacon.Position - direction.normalized * 1f;
            Gizmos.DrawSphere(nextBeacon.Position, 0.3f);
        }
    }
}
