using UnityEngine;

public interface IAgentFactory
{
    GameObject CreateAgent(AgentType type, Vector3 position);
}