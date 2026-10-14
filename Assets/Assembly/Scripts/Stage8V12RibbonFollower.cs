using UnityEngine;

/// <summary>
/// Copies the proven archive trajectory into a second, softer gold strand.
/// It is renderer-only: the V07 ritual controller remains authoritative for
/// timing, position, stars, audio and haptics.
/// </summary>
public sealed class Stage8V12RibbonFollower : MonoBehaviour
{
    public LineRenderer source;
    public Renderer ribbon;
    public MeshFilter ribbonFilter;
    public Camera ribbonCamera;
    public float startWidth = 0.0019f;
    public float endWidth = 0.00028f;

    Mesh mesh;

    public void RefreshNow()
    {
        if (!source || !ribbon || !ribbonFilter) return;
        ribbon.enabled = source.enabled;
        if (!source.enabled || source.positionCount < 2) return;

        int count = source.positionCount;
        Vector3[] positions = new Vector3[count];
        source.GetPositions(positions);
        if (!source.useWorldSpace)
            for (int i = 0; i < count; i++)
                positions[i] = source.transform.TransformPoint(positions[i]);

        if (!mesh)
        {
            mesh = new Mesh { name = "Stage8V12_CameraFacingTaperedGoldRibbon" };
            mesh.MarkDynamic();
            ribbonFilter.sharedMesh = mesh;
        }

        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[(count - 1) * 6];
        Camera camera = ribbonCamera ? ribbonCamera : Camera.main;
        Vector3 view = camera ? camera.transform.forward : Vector3.forward;
        for (int i = 0; i < count; i++)
        {
            Vector3 tangent = i == 0
                ? positions[1] - positions[0]
                : i == count - 1
                    ? positions[count - 1] - positions[count - 2]
                    : positions[i + 1] - positions[i - 1];
            Vector3 right = Vector3.Cross(view, tangent).normalized;
            if (right.sqrMagnitude < 0.01f)
                right = camera ? camera.transform.right : Vector3.right;
            float t = i / (count - 1f);
            float width = Mathf.Lerp(startWidth, endWidth, t) *
                (0.82f + 0.18f * Mathf.Sin(t * Mathf.PI));
            Vector3 center = transform.InverseTransformPoint(positions[i]);
            Vector3 offset = transform.InverseTransformVector(right * width);
            vertices[i * 2] = center - offset;
            vertices[i * 2 + 1] = center + offset;
            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);
            float alpha = Mathf.SmoothStep(0.24f, 1f, t) *
                (1f - 0.28f * t);
            colors[i * 2] = colors[i * 2 + 1] =
                new Color(1f, 0.66f, 0.20f, alpha);
            if (i >= count - 1) continue;
            int q = i * 6;
            int v = i * 2;
            triangles[q] = v;
            triangles[q + 1] = v + 2;
            triangles[q + 2] = v + 1;
            triangles[q + 3] = v + 1;
            triangles[q + 4] = v + 2;
            triangles[q + 5] = v + 3;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void LateUpdate() => RefreshNow();
}
