# Building Evacuation Simulation

> **Real-time 3D simulation of building evacuation during fire emergency**  
> Built with Unity 3D & C# | NavMesh AI

[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-11.0-blue?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![NavMesh](https://img.shields.io/badge/NavMesh-AI-green)](https://docs.unity3d.com/Manual/nav-BuildingNavMesh.html)

## Project Overview

An intelligent agent-based simulation system that models realistic human behavior during building evacuations. Agents navigate multi-floor buildings using visual beacons, avoid dynamic fire hazards, and exhibit emergent panic behaviors under stress.

**Key Features:**
- **Autonomous Agent AI** - Intelligent pathfinding with beacon-based navigation
- **Dynamic Fire Spread** - Real-time fire propagation with vertical floor spreading
- **Multi-Floor Navigation** - Complex 3D pathfinding across stairs and corridors
- **Real-Time Analytics** - Live tracking of evacuation success rates and casualties
- **Interactive Controls** - Floor visibility toggle, simulation parameters adjustment

---

## Technical Highlights

### **AI & Pathfinding**
```csharp
// Smart beacon-based navigation with visibility checks
private EvacuationBeacon FindNearestVisibleBeacon()
{
    float pathDist = GetNavMeshPathDistance(start, beacon.Position);
    if (HasLineOfSight(beacon.Position) && pathDist <= visionRange)
        return beacon;
}
```

- **NavMesh integration** for realistic obstacle avoidance
- **Line-of-sight calculations** for beacon visibility
- **Dynamic rerouting** when paths are blocked by fire
- **Panic system** - Speed increases but decision quality degrades under stress

### **Fire Simulation**
```csharp
// Intelligent fire spread with NavMesh validation
Vector3? spreadPos = FindValidSpreadPosition(sourcePosition);
if (spreadPos.HasValue && !IsTooCloseToOtherFires(spreadPos.Value))
    SpawnFireNode(spreadPos.Value);
```

- **Procedural spread algorithm** - Fire spreads organically across NavMesh surfaces
- **Vertical propagation** - 10% chance to spread down, 5% up between floors
- **NavMeshObstacle carving** - Dynamic pathfinding around fire obstacles
- **Accelerating spread** - Fire intensity increases over time

### **Agent Behavior System**

#### **Pre-Alarm (Working State)**
- Room-based wandering with 25% room change probability
- Time-based behavior: 5-20 seconds per room
- Natural movement through corridors

#### **Post-Alarm (Evacuation State)**
1. **Beacon Detection** → Navigate to nearest visible beacon
2. **Corridor Fallback** → Move to corridors if no beacon visible
3. **Follow Others** → Track agents who recently saw beacons (≤10s)
4. **Panic Mode** → Random wandering when lost

### **Performance Optimizations**
- **Object pooling** for fire nodes (max 250 instances)
- **Spatial partitioning** for agent-to-agent awareness checks
- **Interval-based updates** for fire proximity checks (0.2s)
- **NavMesh path caching** to reduce calculation overhead

---

## Core Systems

### 1️⃣ **Agent Controller**
- Multi-state FSM (Wandering → Evacuating)
- Vision range-based beacon detection
- Panic level calculation affecting speed/decisions
- Death mechanics (fire contact, crowd crush)

### 2️⃣ **Fire Manager**
- Procedural fire spawning in hazard zones
- Smart spreading (avoids over-clustering)
- Multi-floor propagation
- NavMesh integration for pathfinding

### 3️⃣ **Evacuation Stats**
- Real-time evacuation counter
- Fire death tracking
- Crowd density penalties
- Time-based metrics

### 4️⃣ **Tile Density Manager**
- 3D grid-based crowd monitoring
- Automatic penalty system (removal/slowdown)
- Per-floor density calculation
- Visual debug overlays

### 5️⃣ **Floor Manager**
- Multi-floor visibility toggle
- Keyboard controls (1/2/3 for floors)
- Gizmos-based floor visualization

---

## Technologies Used

- **Unity 3D** (2022.3+) - Game engine
- **C# 11.0** - Primary language
- **NavMesh AI** - Pathfinding system
- **TextMeshPro** - UI rendering
- **ProBuilder** - Level design
- **Tilemap** - Floor layouts

---

## Use Cases

- **Building Safety Analysis** - Test evacuation route effectiveness
- **Educational Tool** - Demonstrate crowd dynamics and panic behavior
- **Emergency Planning** - Optimize beacon placement and exit strategies
- **Research Platform** - Study agent-based modeling and emergent behavior

---

## Future Enhancements

- [ ] **Smoke mechanics** - Vision reduction and disorientation
- [ ] **Elevators** - Disabled during fire, trap agents
- [ ] **Group behavior** - Families staying together
- [ ] **Machine learning** - Optimize evacuation strategies via RL

---


**Built with ❤️ and a lot of debugging** 🐛
