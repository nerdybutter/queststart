using System;
using UnityEngine;
using Mirror;

public class PvPZone : MonoBehaviour
{
    public Color gizmoColor = new Color(1, 0, 0, 0.25f);
    public Color gizmoWireColor = new Color(1, 0, 0, 0.8f);

    void OnDrawGizmos()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(collider.offset, collider.size);
        Gizmos.color = gizmoWireColor;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}