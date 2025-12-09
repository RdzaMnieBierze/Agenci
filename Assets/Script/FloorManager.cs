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
        if (hideAgentsOnHiddenFloors)
        {
            HideAgentsOnFloor(floor, visible);
        }
        else
        {
            MakeAgentsTransparent(floor, visible);
        }
    }

    private void HideAgentsOnFloor(Floor floor, bool visible)
    {
        // Znajdź wszystkich agentów na tym piętrze
        UnityEngine.AI.NavMeshAgent[] allAgents = FindObjectsOfType<UnityEngine.AI.NavMeshAgent>();
        
        foreach (var agent in allAgents)
        {
            if (IsAgentOnFloor(agent.transform.position, floor))
            {
                Renderer[] agentRenderers = agent.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in agentRenderers)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }

    private void MakeAgentsTransparent(Floor floor, bool visible)
    {
        UnityEngine.AI.NavMeshAgent[] allAgents = FindObjectsOfType<UnityEngine.AI.NavMeshAgent>();
        
        foreach (var agent in allAgents)
        {
            if (IsAgentOnFloor(agent.transform.position, floor))
            {
                Renderer[] agentRenderers = agent.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in agentRenderers)
                {
                    Material[] materials = renderer.materials;
                    foreach (Material mat in materials)
                    {
                        if (visible)
                        {
                            // Pełna widoczność
                            SetMaterialTransparency(mat, 1f);
                        }
                        else
                        {
                            // Przezroczystość
                            SetMaterialTransparency(mat, agentTransparency);
                        }
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

    private bool IsAgentOnFloor(Vector3 position, Floor floor)
    {
        float floorY = (floor.floorNumber - 1) * floorHeight;
        
        return position.y >= floorY - 0.5f && position.y <= floorY + floorHeight + 0.5f;
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