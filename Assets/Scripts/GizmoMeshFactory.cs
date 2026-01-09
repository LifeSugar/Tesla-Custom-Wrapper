using UnityEngine;

/// <summary>
/// 运行时程序化生成 Gizmo 网格的工厂类
/// 生成圆柱体、圆锥体和立方体，无需外部模型依赖
/// </summary>
public static class GizmoMeshFactory
{
    /// <summary>
    /// 创建圆柱体网格（用于轴杆）
    /// </summary>
    /// <param name="radius">半径</param>
    /// <param name="height">高度</param>
    /// <param name="segments">圆周分段数</param>
    public static Mesh CreateCylinder(float radius = 0.02f, float height = 1f, int segments = 8)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GizmoCylinder";

        int vertexCount = segments * 2 + 2; // 顶部和底部各一圈，加上两个中心点
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[segments * 12]; // 侧面 + 顶底盖

        float angleStep = 360f / segments * Mathf.Deg2Rad;
        float halfHeight = height * 0.5f;

        // 生成顶部和底部圆环顶点
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // 底部圆环
            vertices[i] = new Vector3(x, 0, z);
            normals[i] = new Vector3(x, 0, z).normalized;

            // 顶部圆环
            vertices[i + segments] = new Vector3(x, height, z);
            normals[i + segments] = new Vector3(x, 0, z).normalized;
        }

        // 顶部和底部中心点
        vertices[segments * 2] = new Vector3(0, 0, 0);
        normals[segments * 2] = Vector3.down;
        vertices[segments * 2 + 1] = new Vector3(0, height, 0);
        normals[segments * 2 + 1] = Vector3.up;

        int triIndex = 0;

        // 侧面三角形
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // 第一个三角形
            triangles[triIndex++] = i;
            triangles[triIndex++] = i + segments;
            triangles[triIndex++] = next;

            // 第二个三角形
            triangles[triIndex++] = next;
            triangles[triIndex++] = i + segments;
            triangles[triIndex++] = next + segments;
        }

        // 底部盖
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = segments * 2; // 底部中心
            triangles[triIndex++] = next;
            triangles[triIndex++] = i;
        }

        // 顶部盖
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = segments * 2 + 1; // 顶部中心
            triangles[triIndex++] = i + segments;
            triangles[triIndex++] = next + segments;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// 创建圆锥体网格（用于箭头）
    /// </summary>
    /// <param name="radius">底部半径</param>
    /// <param name="height">高度</param>
    /// <param name="segments">圆周分段数</param>
    public static Mesh CreateCone(float radius = 0.06f, float height = 0.2f, int segments = 8)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GizmoCone";

        int vertexCount = segments + 2; // 底部圆环 + 顶点 + 底部中心
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[segments * 6]; // 侧面 + 底盖

        float angleStep = 360f / segments * Mathf.Deg2Rad;

        // 底部圆环
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices[i] = new Vector3(x, 0, z);
            
            // 圆锥侧面法线需要考虑斜面角度
            Vector3 toTip = new Vector3(0, height, 0) - vertices[i];
            Vector3 tangent = new Vector3(-z, 0, x);
            normals[i] = Vector3.Cross(tangent, toTip).normalized;
        }

        // 顶点
        vertices[segments] = new Vector3(0, height, 0);
        normals[segments] = Vector3.up;

        // 底部中心
        vertices[segments + 1] = new Vector3(0, 0, 0);
        normals[segments + 1] = Vector3.down;

        int triIndex = 0;

        // 侧面三角形
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = i;
            triangles[triIndex++] = segments; // 顶点
            triangles[triIndex++] = next;
        }

        // 底盖三角形
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = segments + 1; // 底部中心
            triangles[triIndex++] = next;
            triangles[triIndex++] = i;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// 创建四边形网格（用于平面拖拽块）
    /// 默认生成在 XY 平面上，中心在 (offset, offset)
    /// </summary>
    /// <param name="size">边长</param>
    /// <param name="offset">距离原点的偏移量</param>
    public static Mesh CreateQuad(float size = 0.3f, float offset = 0f)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GizmoQuad";

        // 为了保证光照和法线正确，正面和背面需要独立的顶点
        Vector3[] vertices = new Vector3[8];
        Vector3[] normals = new Vector3[8];
        int[] triangles = new int[12]; // 2个三角形 * 2面 * 3顶点

        // 基础坐标
        Vector3 v0 = new Vector3(offset, offset + size, 0);       // TL
        Vector3 v1 = new Vector3(offset + size, offset + size, 0); // TR
        Vector3 v2 = new Vector3(offset, offset, 0);             // BL
        Vector3 v3 = new Vector3(offset + size, offset, 0);       // BR

        // 正面顶点 (0-3) - 法线朝 -Z (假设右手拇指法则，0-1-2 CCW)
        // 实际上 Unity Default Quad 法线是 -Z (Look At Camera)
        vertices[0] = v0; vertices[1] = v1; vertices[2] = v2; vertices[3] = v3;
        Vector3 frontNormal = Vector3.back; 
        for(int i=0; i<4; i++) normals[i] = frontNormal;

        // 背面顶点 (4-7) - 法线朝 +Z
        vertices[4] = v0; vertices[5] = v1; vertices[6] = v2; vertices[7] = v3;
        Vector3 backNormal = Vector3.forward;
        for(int i=4; i<8; i++) normals[i] = backNormal;

        // 正面三角形 (CCW: 0->1->2, 2->1->3)
        // 0(TL), 1(TR), 2(BL)
        // 2(BL), 1(TR), 3(BR)
        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 2; triangles[4] = 1; triangles[5] = 3;

        // 背面三角形 (CW from front view -> CCW from back view)
        // 4(TL), 5(TR), 6(BL) -> 4->6->5
        // 6(BL), 5(TR), 7(BR) -> 6->7->5
        triangles[6] = 4; triangles[7] = 6; triangles[8] = 5;
        triangles[9] = 6; triangles[10] = 7; triangles[11] = 5;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        
        // 不需要 RecalculateNormals，因为我们手动设置了

        return mesh;
    }

    /// <summary>
    /// 创建圆环网格（用于旋转轴）
    /// 生成在 XZ 平面上
    /// </summary>
    /// <param name="radius">圆环主半径</param>
    /// <param name="thickness">圆环管子粗细</param>
    /// <param name="segments">主圆周分段数</param>
    /// <param name="tubeSegments">管子截面分段数</param>
    public static Mesh CreateTorus(float radius = 1f, float thickness = 0.05f, int segments = 64, int tubeSegments = 8)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GizmoTorus";

        int vertexCount = (segments + 1) * (tubeSegments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[segments * tubeSegments * 6];

        float angleStep = 360f / segments * Mathf.Deg2Rad;
        float tubeStep = 360f / tubeSegments * Mathf.Deg2Rad;

        int vIndex = 0;
        int tIndex = 0;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            for (int j = 0; j <= tubeSegments; j++)
            {
                float tubeAngle = j * tubeStep;
                float tubeCos = Mathf.Cos(tubeAngle);
                float tubeSin = Mathf.Sin(tubeAngle);

                // 管子中心点
                float tx = radius * cos;
                float tz = radius * sin;

                // 顶点偏移 (管子截面圆, 在 XZ 平面的垂直切面上)
                // 截面圆的 X' 轴沿径向 (tx, tz), Y' 轴沿世界 Y
                float rOffset = thickness * tubeCos;
                float yOffset = thickness * tubeSin;

                // 最终位置
                vertices[vIndex] = new Vector3(
                    (radius + rOffset) * cos,
                    yOffset,
                    (radius + rOffset) * sin
                );

                // 法线
                Vector3 center = new Vector3(tx, 0, tz); // 管子中心
                normals[vIndex] = (vertices[vIndex] - center).normalized;

                vIndex++;
            }
        }

        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int nextI = i + 1;
                int nextJ = j + 1;

                int a = i * (tubeSegments + 1) + j;
                int b = nextI * (tubeSegments + 1) + j;
                int c = i * (tubeSegments + 1) + nextJ;
                int d = nextI * (tubeSegments + 1) + nextJ;

                triangles[tIndex++] = c;
                triangles[tIndex++] = b;
                triangles[tIndex++] = a;

                triangles[tIndex++] = c;
                triangles[tIndex++] = d;
                triangles[tIndex++] = b;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }
}
