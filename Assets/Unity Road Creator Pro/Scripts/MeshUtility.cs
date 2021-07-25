

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace RoadCreatorPro
{
    public static class MeshUtility
    {
        public static Mesh AssignMesh(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        public static List<int> AddTriangle(List<int> triangles, int one, int two, int three)
        {
            triangles.AddRange(new int[]{ one, two, three});
            return triangles;
        }

        public static List<int> AddSquare(List<int> triangles, int one, int two, int three, int four)
        {
            triangles = AddTriangle(triangles, three, two, four);
            triangles = AddTriangle(triangles, three, one, two);
            return triangles;
        }
    }
}
#endif