using System;
using UnityEngine;

[Serializable]
public struct AgentTraits
{
    [Tooltip("Movement speed of the agent.")]
    public float MoveSpeed;

    [Tooltip("Distance at which the agent can see fire.")]
    public float VisionRange;

    [Tooltip("Distance at which the agent can hear alarms or shouts.")]
    public float HearingRange;

    [Tooltip("Time in seconds to react to a stimulus.")]
    public float ReactionTime;

    public AgentTraits(float speed, float vision, float hearing, float reaction)
    {
        MoveSpeed = speed;
        VisionRange = vision;
        HearingRange = hearing;
        ReactionTime = reaction;
    }
}