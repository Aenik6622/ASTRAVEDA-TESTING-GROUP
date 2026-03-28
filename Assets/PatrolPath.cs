using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("AI/Patrol Path")]
[DisallowMultipleComponent]
public class PatrolPath : MonoBehaviour
{
    [SerializeField] private bool loop = true;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 1f, 0.9f);

    public bool Loop => loop;

    public Transform[] GetPoints()
    {
        List<Transform> points = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                points.Add(child);
            }
        }

        return points.ToArray();
    }

    [ContextMenu("Add Patrol Point")]
    private void AddPatrolPoint()
    {
        GameObject pointObject = new GameObject("Point " + (transform.childCount + 1));
        pointObject.transform.SetParent(transform, false);
        pointObject.transform.localPosition = new Vector3(0f, 0f, (transform.childCount + 1) * 2f);
    }

    private void OnDrawGizmos()
    {
        Transform[] points = GetPoints();
        if (points.Length == 0)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
            {
                continue;
            }

            Gizmos.DrawSphere(points[i].position, 0.25f);

            int nextIndex = i + 1;
            if (nextIndex >= points.Length)
            {
                if (!loop)
                {
                    continue;
                }

                nextIndex = 0;
            }

            if (points[nextIndex] != null)
            {
                Gizmos.DrawLine(points[i].position, points[nextIndex].position);
            }
        }
    }
}
