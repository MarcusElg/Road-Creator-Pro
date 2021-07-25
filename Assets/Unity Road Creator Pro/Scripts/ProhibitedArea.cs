#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

using UnityEngine;

namespace RoadCreatorPro
{
    [HelpURL("https://mcrafterzz.itch.io/road-creator-pro")]
    public class ProhibitedArea : MonoBehaviour
    {
        // Internal
        public bool controlsFolded = false;
        public List<int> handleHashes = new List<int>();
        public List<int> handleIds = new List<int>();
        public int lastHashIndex = 0;
        public SerializedObject settings;

        public Material centerMaterial;
        public Material outerMaterial;
        public float uvScale = 1;
        public float outerLineWidth = 0.25f;
        public float centerRotation = 0;
        public Transform currentPoint;

        public void Regenerate()
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            CheckVariables();
            List<Vector3> insideVertices = GenerateOuterMesh();
            GenerateInnerMesh(insideVertices);
        }

        public List<Vector3> GenerateOuterMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> innerVertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            for (int i = 0; i < transform.GetChild(0).childCount; i++)
            {
                // Vertices
                Vector3 position = transform.GetChild(0).GetChild(i).transform.localPosition;
                int previousIndex = i - 1;
                if (i == 0)
                {
                    previousIndex = transform.GetChild(0).childCount - 1;
                }

                Vector3 previousPosition = transform.GetChild(0).GetChild(previousIndex).transform.localPosition;
                Vector3 nextPosition = (transform.GetChild(0).GetChild((i + 1) % transform.GetChild(0).childCount)).transform.localPosition;

                Vector3 outerVertex = transform.GetChild(0).GetChild(i).transform.localPosition + new Vector3(0, 0.02f);
                Vector3 nextForward = (nextPosition - position).normalized;
                Vector3 currentForward = (previousPosition - position).normalized;
                Vector3 left = Utility.CalculateLeft(nextForward - currentForward);

                Vector3 innerVertex = outerVertex + left * (outerLineWidth * (1 + (180.1f - Vector3.Angle(nextForward, currentForward)) / 180)); // Compensate for the fact that sharp angles make the line thinner

                vertices.Add(outerVertex);
                vertices.Add(innerVertex);
                innerVertices.Add(innerVertex);

                // Uvs
                Vector3 uv = outerVertex * uvScale / 100;
                uvs.Add(new Vector2(uv.x, uv.z));
                uv = innerVertex * uvScale / 100;
                uvs.Add(new Vector2(uv.x, uv.z));

                // Triangles
                int divider = transform.GetChild(0).childCount * 2;
                triangles = MeshUtility.AddSquare(triangles, (vertices.Count - 1) % divider,
                    (vertices.Count + 1) % divider, (vertices.Count - 2) % divider,
                    (vertices.Count) % divider);
            }

            // Assign mesh
            Mesh mesh = MeshUtility.AssignMesh(vertices, triangles, uvs);

            transform.GetChild(1).GetChild(1).GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.GetChild(1).GetChild(1).GetComponent<MeshRenderer>().sharedMaterial = outerMaterial;

