using UnityEngine;

public class AgentFactory : MonoBehaviour, IAgentFactory
{
    [Header("Prefabs")]
    [SerializeField] private GameObject agentPrefab;

    [Header("Visuals")]
    [SerializeField] private Material adultMaterial;
    [SerializeField] private Material childMaterial;
    [SerializeField] private Material elderlyMaterial;
    [SerializeField] private Material disabledMaterial;
    [SerializeField] private Material blindMaterial;

    public GameObject CreateAgent(AgentType type, Vector3 position)
    {
        if (agentPrefab == null)
        {
            Debug.LogError("AgentFactory: Agent Prefab is missing!");
            return null;
        }

        GameObject newAgent = Instantiate(agentPrefab, position, Quaternion.identity);
        
        // Ensure AgentController exists
        AgentController controller = newAgent.GetComponent<AgentController>();
        if (controller == null)
        {
            controller = newAgent.AddComponent<AgentController>();
        }

        // Generate Traits based on Type
        AgentTraits traits = GenerateTraits(type);

        // Apply Visuals
        ApplyVisuals(newAgent, type);

        // Initialize Controller
        controller.Initialize(type, traits);

        newAgent.name = $"Agent_{type}_{Random.Range(1000, 9999)}";
        return newAgent;
    }

    private AgentTraits GenerateTraits(AgentType type)
    {
        float speed = 3.5f;
        float vision = 10f;
        float hearing = 10f;
        float reaction = 1f;

        switch (type)
        {
            case AgentType.Adult:
                speed = Random.Range(3.5f, 5.0f);
                vision = Random.Range(12f, 18f);
                hearing = Random.Range(8f, 12f);
                reaction = Random.Range(0.5f, 1.0f);
                break;

            case AgentType.Child:
                speed = Random.Range(2.5f, 4.0f); // Slower but energetic
                vision = Random.Range(10f, 15f); // Lower vantage point?
                hearing = Random.Range(10f, 14f); // Good hearing
                reaction = Random.Range(0.8f, 1.5f); // Distracted?
                break;

            case AgentType.Elderly:
                speed = Random.Range(1.5f, 2.5f); // Slow
                vision = Random.Range(5f, 10f); // Poor vision
                hearing = Random.Range(4f, 8f); // Poor hearing
                reaction = Random.Range(1.5f, 3.0f); // Slow reaction
                break;

            case AgentType.Disabled:
                speed = Random.Range(1.0f, 2.0f); // Very slow
                vision = Random.Range(12f, 18f);
                hearing = Random.Range(8f, 12f);
                reaction = Random.Range(1.0f, 2.0f);
                break;
            /*case AgentType.Tester:
                speed = 5.0f;
                vision = 25f;
                hearing = 15f;
                reaction = 0.5f;
                break;*/
        }

        return new AgentTraits(speed, vision, hearing, reaction);
    }

    private void ApplyVisuals(GameObject agent, AgentType type)
    {
        Renderer renderer = agent.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        Material matToUse = adultMaterial;

        switch (type)
        {
            case AgentType.Adult: matToUse = adultMaterial; break;
            case AgentType.Child: matToUse = childMaterial; break;
            case AgentType.Elderly: matToUse = elderlyMaterial; break;
            case AgentType.Disabled: matToUse = disabledMaterial; break;
        }

        if (matToUse != null)
        {
            renderer.material = matToUse;
        }
        
        // Optional: Scale change for Child
        if (type == AgentType.Child)
        {
            agent.transform.localScale = Vector3.one * 0.7f;
        }
        else
        {
            agent.transform.localScale = Vector3.one;
        }
    }
}