using System.Collections.Generic;
using UnityEngine;

public enum EnemyGroupState
{
    Patrol,
    Combat
}

public class EnemyGroup
{
    public int groupId;

    public EnemyBrain leader;
    public readonly List<EnemyBrain> members = new();

    public EnemyGroupState state = EnemyGroupState.Patrol;
    public Transform combatTarget;
    public float combatExitDelay = 4f;
    public float lastCombatTime;

    public readonly List<Transform> waypoints = new();
    public int currentWaypointIndex = 0;
    public bool loopPath = true;

    public bool IsEmpty()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyBrain m = members[i];
            if (m != null && m.gameObject.activeInHierarchy && !m.IsDead())
                return false;
        }

        return true;
    }

    public void CleanupNullOrDeadMembers()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            EnemyBrain m = members[i];
            if (m == null || !m.gameObject.activeInHierarchy || m.IsDead())
                members.RemoveAt(i);
        }
    }

    public void RefreshLeader()
    {
        if (leader != null && leader.gameObject.activeInHierarchy && !leader.IsDead())
            return;

        leader = null;

        for (int i = 0; i < members.Count; i++)
        {
            EnemyBrain m = members[i];
            if (m != null && m.gameObject.activeInHierarchy && !m.IsDead())
            {
                leader = m;
                return;
            }
        }
    }

    public void EnterCombat(Transform target)
    {
        if (target == null) return;

        state = EnemyGroupState.Combat;
        combatTarget = target;
        lastCombatTime = Time.time;
    }

    public void KeepCombatAlive()
    {
        lastCombatTime = Time.time;
    }

    public void UpdateCombatState()
    {
        if (state != EnemyGroupState.Combat)
            return;

        bool targetAlive = combatTarget != null && combatTarget.gameObject.activeInHierarchy;

        if (targetAlive)
        {
            lastCombatTime = Time.time;
            return;
        }

        if (Time.time - lastCombatTime >= combatExitDelay)
        {
            state = EnemyGroupState.Patrol;
            combatTarget = null;
        }
    }

    public Transform GetCurrentWaypoint()
    {
        if (waypoints.Count == 0)
            return null;

        currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
        return waypoints[currentWaypointIndex];
    }

    public void AdvanceWaypoint()
    {
        if (waypoints.Count == 0)
            return;

        if (loopPath)
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        else
            currentWaypointIndex = Mathf.Min(currentWaypointIndex + 1, waypoints.Count - 1);
    }
}