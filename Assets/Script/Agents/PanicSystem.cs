using Unity.Mathematics;
using UnityEngine;

public class PanicSystem
{
    private float panicLevel = 0f;
    private float panicIncreaseRate = 0.05f;
    private float panicDecreaseRate = 0.1f;
    private float panicThreshold = 0.7f;

    public void UpdatePanicLevel(AgentController agent, float deltaTime)
    {
        // Panika rośnie
        if (agent.TimeSinceLastBeaconSeen > 5f)
            panicLevel += panicIncreaseRate * deltaTime;
        
        if(!agent.HasNearbyAgents(5f))
            panicLevel += panicIncreaseRate * deltaTime;

        if (!agent.NavAgent.hasPath)
            panicLevel -= panicDecreaseRate * deltaTime;

        // Panika maleje
        if (agent.TimeSinceLastBeaconSeen <= 5f && agent.HasTargetBeacon)
            panicLevel -= panicDecreaseRate * deltaTime;

        if (agent.NavAgent.hasPath && agent.NavAgent.remainingDistance < agent.Traits.VisionRange * 0.5f)
            panicLevel -= panicDecreaseRate * deltaTime;

        panicLevel = Mathf.Clamp01(panicLevel);

        if (panicLevel >= panicThreshold)
            TriggerPanicBehavior(agent);
        else
            agent.NavAgent.speed = agent.Traits.MoveSpeed;
        
    }

    private void TriggerPanicBehavior(AgentController agent)
    {
        // Implementacja zachowania paniki, np. losowe poruszanie się
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 10f;
        randomDirection += agent.transform.position;
        UnityEngine.AI.NavMeshHit hit;
        UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, 10f, 1);
        agent.NavAgent.SetDestination(hit.position);
        agent.NavAgent.speed = agent.Traits.MoveSpeed * 1.5f; 
    }
}
