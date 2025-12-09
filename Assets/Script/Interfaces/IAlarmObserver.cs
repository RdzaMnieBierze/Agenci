using UnityEngine;

public interface IAlarmObserver
{
    void OnAlarmTriggered(Vector3 alarmPosition);
}