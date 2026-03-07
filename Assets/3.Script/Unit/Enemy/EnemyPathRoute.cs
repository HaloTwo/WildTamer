using System.Collections.Generic;
using UnityEngine;

public class EnemyPathRoute : MonoBehaviour
{
    public bool loop = true;
    public List<Transform> points = new();

    void Reset()
    {
        CollectChildren();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (points == null) points = new List<Transform>();
    }
#endif

    [ContextMenu("Collect Children")]
    public void CollectChildren()
    {
        points.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            points.Add(transform.GetChild(i));
        }
    }

    public Transform GetPoint(int index)
    {
        if (points == null || points.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, points.Count - 1);
        return points[index];
    }

}