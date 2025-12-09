
using UnityEngine;
using System.Collections.Generic;

public class AlarmSystem : MonoBehaviour
{
    public static AlarmSystem Instance { get; private set; }

    private List<IAlarmObserver> observers = new List<IAlarmObserver>();
    private bool isAlarmActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterObserver(IAlarmObserver observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void UnregisterObserver(IAlarmObserver observer)
    {
        if (observers.Contains(observer))
        {
            observers.Remove(observer);
        }
    }

    public void TriggerAlarm(Vector3 position)
    {
        if (isAlarmActive) return;

        isAlarmActive = true;
        Debug.Log("ALARM TRIGGERED! Evacuate!");

        foreach (var observer in observers)
        {
            observer.OnAlarmTriggered(position);
        }
    }

    public void ResetAlarm()
    {
        isAlarmActive = false;
        observers.Clear();
        Debug.Log("AlarmSystem: Reset complete.");
    }
}