            return innerVertices;
        }

        public void GenerateInnerMesh(List<Vector3> insideVertices)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            // Vertices
            for (int i = 0; i < insideVertices.Count; i++)
            {
                vertices.Add(insideVertices[i]);

                // Uvs
                Vector3 uv = Quaternion.Euler(0, centerRotation, 0) * (insideVertices[i] * uvScale / 100);
                uvs.Add(new Vector2(uv.x, uv.z));
            }

            // Triangles (Polygon triangulation)
            List<int> verticesLeft = new List<int>();

            // Fill vertex list
            for (int i = 0; i < vertices.Count; i++)
            {
                verticesLeft.Add(i);
            }

            // Iterations
            for (int i = 0; i < 100; i++)
            {
                if (verticesLeft.Count == 0)
                {
                    break;
                }

                for (int j = 0; j < verticesLeft.Count; j++)
                {
                    int previousVertexIndex = j - 1;
                    if (previousVertexIndex == -1)
                    {
                        previousVertexIndex = verticesLeft.Count - 1;
                    }

                    int nextVertexIndex = (j + 1) % (verticesLeft.Count);

                    // Check if concave
                    if (!Utility.IsConvex(vertices[verticesLeft[previousVertexIndex]], vertices[verticesLeft[j]], vertices[verticesLeft[nextVertexIndex]]))
                    {
                        // Check if any vertex is inside triangle
                        bool valid = true;
                        for (int k = 0; k < verticesLeft.Count; k++)
                        {
                            // Don't check against vertices that make up the triangle
                            if (k == previousVertexIndex || k == j || k == nextVertexIndex)
                            {
                                continue;
                            }

                            if (Utility.PointInTriangle(new Vector2(vertices[verticesLeft[previousVertexIndex]].x, vertices[verticesLeft[previousVertexIndex]].z), new Vector2(vertices[verticesLeft[j]].x, vertices[verticesLeft[j]].z),
                                new Vector2(vertices[verticesLeft[nextVertexIndex]].x, vertices[verticesLeft[nextVertexIndex]].z), new Vector2(vertices[verticesLeft[k]].x, vertices[verticesLeft[k]].z)))
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (valid)
                        {
                            // Add triangle
                            triangles = MeshUtility.AddTriangle(triangles, verticesLeft[previousVertexIndex], verticesLeft[nextVertexIndex], verticesLeft[j]);

                            // Remove used vertex
                            verticesLeft.RemoveAt(j);
                            break;
                        }
                    }
                }
            }

            // Assign mesh
            Mesh mesh = MeshUtility.AssignMesh(vertices, triangles, uvs);

            transform.GetChild(1).GetChild(0).GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.GetChild(1).GetChild(0).GetComponent<MeshCollider>().sharedMesh = mesh;
            transform.GetChild(1).GetChild(0).GetComponent<MeshRenderer>().sharedMaterial = centerMaterial;
        }

        private void CheckVariables()
        {
            if (centerMaterial == null)
            {
                centerMaterial = Resources.Load("Materials/Prohibited Area") as Material;
            }

            if (outerMaterial == null)
            {
                outerMaterial = Resources.Load("Materials/Road Lines") as Material;
            }
        }

        public void InitializeSystem()
        {
            if (settings == null)
            {
                settings = RoadCreatorSettings.GetSerializedSettings();
            }

            if (transform.childCount == 0)
            {
                GameObject points = new GameObject("Points");
                points.transform.SetParent(transform, true);
                points.transform.localPosition = Vector3.zero;
                points.hideFlags = HideFlags.HideInHierarchy;

                GameObject meshes = new GameObject("Meshes");
                meshes.transform.SetParent(transform, true);
                meshes.transform.localPosition = Vector3.zero;
                meshes.hideFlags = HideFlags.HideInHierarchy;

                GameObject mainMesh = new GameObject("Main Mesh");
                mainMesh.transform.SetParent(meshes.transform);
                Utility.AddCollidableMeshAndOtherComponents(ref mainMesh, new List<System.Type> { typeof(SelectParent) });
                mainMesh.hideFlags = HideFlags.NotEditable;
                mainMesh.transform.localPosition = Vector3.zero;

                GameObject borderMesh = new GameObject("Border Mesh");
                borderMesh.transform.SetParent(meshes.transform);
                Utility.AddMeshAndOtherComponents(ref borderMesh, new List<System.Type> { typeof(SelectParent) });
                borderMesh.hideFlags = HideFlags.NotEditable;
                borderMesh.transform.localPosition = Vector3.zero;

                // Add starting points
                AddDefaultPoint(new Vector3(-2, 0, -2));
                AddDefaultPoint(new Vector3(2, 0, -2));
                AddDefaultPoint(new Vector3(2, 0, 2));
                AddDefaultPoint(new Vector3(-2, 0, 2));
            }
        }

        public void AddDefaultPoint(Vector3 localPosition)
        {
            GameObject point = new GameObject("Point");
            point.hideFlags = HideFlags.NotEditable;
            point.transform.SetParent(transform.GetChild(0));
            point.transform.position = transform.position + localPosition;
        }
    }
}
#endif
