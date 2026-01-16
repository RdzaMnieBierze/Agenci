using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

public class FloorManager : MonoBehaviour
{
    [System.Serializable]
    public class Floor
    {
        public string name;
        public GameObject floorObject;
        public int floorNumber;
        public bool isVisible = true;
    }

    [SerializeField] private List<Floor> floors = new List<Floor>();
    [SerializeField] private int currentFloorView = 3;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI floorInfoText;
    
    [Header("Ustawienia renderowania")]
    [SerializeField] private bool hideAgentsOnHiddenFloors = false;
    [SerializeField] private float agentTransparency = 0.3f;
    [SerializeField] private float floorHeight = 2.2f; // Wysokość jednego piętra
    
    private float agentUpdateTimer = 0f;
    private float agentUpdateInterval = 0.2f; // Odśwież widoczność agentów co 0.2 sekundy
    private UnityEngine.AI.NavMeshAgent[] allAgents = new UnityEngine.AI.NavMeshAgent[0];
    
    private void Start()
    {
        UpdateFloorVisibility();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        
        // Sterowanie klawiszami 1, 2, 3
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("Przełączam na piętro 1");
            SetFloorView(1);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("Przełączam na piętro 2");
            SetFloorView(2);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Debug.Log("Przełączam na piętro 3");
            SetFloorView(3);
        }
        
        // Strzałki w górę/dół
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            Debug.Log("Strzałka w górę");
            SetFloorView(Mathf.Min(currentFloorView + 1, floors.Count));
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            Debug.Log("Strzałka w dół");
            SetFloorView(Mathf.Max(currentFloorView - 1, 1));
        }
        
        // Odśwież widoczność agentów
        agentUpdateTimer += Time.deltaTime;
        if (agentUpdateTimer >= agentUpdateInterval)
        {
            agentUpdateTimer = 0f;
            allAgents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
            UpdateAgentVisibility();
        }
    }

    public void SetFloorView(int floorNumber)
    {
        currentFloorView = floorNumber;
        UpdateFloorVisibility();
        UpdateUI();
    }

    private void UpdateFloorVisibility()
    {
        foreach (Floor floor in floors)
        {
            if (floor.floorObject == null) continue;
            bool shouldBeVisible = floor.floorNumber <= currentFloorView;
            
            SetFloorVisibility(floor, shouldBeVisible);
        }
    }

    private void SetFloorVisibility(Floor floor, bool visible)
    {
        floor.isVisible = visible;

        if (floor.floorObject == null) return;

        Renderer[] renderers = floor.floorObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = visible;
        }
        
        UnityEngine.Tilemaps.TilemapRenderer[] tilemapRenderers = floor.floorObject.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>();
        foreach (var tilemapRenderer in tilemapRenderers)
        {
            tilemapRenderer.enabled = visible;
        }
        // Widoczność agentów jest obsługiwana przez UpdateAgentVisibility() w Update
    }

    private bool IsAgentOnFloor(Vector3 position, Floor floor)
    {
        float floorY = (floor.floorNumber - 1) * floorHeight;
        
        return position.y >= floorY - 0.5f && position.y <= floorY + floorHeight + 0.5f;
    }

    private void UpdateAgentVisibility()
    {
        // Odśwież widoczność wszystkich agentów na podstawie obecnego widoku
        foreach (var agent in allAgents)
        {
            if (agent == null || !agent.isActiveAndEnabled) continue;
            
            // Oblicz na którym piętrze faktycznie jest agent
            int agentFloor = Mathf.FloorToInt(agent.transform.position.y / floorHeight) + 1;
            
            // Agent jest widoczny jeśli jego piętro <= wyświetlane piętro
            bool isOnVisibleFloor = agentFloor <= currentFloorView;
            
            // Zaktualizuj widoczność
            Renderer[] agentRenderers = agent.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in agentRenderers)
            {
                if (hideAgentsOnHiddenFloors)
                {
                    renderer.enabled = isOnVisibleFloor;
                }
                else
                {
                    Material[] materials = renderer.materials;
                    foreach (Material mat in materials)
                    {
                        SetMaterialTransparency(mat, isOnVisibleFloor ? 1f : agentTransparency);
                    }
                }
            }
        }
    }

    private void SetMaterialTransparency(Material mat, float alpha)
    {
        Color color = mat.color;
        color.a = alpha;
        mat.color = color;
        
        // Ustaw rendering mode na transparent jeśli alpha < 1
        if (alpha < 1f)
        {
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    private void UpdateUI()
    {
        if (floorInfoText != null)
        {
            floorInfoText.text = $"Widok: Piętro {currentFloorView} / {floors.Count}";
        }
    }

    // Metody publiczne do wywołania z UI buttonów
    public void NextFloor()
    {
        SetFloorView(Mathf.Min(currentFloorView + 1, floors.Count));
    }

    public void PreviousFloor()
    {
        SetFloorView(Mathf.Max(currentFloorView - 1, 1));
    }

    public void ShowAllFloors()
    {
        SetFloorView(floors.Count);
    }
}