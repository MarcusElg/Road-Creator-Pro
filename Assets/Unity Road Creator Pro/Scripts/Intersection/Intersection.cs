#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadCreatorPro
{
    [HelpURL("https://mcrafterzz.itch.io/road-creator-pro")]
    public class Intersection : MonoBehaviour
    {
        public bool generateColliders = true;
        public bool cornerSharpnessPerCorner = false;
        public float cornerSharpnessFactor = 0.75f;
        public List<Material> mainMaterials = new List<Material>();
        public PhysicMaterial mainPhysicMaterial;
        public float detailLevel = 10;
        public float uvXScale = 1;
        public float textureTilingMultiplier = 1;
        public bool flipUvs = false;
        public bool automaticallyGenerateMainRoads = false;
        public List<IntersectionMainRoad> mainRoads = new List<IntersectionMainRoad>();

        // Terrain modification
        public bool modifyTerrainHeight = true;
        public float terrainRadius = 20;
        public int terrainSmoothingRadius = 1;
        public float terrainSmoothingAmount = 0.5f;
        public bool modifyTerrainOnUpdate = false;
        public float terrainAngle = 45;
        public float terrainExtraMaxHeight = 3f;
        public float terrainModificationYOffset = 0.1f;

        public bool terrainRemoveDetails = true;
        public float terrainDetailsRadius = 10;
        public bool terrainRemoveDetailsOnUpdate = false;

        public bool terrainRemoveTrees = true;
        public float terrainTreesRadius = 10;
        public bool terrainRemoveTreesOnUpdate = false;

        // Crosswalks
        public bool generateCrosswalks = true;
        public bool generateSameCrosswalkForAllConnections = true;
        public List<IntersectionCrosswalk> crosswalks = new List<IntersectionCrosswalk>();

        // LOD
        public int lodLevels = 3;
        public List<float> lodDistances = new List<float>();
        public List<int> lodVertexDivisions = new List<int>();

        // Internal
        [SerializeReference]
        public List<IntersectionConnection> connections = new List<IntersectionConnection>();
        public SerializedObject settings;
        public int tab = 0;
        public int connectionTab = 0;
        public int mainRoadTab = 0;
        public int crosswalkTab = 0;
        public IntersectionMainRoad defaultMainRoad = new IntersectionMainRoad();
        public IntersectionCrosswalk defaultCrosswalk = new IntersectionCrosswalk();
        public ComputeShader detailComputeShader;
        public ComputeShader treeComputeShader;
        public float heightMovement; // Relative movement in y-axis by handle

        private ComputeShader terrainSmoothShader;
        private int terrainResolution;
        private Vector3 terrainDataSize;
        private Vector3 terrainPosition;

        public int handleHash = 0;
        public int handleId = 0;

        public void InitializeIntersection()
        {
            if (settings == null)
            {
                settings = RoadCreatorSettings.GetSerializedSettings();
            }

            if (transform.childCount == 0)
            {
                GameObject meshes = new GameObject("Meshes");
                meshes.hideFlags = HideFlags.HideInHierarchy;
                meshes.transform.SetParent(transform, false);

                GameObject turnMarkings = new GameObject("Turn Markings");
                turnMarkings.hideFlags = HideFlags.HideInHierarchy;
                turnMarkings.transform.SetParent(transform, false);

                GameObject crosswalks = new GameObject("Crosswalks");
                crosswalks.hideFlags = HideFlags.HideInHierarchy;
                crosswalks.transform.SetParent(transform, false);
            }

            transform.hideFlags = HideFlags.NotEditable;

            // Add lod levels
            lodDistances.Add(0.5f);
            lodDistances.Add(0.7f);
            lodDistances.Add(0.9f);

            lodVertexDivisions.Add(2);
            lodVertexDivisions.Add(5);
            lodVertexDivisions.Add(8);
        }

        public void Regenerate(bool selfChanged, bool updateTerrain = false, bool updateDetails = false, bool updateTrees = false)
        {
            if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            Utility.SetRoadSystemParent(transform);

            // Always modify terrain
            if (modifyTerrainOnUpdate)
            {
                updateTerrain = true;
            }

            if (terrainRemoveDetailsOnUpdate)
            {
                updateDetails = true;
            }

            if (terrainRemoveTreesOnUpdate)
            {
                updateTrees = true;
            }

            // Prevent errors
            if (settings == null)
            {
                settings = RoadCreatorSettings.GetSerializedSettings();
            }

            // Check for deleted roads
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i] == null || connections[i].roadPoint == null)
                {
                    connections.RemoveAt(i);
                }
            }

            // Remove if less than three connections
            if (connections.Count < 3)
            {
                foreach (IntersectionConnection connection in connections)
                {
                    RoadCreator road = connection.GetRoad();
                    Undo.RegisterCompleteObjectUndo(road, "Destroy Intersection");

                    if (this == road.startIntersection)
                    {
                        road.startIntersection = null;
                        road.startIntersectionConnection = null;
                    }
                    else
                    {
                        road.endIntersection = null;
                        road.endIntersectionConnection = null;
                    }
                }

                connections.Clear();
                Undo.DestroyObjectImmediate(gameObject);
                return;
            }

            // Prevent connections duplicating
            if (selfChanged)
            {
                // Copy intersection connection
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i].endConnection)
                    {
                        connections[i].GetRoad().endIntersection = this; // Intersection variable resets on undo but connection does not for some reason
                        connections[i].GetRoad().endIntersectionConnection = connections[i];
                    }
                    else
                    {
                        connections[i].GetRoad().startIntersection = this;
                        connections[i].GetRoad().startIntersectionConnection = connections[i];
                    }
                }
            }
            else
            {
                // Copy road connection
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i].endConnection)
                    {
                        connections[i] = connections[i].GetRoad().endIntersectionConnection;
                    }
                    else
                    {
                        connections[i] = connections[i].GetRoad().startIntersectionConnection;
                    }
                }
            }

            // Move connected points
            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].roadPoint.transform.position += new Vector3(0, heightMovement, 0);
            }
            heightMovement = 0;

            // Set new center position
            Vector3 totalPosition = Vector3.zero;
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i] == null)
                {
                    return;
                }

                totalPosition += connections[i].leftPoint;
            }

            transform.position = totalPosition / connections.Count;

            // Sort connections
            for (int i = 0; i < connections.Count; i++)
            {
                // Compare with Vector3.forward
                connections[i].YRotation = Quaternion.LookRotation((connections[i].roadPoint.transform.position - transform.position).normalized).eulerAngles.y;
            }
            connections.Sort();

            // Calculate new distances
            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].length = Vector3.Distance(connections[i].roadPoint.transform.position, transform.position);
            }

            RecalculateTangents();

            // Calculate left and right points
            for (int i = 0; i < connections.Count; i++)
            {
                int startIndex = connections[i].startIndex;
                int endIndex = connections[i].endIndex;

                Vector3 startPoint = Vector3.zero;
                if (connections[i].endConnection)
                {
                    Vector3[] roadVertices = connections[i].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[i].connectedLaneIndexes[startIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
                    connections[i].leftPoint = roadVertices[roadVertices.Length - 2] + connections[i].roadPoint.transform.parent.GetChild(connections[i].connectedLanes[startIndex].startIndex).position;
                    roadVertices = connections[i].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[i].connectedLaneIndexes[endIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
                    connections[i].rightPoint = roadVertices[roadVertices.Length - 1] + connections[i].roadPoint.transform.parent.GetChild(connections[i].connectedLanes[endIndex].startIndex).position;
                }
                else
                {
                    Vector3[] roadVertices = connections[i].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[i].connectedLaneIndexes[endIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
                    connections[i].leftPoint = roadVertices[1] + connections[i].roadPoint.transform.parent.GetChild(connections[i].connectedLanes[endIndex].startIndex).position;
                    roadVertices = connections[i].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[i].connectedLaneIndexes[startIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
                    connections[i].rightPoint = roadVertices[0] + connections[i].roadPoint.transform.parent.GetChild(connections[i].connectedLanes[startIndex].startIndex).position;
                }
            }

            // Generate main roads
            RemoveGeneratedMainRoads();
            if (automaticallyGenerateMainRoads)
            {
                AddGeneratedMainRoads();
            }

            // Generate mesh
            List<List<Vector3>> vertices = new List<List<Vector3>>();
            List<List<Vector2>> uvs = new List<List<Vector2>>();
            List<List<int>> triangles = new List<List<int>>();

            // Add vertices, triangles and uvs to all lod levels
            for (int i = 0; i < lodLevels + 1; i++)
            {
                vertices.Add(new List<Vector3>());
                uvs.Add(new List<Vector2>());
                triangles.Add(new List<int>());
            }

            CheckVariables();
            RemoveAllChildren(0); // Meshes
            RemoveAllChildren(1); // Turn markings
            RemoveAllChildren(2); // Crosswalks

            // Terrain Generation
            Terrain terrain = null;
            float[,] heightMap = null;
            float[,] originalHeightmap = null;
            HashSet<Vector2Int> detailHeights = new HashSet<Vector2Int>();
            List<TreeInstance> trees = null;
            List<Vector3> treePositions = new List<Vector3>();
            HashSet<Vector3> treesToRemove = new HashSet<Vector3>();
            HashSet<Vector2Int> terrainPoints = new HashSet<Vector2Int>(); // Points to smooth
            HashSet<Vector2Int> finishedTerrainPoints = new HashSet<Vector2Int>(); // Points not to smooth
            Dictionary<Vector2Int, float> terrainDistances = new Dictionary<Vector2Int, float>();

            if (updateTerrain || updateDetails || updateTrees)
            {
                RaycastHit raycastHit;
                if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out raycastHit, 100, ~(1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection") | 1 << LayerMask.NameToLayer("Prefab Line"))))
                {
                    if (raycastHit.transform.GetComponent<Terrain>() != null)
                    {
                        terrain = raycastHit.transform.GetComponent<Terrain>();
                        heightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
                        originalHeightmap = (float[,])heightMap.Clone();
                        terrainResolution = terrain.terrainData.heightmapResolution - 1;
                        terrainDataSize = terrain.terrainData.size;
                        terrainPosition = terrain.transform.position;

                        if (terrainRemoveTrees && updateTrees)
                        {
                            trees = new List<TreeInstance>(terrain.terrainData.treeInstances);

                            for (int i = 0; i < trees.Count; i++)
                            {
                                Vector3 position = Vector3.Scale(trees[i].position, terrain.terrainData.size) + terrain.transform.position;
                                treePositions.Add(position);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < connections.Count; i++)
            {
                // Main mesh
                GenerateMeshSection(i, ref vertices, ref uvs, ref triangles, terrain, ref heightMap, originalHeightmap, ref terrainPoints, ref finishedTerrainPoints, ref detailHeights, ref terrainDistances, ref treePositions, ref treesToRemove, updateTerrain, updateDetails, updateTrees);

                // Turn markings
                PlaceTurnMarkings(i);
            }

            // Main roads
            for (int i = 0; i < mainRoads.Count; i++)
            {
                GenerateMainRoad(i);
            }

            // Crosswalks
            if (generateCrosswalks)
            {
                GenerateCrosswalks();
            }

            // Terrain modification
            if ((updateTerrain || updateDetails || updateTrees) && terrain != null)
            {
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Modify Terrain");

                // Smooth out terrain
                Utility.SmoothTerrain(terrainSmoothShader, terrainResolution, terrainSmoothingRadius, terrainSmoothingAmount, ref heightMap, terrainPoints);

                // Fill heightmap
                if (modifyTerrainHeight && updateTerrain)
                {
                    terrain.terrainData.SetHeights(0, 0, heightMap);
                }

                // Remove details
                if (terrainRemoveDetails && updateDetails)
                {
                    for (int i = 0; i < terrain.terrainData.detailPrototypes.Length; i++)
                    {
                        int[,] detailMap = terrain.terrainData.GetDetailLayer(0, 0, terrain.terrainData.detailWidth, terrain.terrainData.detailHeight, i);

                        foreach (Vector2Int pair in detailHeights)
                        {
                            detailMap[pair.y, pair.x] = 0;
                        }

                        terrain.terrainData.SetDetailLayer(0, 0, i, detailMap);
                    }
                }

                // Remove trees
                if (terrainRemoveTrees && updateTrees)
                {
                    for (int i = trees.Count - 1; i >= 0; i--)
                    {
                        if (treesToRemove.Contains(Vector3.Scale(trees[i].position, terrain.terrainData.size) + terrain.transform.position))
                        {
                            trees.RemoveAt(i);
                        }
                    }

                    terrain.terrainData.SetTreeInstances(trees.ToArray(), false);
                }

                terrain.Flush();
            }

            AssignMesh(vertices, uvs, triangles, "Main Mesh", mainMaterials, mainPhysicMaterial);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void RemoveGeneratedMainRoads()
        {
            for (int i = mainRoads.Count - 1; i >= 0; i--)
            {
                if (mainRoads[i].generated)
                {
                    mainRoads.RemoveAt(i);
                }
            }
        }

        private void AddGeneratedMainRoads()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                // Calculate next connection index
                int nextConnection = i + 1;
                if (i == connections.Count - 1)
                {
                    nextConnection = 0;
                }

                // Check that they have the same amount of lanes left
                int startLanesLeft = 0;
                if (connections[i].endConnection)
                {
                    startLanesLeft = connections[i].startIndex;
                }
                else
                {
                    startLanesLeft = connections[i].connectedLanes.Count - connections[i].endIndex - 1;
                }

                int endLanesLeft = 0;
                if (connections[nextConnection].endConnection)
                {
                    endLanesLeft = connections[nextConnection].connectedLanes.Count - connections[nextConnection].endIndex - 1;
                }
                else
                {
                    endLanesLeft = connections[nextConnection].startIndex;
                }

                if (startLanesLeft != endLanesLeft || startLanesLeft == 0)
                {
                    continue;
                }

                // They have the same amount of connected lanes so generate main roads
                for (int j = 0; j < startLanesLeft; j++)
                {
                    IntersectionMainRoad mainRoad = new IntersectionMainRoad();
                    mainRoad.startIndex = i;
                    mainRoad.endIndex = nextConnection;
                    mainRoad.generated = true;
                    mainRoad.wholeLeftRoad = false;
                    mainRoad.wholeRightRoad = false;
                    mainRoad.yOffset = 0;

                    // Set connected indexes
                    // Indexes for start roads are later flipped so give them the same values here
                    mainRoad.startIndexLeftRoad = j;
                    mainRoad.endIndexLeftRoad = mainRoad.startIndexLeftRoad;

                    mainRoad.startIndexRightRoad = connections[nextConnection].connectedLanes.Count - j - 1;
                    mainRoad.endIndexRightRoad = mainRoad.startIndexRightRoad;

                    // Set material
                    if (connections[i].endConnection)
                    {
                        mainRoad.material = connections[i].connectedLanes[mainRoad.startIndexLeftRoad].materials[0];
                        mainRoad.flipUvs = !connections[i].connectedLanes[mainRoad.startIndexLeftRoad].flipUvs;
                    }
                    else
                    {
                        mainRoad.material = connections[i].connectedLanes[connections[i].connectedLanes.Count - j - 1].materials[0];
                        mainRoad.flipUvs = connections[i].connectedLanes[connections[i].connectedLanes.Count - j - 1].flipUvs;
                    }

                    mainRoads.Add(mainRoad);
                }
            }
        }

        public void CheckVariables()
        {
            if (mainMaterials.Count == 0)
            {
                List<Material> materials = new List<Material>();
                for (int j = 0; j < settings.FindProperty("defaultIntersectionMaterials").arraySize; j++)
                {
                    materials.Add((Material)settings.FindProperty("defaultIntersectionMaterials").GetArrayElementAtIndex(j).objectReferenceValue);
                }

                mainMaterials = materials;
            }

            // Support crosswalks for existing intersections
            if (transform.childCount == 2)
            {
                GameObject crosswalks = new GameObject("Crosswalks");
                crosswalks.hideFlags = HideFlags.HideInHierarchy;
                crosswalks.transform.SetParent(transform, false);
            }

            // Compute shaders
            if (detailComputeShader == null)
            {
                detailComputeShader = Resources.Load("Shaders/RemoveTerrainDetails") as ComputeShader;
            }

            if (treeComputeShader == null)
            {
                treeComputeShader = Resources.Load("Shaders/RemoveTerrainTrees") as ComputeShader;
            }

            if (terrainSmoothShader == null)
            {
                terrainSmoothShader = Resources.Load("Shaders/SmoothTerrain") as ComputeShader;
            }

            // Main roads
            for (int i = 0; i < mainRoads.Count; i++)
            {
                if (mainRoads[i].material == null)
                {
                    mainRoads[i].material = (Material)settings.FindProperty("defaultIntersectionMainRoadMaterial").objectReferenceValue;
                }

                // Fix start and end offsets being out of range
                if (mainRoads[i].endIndex > connections.Count - 1)
                {
                    mainRoads[i].endIndex -= 1;
                    mainRoads[i].startIndex -= 1;

                    if (mainRoads[i].startIndex < 0)
                    {
                        mainRoads[i].startIndex = connections.Count - 1;
                    }
                }

                // Fix left connection lane indexes being out of range
                if (mainRoads[i].endIndexLeftRoad > connections[mainRoads[i].startIndex].connectedLanes.Count - 1)
                {
                    mainRoads[i].endIndexLeftRoad -= 1;
                    mainRoads[i].startIndexLeftRoad -= 1;

                    if (mainRoads[i].startIndexLeftRoad < 0)
                    {
                        mainRoads[i].startIndexLeftRoad = connections[mainRoads[i].startIndex].connectedLanes.Count - 1;
                    }
                }

                // Fix right connection lane indexes being out of range
                if (mainRoads[i].endIndexRightRoad > connections[mainRoads[i].endIndex].connectedLanes.Count - 1)
                {
                    mainRoads[i].endIndexRightRoad -= 1;
                    mainRoads[i].startIndexRightRoad -= 1;

                    if (mainRoads[i].startIndexRightRoad < 0)
                    {
                        mainRoads[i].startIndexRightRoad = connections[mainRoads[i].endIndex].connectedLanes.Count - 1;
                    }
                }
            }

            // Crosswalks
            for (int i = 0; i < crosswalks.Count; i++)
            {
                if (crosswalks[i].material == null)
                {
                    crosswalks[i].material = (Material)settings.FindProperty("defaultIntersectionCrosswalkMaterial").objectReferenceValue;
                }

                crosswalks[i].connectionIndex = Mathf.Clamp(crosswalks[i].connectionIndex, 0, connections.Count - 1);
            }

            // Connections
            for (int i = 0; i < connections.Count; i++)
            {
                // Make sure turn markings array is of the right size
                // Add
                while (connections[i].turnMarkings.Count < connections[i].turnMarkingsAmount)
                {
                    connections[i].turnMarkings.Add(new Vector3Bool(false, true, false));
                }

                // Remove
                while (connections[i].turnMarkings.Count > connections[i].turnMarkingsAmount)
                {
                    connections[i].turnMarkings.RemoveAt(connections[i].turnMarkings.Count - 1);
                }

                // Make sure turn markings X-offsets is right size
                // Add
                while (connections[i].turnMarkingsXOffsets.Count < connections[i].turnMarkingsRepetitions)
                {
                    connections[i].turnMarkingsXOffsets.Add(new FloatList());

                    for (int j = 0; j < connections[i].turnMarkingsAmount; j++)
                    {
                        connections[i].turnMarkingsXOffsets[connections[i].turnMarkingsXOffsets.Count - 1].list.Add(1.5f * (-connections[i].turnMarkingsAmount / 2 + j));
                    }
                }

                // Remove
                while (connections[i].turnMarkingsXOffsets.Count > connections[i].turnMarkingsRepetitions)
                {
                    connections[i].turnMarkingsXOffsets.RemoveAt(connections[i].turnMarkingsXOffsets.Count - 1);
                }

                // Check amount
                for (int j = 0; j < connections[i].turnMarkingsXOffsets.Count; j++)
                {
                    while (connections[i].turnMarkingsXOffsets[j].list.Count < connections[i].turnMarkingsAmount)
                    {
                        connections[i].turnMarkingsXOffsets[j].list.Add(1.5f * (-connections[i].turnMarkingsAmount / 2 + connections[i].turnMarkingsXOffsets[j].list.Count - 1));
                    }

                    while (connections[i].turnMarkingsXOffsets[j].list.Count > connections[i].turnMarkingsAmount)
                    {
                        connections[i].turnMarkingsXOffsets[j].list.RemoveAt(connections[i].turnMarkingsXOffsets[j].list.Count - 1);
                    }
                }
            }

            // Incorrect amount of lod levels
            while (lodLevels > lodDistances.Count)
            {
                float distance = 0.5f;
                int division = 3;

                if (lodDistances.Count > 0)
                {
                    distance = Mathf.Min(lodDistances[lodDistances.Count - 1] + 0.1f, 0.99f);
                    division = lodVertexDivisions[lodVertexDivisions.Count - 1] + 5;

                    // Make sure they don't have the same distances
                    if (lodDistances[lodDistances.Count - 1] >= distance)
                    {
                        lodDistances[lodDistances.Count - 1] = distance -= 0.01f;

                        if (lodDistances.Count > 1)
                        {
                            int k = lodDistances.Count - 2;
                            while (k > 0 && lodDistances[k - 1] >= lodDistances[k])
                            {
                                lodDistances[k - 1] = lodDistances[k] - 0.01f;
                                k--;
                            }
                        }
                    }
                }

                lodDistances.Add(distance);
                lodVertexDivisions.Add(division);
            }

            while (lodLevels < lodDistances.Count)
            {
                lodDistances.RemoveAt(lodDistances.Count - 1);
                lodVertexDivisions.RemoveAt(lodVertexDivisions.Count - 1);
            }
        }

        public void RecalculateTangents()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                int nextIndex = i + 1;
                if (nextIndex > connections.Count - 1)
                {
                    nextIndex = 0;
                }

                Vector3 startTangent = Vector3.zero;
                Vector3 endTangent = Vector3.zero;

                PointData pointData = connections[i].GetRoad().GetPointData();
                if (connections[i].roadPoint.transform.GetSiblingIndex() == 0)
                {
                    startTangent = (pointData.positions[0] - pointData.positions[1]).normalized;
                    connections[i].direction = connections[i].roadPoint.leftLocalControlPointPosition.normalized;
                }
                else
                {
                    startTangent = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
                    connections[i].direction = connections[i].roadPoint.rightLocalControlPointPosition.normalized;
                }

                pointData = connections[nextIndex].GetRoad().GetPointData();
                if (connections[nextIndex].roadPoint.transform.GetSiblingIndex() == 0)
                {
                    endTangent = (pointData.positions[0] - pointData.positions[1]).normalized;
                }
                else
                {
                    endTangent = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
                }

                Vector3 intersectPoint = Utility.GetLineIntersection(connections[i].leftPoint, startTangent, connections[nextIndex].rightPoint, endTangent);

                if (intersectPoint == Utility.MaxVector3)
                {
                    // No point found
                    connections[i].leftTangent = startTangent;
                    connections[nextIndex].rightTangent = endTangent;
                }
                else
                {
                    // Convert cubic bezier curve to quadratic bezier curve
                    connections[i].leftTangent = Utility.ToXZ(intersectPoint - connections[i].leftPoint) * (cornerSharpnessPerCorner ? connections[i].leftCornerSharpness : cornerSharpnessFactor);
                    connections[nextIndex].rightTangent = Utility.ToXZ(intersectPoint - connections[nextIndex].rightPoint) * (cornerSharpnessPerCorner ? connections[i].leftCornerSharpness : cornerSharpnessFactor);
                }
            }
        }

        private void RemoveAllChildren(int childIndex)
        {
            for (int i = transform.GetChild(childIndex).childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(childIndex).GetChild(0).gameObject);
            }
        }

        private void GenerateMeshSection(int index, ref List<List<Vector3>> vertices, ref List<List<Vector2>> uvs, ref List<List<int>> triangles, Terrain terrain, ref float[,] heightMap, float[,] originalHeightmap, ref HashSet<Vector2Int> terrainPoints, ref HashSet<Vector2Int> finishedTerrainPoints, ref HashSet<Vector2Int> detailHeights, ref Dictionary<Vector2Int, float> terrainDistances, ref List<Vector3> treePositions, ref HashSet<Vector3> treesToRemove, bool updateTerrain, bool updateDetails, bool updateTrees)
        {
            int nextIndex = index + 1;
            if (nextIndex > connections.Count - 1)
            {
                nextIndex = 0;
            }

            // Lerp
            Vector3 startPoint = connections[index].leftPoint;
            Vector3 endPoint = connections[nextIndex].rightPoint;
            Vector3 startTangent = startPoint + connections[index].leftTangent;
            Vector3 endTangent = endPoint + connections[nextIndex].rightTangent;

            Vector3 lastInnerPoint = Vector3.zero;
            Vector3 lastOuterPoint = startPoint;

            float totalDistance = connections[index].length + connections[nextIndex].length;

            bool middleAdded = false;
            float segments = Mathf.Max(totalDistance * detailLevel * 0.3f, 3);
            float distance = 0;

            // Calculate curve offsets
            float leftCurveOffset = connections[index].leftCurveOffset;
            float rightCurveOffset = connections[nextIndex].rightCurveOffset;
            float leftStraightTEnd = Mathf.Min(0.4f, leftCurveOffset / totalDistance); // T position when line overgoes to curve
            float rightStartTStart = Mathf.Max(0.6f, 1 - (rightCurveOffset / totalDistance)); // T position when curve overgoes to curve

            for (float t = 0; t <= 1 + 1 / segments; t += 1 / segments)
            {
                float realT = t;
                if (t > 1)
                {
                    realT = 1;
                }

                // Main lod mesh
                Vector3 outerPoint;
                if (realT < leftStraightTEnd)
                {
                    // First straight
                    float straightT = realT / leftStraightTEnd; // Remap to progress in straight, not in total
                    outerPoint = Vector3.Lerp(startPoint, startPoint + Utility.ToXZ(startTangent - startPoint).normalized * leftCurveOffset, straightT);
                }
                else if (realT > rightStartTStart)
                {
                    // Second straight
                    float straightT = (realT - rightStartTStart) / (1 - rightStartTStart); // Remap to progress in straight, not in total
                    if (realT == 1) straightT = 1; // Make sure end is added
                    outerPoint = Vector3.Lerp(endPoint + Utility.ToXZ(endTangent - endPoint).normalized * rightCurveOffset, endPoint, straightT);
                }
                else
                {
                    // Curve
                    Vector3 startCurveOffset = Utility.ToXZ(startTangent - startPoint).normalized * leftCurveOffset;
                    Vector3 endCurveOffset = Utility.ToXZ(endTangent - endPoint).normalized * rightCurveOffset;
                    float curveT = (realT - leftStraightTEnd) / (1 - leftStraightTEnd - (1 - rightStartTStart)); // Remap to be progress in curve, not in total
                    outerPoint = Utility.Lerp4(startPoint + startCurveOffset, endPoint + endCurveOffset, startTangent, endTangent, curveT);
                }

                Vector3 innerPoint = Vector3.zero;

                distance += Vector3.Distance(lastOuterPoint, outerPoint);

                // Always make sure that a point is added in the middle
                if (t <= 0.5f || !middleAdded)
                {
                    if (t >= 0.5f)
                    {
                        middleAdded = true;
                    }

                    Vector3 currentCenterPoint = (connections[index].rightPoint + startPoint) / 2;
                    currentCenterPoint.y = connections[index].rightPoint.y;
                    innerPoint = Vector3.Lerp(currentCenterPoint, transform.position, Mathf.Min(1, t * 2));
                }
                else
                {
                    Vector3 nextCenterPoint = (connections[nextIndex].leftPoint + endPoint) / 2;
                    nextCenterPoint.y = endPoint.y;
                    innerPoint = Vector3.Lerp(transform.position, nextCenterPoint, (t - 0.5f) * 2);
                }

                vertices[0].Add(outerPoint - transform.position);
                vertices[0].Add(innerPoint - transform.position);

                // Uvs
                float uvZ = distance * 0.3f * textureTilingMultiplier;
                if (flipUvs)
                {
                    uvs[0].Add(new Vector2(Vector3.Distance(outerPoint, innerPoint) / uvXScale / 3, uvZ));
                    uvs[0].Add(new Vector2(0, uvZ));
                }
                else
                {
                    uvs[0].Add(new Vector2(0, uvZ));
                    uvs[0].Add(new Vector2(Vector3.Distance(outerPoint, innerPoint) / uvXScale / 3, uvZ));
                }

                if (t > 0)
                {
                    // Add triangles
                    triangles[0].Add(vertices[0].Count - 3);
                    triangles[0].Add(vertices[0].Count - 2);
                    triangles[0].Add(vertices[0].Count - 1);

                    triangles[0].Add(vertices[0].Count - 3);
                    triangles[0].Add(vertices[0].Count - 4);
                    triangles[0].Add(vertices[0].Count - 2);
                }

                // Add to other lod levels
                for (int i = 0; i < lodLevels; i++)
                {
                    // Always generate first last and lane intersection vertices
                    if (t == 0 || realT == 1 || (t >= 0.5f && (t - 1 / segments) < 0.5f) || (vertices[0].Count / 2) % lodVertexDivisions[i] == 0)
                    {
                        // Vertices
                        vertices[i + 1].Add(vertices[0][vertices[0].Count - 2]);
                        vertices[i + 1].Add(vertices[0][vertices[0].Count - 1]);

                        // Uvs
                        uvs[i + 1].Add(uvs[0][uvs[0].Count - 2]);
                        uvs[i + 1].Add(uvs[0][uvs[0].Count - 1]);

                        // Triangles
                        if (vertices[i + 1].Count > 2)
                        {
                            triangles[i + 1].Add(vertices[i + 1].Count - 1);
                            triangles[i + 1].Add(vertices[i + 1].Count - 3);
                            triangles[i + 1].Add(vertices[i + 1].Count - 2);

                            triangles[i + 1].Add(vertices[i + 1].Count - 2);
                            triangles[i + 1].Add(vertices[i + 1].Count - 3);
                            triangles[i + 1].Add(vertices[i + 1].Count - 4);
                        }
                    }
                }

                // Remove details
                if (terrain != null)
                {
                    Vector3 left = (outerPoint - innerPoint).normalized;

                    if (terrainRemoveDetails && updateDetails)
                    {
                        Vector3 forward = new Vector3(-left.z, 0, left.x);
                        Utility.RemoveTerrainDetails(terrain, ref detailHeights, forward, left, ref detailComputeShader, terrainDetailsRadius, innerPoint);
                    }

                    // Remove trees
                    if (terrainRemoveTrees && updateTrees)
                    {
                        Utility.RemoveTerrainTrees(terrain, ref treePositions, ref treesToRemove, ref treeComputeShader, terrainTreesRadius, innerPoint);
                    }

                    // Terrain deformation
                    if (modifyTerrainHeight && updateTerrain)
                    {
                        float worldUnitPerTerrainUnit = 1f / terrainResolution * terrainDataSize.x; // How many world unit does one terrain unit represent
                        Utility.AdjustTerrain(outerPoint, left, (int)(Vector3.Distance(outerPoint, innerPoint) / worldUnitPerTerrainUnit), terrain,
                                                terrainRadius, terrainDataSize, terrainResolution, terrainPosition, terrainAngle, terrainExtraMaxHeight, terrainModificationYOffset, originalHeightmap, ref heightMap, ref terrainPoints, ref finishedTerrainPoints, ref terrainDistances);
                    }
                }

                lastInnerPoint = innerPoint;
                lastOuterPoint = outerPoint;
            }
        }

        private void GenerateMainRoad(int index)
        {
            IntersectionMainRoad mainRoad = mainRoads[index];

            // Calculate positions to lerp between
            Vector3 startLeftPosition = Vector3.zero; // Upper left
            Vector3 startRightPosition = Vector3.zero; // Lower left
            Vector3 endLeftPosition = Vector3.zero; // Lower right
            Vector3 endRightPosition = Vector3.zero; // Upper left

            // Calculate lane indexes
            int startLeftIndex = mainRoad.startIndexLeftRoad;
            int endLeftIndex = mainRoad.endIndexLeftRoad;

            if (mainRoad.wholeLeftRoad)
            {
                startLeftIndex = 0;
                endLeftIndex = connections[mainRoad.startIndex].connectedLanes.Count - 1;
            }

            int startRightIndex = mainRoad.startIndexRightRoad;
            int endRightIndex = mainRoad.endIndexRightRoad;

            if (mainRoad.wholeRightRoad)
            {
                startRightIndex = 0;
                endRightIndex = connections[mainRoad.endIndex].connectedLanes.Count - 1;
            }

            // Flip start connections
            if (!connections[mainRoad.startIndex].endConnection)
            {
                startLeftIndex = connections[mainRoad.startIndex].connectedLanes.Count - 1 - startLeftIndex;
                endLeftIndex = connections[mainRoad.startIndex].connectedLanes.Count - 1 - endLeftIndex;
            }

            if (!connections[mainRoad.endIndex].endConnection)
            {
                startRightIndex = connections[mainRoad.endIndex].connectedLanes.Count - 1 - startRightIndex;
                endRightIndex = connections[mainRoad.endIndex].connectedLanes.Count - 1 - endRightIndex;
            }

            // Calculate start left position
            Vector3[] roadVertices = connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[startLeftIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
            if (connections[mainRoad.startIndex].endConnection)
            {
                startLeftPosition = roadVertices[roadVertices.Length - 2] + connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[startLeftIndex]).transform.position;
            }
            else
            {
                startLeftPosition = roadVertices[1] + connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[startLeftIndex]).transform.position;
            }

            // Calculate start right position
            roadVertices = connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[endLeftIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
            if (connections[mainRoad.startIndex].endConnection)
            {
                startRightPosition = roadVertices[roadVertices.Length - 1] + connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[endLeftIndex]).transform.position;
            }
            else
            {
                startRightPosition = roadVertices[0] + connections[mainRoad.startIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.startIndex].connectedLaneIndexes[endLeftIndex]).transform.position;
            }

            // Calculate end left position
            roadVertices = connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[startRightIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
            if (connections[mainRoad.endIndex].endConnection)
            {
                endLeftPosition = roadVertices[roadVertices.Length - 2] + connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[startRightIndex]).transform.position;
            }
            else
            {
                endLeftPosition = roadVertices[1] + connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[startRightIndex]).transform.position;
            }

            // Calculate end right position
            roadVertices = connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[endRightIndex]).GetComponent<MeshFilter>().sharedMesh.vertices;
            if (connections[mainRoad.endIndex].endConnection)
            {
                endRightPosition = roadVertices[roadVertices.Length - 1] + connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[endRightIndex]).transform.position;
            }
            else
            {
                endRightPosition = roadVertices[0] + connections[mainRoad.endIndex].roadPoint.transform.parent.parent.GetChild(1).GetChild(connections[mainRoad.endIndex].connectedLaneIndexes[endRightIndex]).transform.position;
            }

            float centerDistance = (Vector3.Distance(startLeftPosition, endRightPosition) + Vector3.Distance(startRightPosition, endLeftPosition)) / 2;
            float segments = Mathf.Max(3, centerDistance * detailLevel);

            // Calculate forward directions
            Vector3 leftForward = Vector3.zero;
            Vector3 rightForward = Vector3.zero;

            PointData pointData = connections[mainRoad.startIndex].GetRoad().GetPointData();
            if (connections[mainRoad.startIndex].endConnection)
            {
                leftForward = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
            }
            else
            {
                leftForward = (pointData.positions[0] - pointData.positions[1]).normalized;
            }

            pointData = connections[mainRoad.endIndex].GetRoad().GetPointData();
            if (connections[mainRoad.endIndex].endConnection)
            {
                rightForward = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
            }
            else
            {
                rightForward = (pointData.positions[0] - pointData.positions[1]).normalized;
            }

            // Create mesh
            List<List<Vector3>> vertices = new List<List<Vector3>>();
            List<List<Vector2>> uvs = new List<List<Vector2>>();
            List<List<int>> triangles = new List<List<int>>();

            // Add vertices, triangles and uvs to all lod levels
            for (int i = 0; i < lodLevels + 1; i++)
            {
                vertices.Add(new List<Vector3>());
                uvs.Add(new List<Vector2>());
                triangles.Add(new List<int>());
            }

            Vector3 leftTopTangent = Utility.ToXZ(leftForward).normalized;
            Vector3 rightTopTangent = Utility.ToXZ(rightForward).normalized;
            Vector3 leftBottomTangent = Utility.ToXZ(leftForward).normalized;
            Vector3 rightBottomTangent = Utility.ToXZ(rightForward).normalized;

            // Get correct corner sharpness
            float modifiedCornerSharpnessFactor = cornerSharpnessFactor;
            if (!mainRoad.generated)
            {
                modifiedCornerSharpnessFactor = 0.66f; // Old behaviour
            }
            else if (!cornerSharpnessPerCorner)
            {
                modifiedCornerSharpnessFactor = cornerSharpnessFactor;
            }
            else if (mainRoad.endIndex > mainRoad.startIndex || (mainRoad.endIndex == 0 && mainRoad.startIndex == connections.Count - 1))
            {
                // Curve to the left
                modifiedCornerSharpnessFactor = connections[mainRoad.startIndex].leftCornerSharpness;
            }
            else
            {
                // Curve to the right
                modifiedCornerSharpnessFactor = connections[mainRoad.endIndex].leftCornerSharpness;
            }

            // Calculate top tangents
            Vector3 curvePoint = Utility.GetLineIntersection(startLeftPosition, leftForward, endRightPosition, rightForward);

            if (curvePoint != Utility.MaxVector3)
            {
                leftTopTangent = Utility.ToXZ(curvePoint - startLeftPosition) * modifiedCornerSharpnessFactor + startLeftPosition;
                rightTopTangent = Utility.ToXZ(curvePoint - endRightPosition) * modifiedCornerSharpnessFactor + endRightPosition;
            }
            else
            {
                leftTopTangent += startLeftPosition;
                rightTopTangent += endRightPosition;
            }

            // Calculate bottom tangents
            curvePoint = Utility.GetLineIntersection(startRightPosition, leftForward, endLeftPosition, rightForward);

            if (curvePoint != Utility.MaxVector3)
            {
                leftBottomTangent = Utility.ToXZ(curvePoint - startRightPosition) * modifiedCornerSharpnessFactor + startRightPosition;
                rightBottomTangent = Utility.ToXZ(curvePoint - endLeftPosition) * modifiedCornerSharpnessFactor + endLeftPosition;
            }
            else
            {
                leftBottomTangent += startRightPosition;
                rightBottomTangent += endLeftPosition;
            }

            // Calculate curve offsets
            float leftCurveOffset = mainRoad.generated ? connections[mainRoad.startIndex].leftCurveOffset : 0;
            float rightCurveOffset = mainRoad.generated ? connections[mainRoad.endIndex].rightCurveOffset : 0;
            float leftStraightTEnd = Mathf.Min(0.4f, leftCurveOffset / centerDistance); // T position when line overgoes to curve
            float rightStartTStart = Mathf.Max(0.6f, 1 - (rightCurveOffset / centerDistance)); // T position when curve overgoes to curve

            // Calculate vertices
            float distance = 0;
            Vector3 lastLeftPosition = Vector3.zero;

            for (float t = 0; t <= 1 + 1 / segments; t += 1 / segments)
            {
                float realT = t;
                if (realT > 1)
                {
                    realT = 1;
                }

                // Main lod level
                // Add vertices
                Vector3 leftPosition, rightPosition;

                if (realT < leftStraightTEnd)
                {
                    // First straight
                    float straightT = realT / leftStraightTEnd; // Remap to progress in straight, not in total
                    leftPosition = Vector3.Lerp(startLeftPosition, startLeftPosition + Utility.ToXZ(leftForward).normalized * leftCurveOffset, straightT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                    rightPosition = Vector3.Lerp(startRightPosition, startRightPosition + Utility.ToXZ(leftForward).normalized * leftCurveOffset, straightT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                }
                else if (realT > rightStartTStart)
                {
                    // Second straight
                    float straightT = (realT - rightStartTStart) / (1 - rightStartTStart); // Remap to progress in straight, not in total
                    if (realT == 1) straightT = 1; // Make sure end is added
                    leftPosition = Vector3.Lerp(endRightPosition + Utility.ToXZ(rightForward).normalized * rightCurveOffset, endRightPosition, straightT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                    rightPosition = Vector3.Lerp(endLeftPosition + Utility.ToXZ(rightForward).normalized * rightCurveOffset, endLeftPosition, straightT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                }
                else
                {
                    // Curve
                    Vector3 startCurveOffset = Utility.ToXZ(leftForward).normalized * leftCurveOffset;
                    Vector3 endCurveOffset = Utility.ToXZ(rightForward).normalized * rightCurveOffset;
                    float curveT = (realT - leftStraightTEnd) / (1 - leftStraightTEnd - (1 - rightStartTStart)); // Remap to be progress in curve, not in total
                    leftPosition = Utility.Lerp4(startLeftPosition + startCurveOffset, endRightPosition + endCurveOffset, leftTopTangent, rightTopTangent, curveT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                    rightPosition = Utility.Lerp4(startRightPosition + startCurveOffset, endLeftPosition + endCurveOffset, leftBottomTangent, rightBottomTangent, curveT) + new Vector3(0, mainRoad.yOffset, 0) - transform.position;
                }

                vertices[0].Add(leftPosition);
                vertices[0].Add(rightPosition);

                distance += Vector3.Distance(lastLeftPosition, leftPosition);
                lastLeftPosition = leftPosition;

                float uvZ = distance * mainRoad.textureTilingMultiplier * 0.3f + mainRoad.textureTilingOffset;
                if (mainRoad.flipUvs)
                {
                    uvs[0].Add(new Vector2(1, uvZ));
                    uvs[0].Add(new Vector2(0, uvZ));
                }
                else
                {
                    uvs[0].Add(new Vector2(0, uvZ));
                    uvs[0].Add(new Vector2(1, uvZ));
                }

                // Add triangles
                if (realT > 0)
                {
                    triangles[0].Add(vertices[0].Count - 1);
                    triangles[0].Add(vertices[0].Count - 3);
                    triangles[0].Add(vertices[0].Count - 2);

                    triangles[0].Add(vertices[0].Count - 3);
                    triangles[0].Add(vertices[0].Count - 4);
                    triangles[0].Add(vertices[0].Count - 2);
                }

                // Add to other lod levels
                for (int i = 0; i < lodLevels; i++)
                {
                    // Always generate first last and lane intersection vertices
                    if (t == 0 || realT == 1 || (vertices[0].Count / 2) % lodVertexDivisions[i] == 0)
                    {
                        // Vertices
                        vertices[i + 1].Add(vertices[0][vertices[0].Count - 2]);
                        vertices[i + 1].Add(vertices[0][vertices[0].Count - 1]);

                        // Uvs
                        uvs[i + 1].Add(uvs[0][uvs[0].Count - 2]);
                        uvs[i + 1].Add(uvs[0][uvs[0].Count - 1]);

                        // Triangles
                        if (vertices[i + 1].Count > 2)
                        {
                            triangles[i + 1].Add(vertices[i + 1].Count - 1);
                            triangles[i + 1].Add(vertices[i + 1].Count - 3);
                            triangles[i + 1].Add(vertices[i + 1].Count - 2);

                            triangles[i + 1].Add(vertices[i + 1].Count - 2);
                            triangles[i + 1].Add(vertices[i + 1].Count - 3);
                            triangles[i + 1].Add(vertices[i + 1].Count - 4);
                        }
                    }
                }
            }

            // Center center point for editor gui arrow
            mainRoad.centerPoint = (Utility.Lerp4(startLeftPosition, endRightPosition, leftTopTangent, rightTopTangent, 0.5f) + Utility.Lerp4(startRightPosition, endLeftPosition, leftBottomTangent, rightBottomTangent, 0.5f)) / 2 + new Vector3(0, 0.01f, 0);

            // Assign mesh
            List<Material> materials = new List<Material>();
            materials.Add(mainRoad.material);
            AssignMesh(vertices, uvs, triangles, "Main Road Mesh", materials, mainRoad.physicMaterial);
        }

        private void GenerateCrosswalks()
        {
            if (generateSameCrosswalkForAllConnections)
            {
                // Make sure that there are the same amount of crosswalks as connections
                // Remove crosswalks
                for (int i = crosswalks.Count - 1; i >= connections.Count; i--)
                {
                    crosswalks.RemoveAt(i);
                }

                // Add crosswalks
                for (int i = crosswalks.Count; i < connections.Count; i++)
                {
                    crosswalks.Add(new IntersectionCrosswalk());
                }

                // Copy default crosswalk if needed
                if (crosswalks[0].material == null)
                {
                    Utility.CopyCrosswalkData(defaultCrosswalk, crosswalks[0]);
                    crosswalks[0].material = (Material)settings.FindProperty("defaultIntersectionCrosswalkMaterial").objectReferenceValue;
                }

                for (int i = 0; i < crosswalks.Count; i++)
                {
                    Utility.CopyCrosswalkData(crosswalks[0], crosswalks[i]); // Copy data from first crosswalk
                    crosswalks[i].connectionIndex = i; // Set connections indexes
                }
            }

            for (int i = 0; i < crosswalks.Count; i++)
            {
                GenerateCrosswalk(i);
            }
        }

        private void GenerateCrosswalk(int i)
        {
            IntersectionCrosswalk crosswalk = crosswalks[i];
            IntersectionConnection connection = connections[crosswalk.connectionIndex];
            crosswalk.centerPoint = connection.roadPoint.transform.position + connection.direction * (crosswalk.width / 2); // Set arrow position

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // Vertices
            bool connectionAnchoring = crosswalk.anchorAtConnection;

            if (!connectionAnchoring)
            {
                int previousIndex = i == 0 ? connections.Count - 1 : i - 1;
                int nextIndex = (i + 1) % connections.Count;

                Vector3 leftIntersectionPoint = Utility.GetLineIntersection(connections[nextIndex].rightPoint, connections[nextIndex].rightTangent.normalized, connection.leftPoint, connection.leftTangent.normalized);
                Vector3 rightIntersectionPoint = Utility.GetLineIntersection(connection.rightPoint, connection.rightTangent.normalized, connections[previousIndex].leftPoint, connections[previousIndex].leftTangent.normalized);

                float anglePreviousConnection = Vector3.Angle(connection.direction, connections[previousIndex].direction);
                float angleNextConnection = Vector3.Angle(connection.direction, connections[nextIndex].direction);

                if (leftIntersectionPoint == Utility.MaxVector3 || rightIntersectionPoint == Utility.MaxVector3 || anglePreviousConnection > 140 || angleNextConnection > 140)
                {
                    connectionAnchoring = true; // Fall back to placing at connection
                }
                else
                {
                    Vector3 left = (rightIntersectionPoint - leftIntersectionPoint).normalized;

                    vertices.Add(leftIntersectionPoint - Utility.ToXZ(connection.direction) * crosswalk.width + left * crosswalk.insetDistance + Vector3.up * crosswalk.yOffset - transform.position);
                    vertices.Add(leftIntersectionPoint + left * crosswalk.insetDistance + Vector3.up * crosswalk.yOffset - transform.position);
                    vertices.Add(rightIntersectionPoint - Utility.ToXZ(connection.direction) * crosswalk.width - left * crosswalk.insetDistance + Vector3.up * crosswalk.yOffset - transform.position);
                    vertices.Add(rightIntersectionPoint - left * crosswalk.insetDistance + Vector3.up * crosswalk.yOffset - transform.position);
                }
            }

            if (connectionAnchoring)
            {
                vertices.Add(connection.leftPoint + Vector3.up * crosswalk.yOffset - transform.position);
                vertices.Add(connection.leftPoint + Utility.ToXZ(connection.direction) * crosswalk.width + Vector3.up * crosswalk.yOffset - transform.position);
                vertices.Add(connection.rightPoint + Vector3.up * crosswalk.yOffset - transform.position);
                vertices.Add(connection.rightPoint + Utility.ToXZ(connection.direction) * crosswalk.width + Vector3.up * crosswalk.yOffset - transform.position);
            }

            // Triangles
            triangles = MeshUtility.AddSquare(triangles, 0, 1, 2, 3);

            // Uvs
            float width = Vector3.Distance(vertices[0], vertices[2]);
            uvs.Add(new Vector2(0, crosswalk.textureTilingOffset));
            uvs.Add(new Vector2(1, crosswalk.textureTilingOffset));
            uvs.Add(new Vector2(0, width * crosswalk.textureTilingMultiplier + crosswalk.textureTilingOffset));
            uvs.Add(new Vector2(1, width * crosswalk.textureTilingMultiplier + crosswalk.textureTilingOffset));

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            // Create object
            GameObject meshObject = new GameObject("Mesh");
            meshObject.transform.parent = transform.GetChild(2);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.AddComponent<MeshFilter>();
            meshObject.AddComponent<MeshRenderer>();
            meshObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Assign mesh
            meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            meshObject.GetComponent<MeshRenderer>().sharedMaterial = crosswalk.material;
        }

        private void PlaceTurnMarkings(int index)
        {
            if (connections[index].turnMarkingsRepetitions > 0 && connections.Count > 2)
            {
                // Get points to lerp between
                Vector3 startPoint = connections[index].roadPoint.transform.position;
                Vector3 startTangent = connections[index].roadPoint.GetComponent<Point>().GetRightLocalControlPoint();
                Vector3 endPoint = connections[index].roadPoint.transform.parent.GetChild(1).position;
                Vector3 endTangent = connections[index].roadPoint.transform.parent.GetChild(1).GetComponent<Point>().GetLeftLocalControlPoint();

                if (connections[index].endConnection)
                {
                    startPoint = connections[index].roadPoint.transform.position;
                    startTangent = connections[index].roadPoint.transform.GetComponent<Point>().GetLeftLocalControlPoint();
                    endPoint = connections[index].roadPoint.transform.parent.GetChild(connections[index].roadPoint.transform.parent.childCount - 2).position;
                    endTangent = connections[index].roadPoint.transform.parent.GetChild(connections[index].roadPoint.transform.parent.childCount - 2).GetComponent<Point>().GetRightLocalControlPoint();
                }

                // Get prefabs
                List<GameObject> prefabs = new List<GameObject>();
                for (int i = 0; i < connections[index].turnMarkingsAmount; i++)
                {
                    // Prevent errors
                    if (connections[index].turnMarkings[i].one == false && connections[index].turnMarkings[i].two == false && connections[index].turnMarkings[i].three == false)
                    {
                        connections[index].turnMarkings[i].two = true;
                    }

                    prefabs.Add((GameObject)settings.FindProperty(Utility.GetTurnMarking(connections[index].turnMarkings[i])).objectReferenceValue);
                }

                // Lerp through points
                Vector3 lastPoint = startPoint;
                float totalDistance = 0;
                int placedPrefabs = 0;

                for (float t = 0.01f; t <= 1; t += 1f / (Vector3.Distance(startPoint, endPoint) * 1.5f * 10))
                {
                    Vector3 currentPoint = Utility.Lerp4(startPoint, endPoint, startTangent, endTangent, t);
                    totalDistance += Vector2.Distance(new Vector2(currentPoint.x, currentPoint.z), new Vector2(lastPoint.x, lastPoint.z));

                    // Place prefabs
                    if (totalDistance > connections[index].turnMarkingsStartOffset && placedPrefabs == 0)
                    {
                        for (int i = 0; i < connections[index].turnMarkingsAmount; i++)
                        {
                            PlaceTurnMarking(currentPoint, lastPoint, prefabs[i], connections[index].turnMarkingsYOffset, connections[index].turnMarkingsXOffsets[0].list[i]);
                        }

                        placedPrefabs += 1;
                    }
                    else if (totalDistance > (connections[index].turnMarkingsStartOffset + connections[index].turnMarkingsContiniusOffset * placedPrefabs) && placedPrefabs > 0)
                    {
                        for (int i = 0; i < connections[index].turnMarkingsAmount; i++)
                        {
                            PlaceTurnMarking(currentPoint, lastPoint, prefabs[i], connections[index].turnMarkingsYOffset, connections[index].turnMarkingsXOffsets[0].list[i]);
                        }

                        placedPrefabs += 1;
                    }

                    if (placedPrefabs == connections[index].turnMarkingsRepetitions)
                    {
                        return;
                    }

                    lastPoint = currentPoint;
                }
            }
        }

        private void PlaceTurnMarking(Vector3 currentPoint, Vector3 lastPoint, GameObject prefab, float yOffset, float xOffset)
        {
            // Make sure that the turn marking is always visible above the road
            Vector3 forward = currentPoint - lastPoint;
            Vector3 left = new Vector3(-forward.z, 0, forward.x).normalized;//Vector3.Cross(forward, Vector3.up).normalized;
            forward = forward.normalized;

            // X-Offset
            currentPoint += left * xOffset;
            lastPoint += left * xOffset;

            float length = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.extents.z;
            float width = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x;
            Vector3 startPosition = currentPoint + forward * length;
            Vector3 endPosition = currentPoint - forward * length;
            Vector3 leftPosition = currentPoint + left * width;
            Vector3 rightPosition = currentPoint - left * width;

            RaycastHit raycastHit;
            if (Physics.Raycast(startPosition + Vector3.up * 3, Vector3.down, out raycastHit, 4, 1 << LayerMask.NameToLayer("Road")))
            {
                startPosition = raycastHit.point;
            }

            if (Physics.Raycast(endPosition + Vector3.up * 3, Vector3.down, out raycastHit, 4, 1 << LayerMask.NameToLayer("Road")))
            {
                endPosition = raycastHit.point;
            }

            if (Physics.Raycast(leftPosition + Vector3.up * 3, Vector3.down, out raycastHit, 4, 1 << LayerMask.NameToLayer("Road")))
            {
                leftPosition = raycastHit.point;
            }

            if (Physics.Raycast(rightPosition + Vector3.up * 3, Vector3.down, out raycastHit, 4, 1 << LayerMask.NameToLayer("Road")))
            {
                rightPosition = raycastHit.point;
            }

            if (Physics.Raycast(currentPoint + Vector3.up * 3, Vector3.down, out raycastHit, 4, 1 << LayerMask.NameToLayer("Road")))
            {
                currentPoint = raycastHit.point;
            }

            GameObject turnMarking = Instantiate(prefab);
            turnMarking.transform.SetParent(transform.GetChild(1));
            turnMarking.hideFlags = HideFlags.HideInHierarchy;

            // Forward/Backward
            turnMarking.transform.position = Vector3.Max((startPosition + endPosition) / 2, currentPoint) + new Vector3(0, yOffset, 0);
            // Left/Right
            turnMarking.transform.position = Vector3.Max((leftPosition + rightPosition) / 2, turnMarking.transform.position) + new Vector3(0, yOffset, 0);

            turnMarking.transform.forward = -(startPosition - endPosition).normalized;
            turnMarking.transform.rotation = Quaternion.Euler(turnMarking.transform.rotation.eulerAngles.x, turnMarking.transform.rotation.eulerAngles.y, ((leftPosition - rightPosition).y / (width * 2)) * 90);
        }

        private void AssignMesh(List<List<Vector3>> vertices, List<List<Vector2>> uvs, List<List<int>> triangles, string name, List<Material> materials, PhysicMaterial physicsMaterial)
        {
            // Create mesh object
            GameObject meshObject = new GameObject(name);
            meshObject.transform.SetParent(transform.GetChild(0));
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.hideFlags = HideFlags.NotEditable;
            Utility.AddCollidableMeshAndOtherComponents(ref meshObject, new List<System.Type> { typeof(SelectParent) });

            // First lod level is stored in LOD group object
            Mesh mesh = new Mesh();
            mesh.vertices = vertices[0].ToArray();

            // Create submeshes
            mesh.subMeshCount = materials.Count;

            for (int i = 0; i < materials.Count; i++)
            {
                mesh.SetTriangles(triangles[0].ToArray(), i);
            }

            mesh.uv = uvs[0].ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            meshObject.GetComponent<MeshCollider>().sharedMesh = mesh;
            meshObject.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            meshObject.layer = LayerMask.NameToLayer("Intersection");

            // Collider for main lod level as physics otherwise would be unaccurate
            if (generateColliders == true)
            {
                if (meshObject.GetComponent<MeshCollider>().enabled == false)
                {
                    meshObject.GetComponent<MeshCollider>().enabled = true;
                }

                meshObject.GetComponent<MeshCollider>().sharedMesh = mesh;
                meshObject.GetComponent<MeshCollider>().sharedMaterial = physicsMaterial;
            }
            else
            {
                if (meshObject.GetComponent<MeshCollider>().enabled == true)
                {
                    meshObject.GetComponent<MeshCollider>().enabled = false;
                }
            }

            // Setup LOD
            if (meshObject.GetComponent<LODGroup>() == null)
            {
                meshObject.gameObject.AddComponent<LODGroup>();
            }
            meshObject.GetComponent<LODGroup>().fadeMode = LODFadeMode.CrossFade;

            List<LOD> lods = new List<LOD>();
            // Main mesh
            float distance = 0;
            if (lodLevels > 0)
            {
                distance = 1 - lodDistances[0];
            }

            lods.Add(new LOD(distance, new MeshRenderer[] { meshObject.GetComponent<MeshRenderer>() }));

            // Add meshes
            while (lodLevels > meshObject.transform.childCount)
            {
                GameObject lodObject = new GameObject("LOD Object");
                lodObject.transform.SetParent(meshObject.transform, false);
                Utility.AddMeshAndOtherComponents(ref lodObject, new List<System.Type> { typeof(SelectParent) });
                lodObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lodObject.hideFlags = HideFlags.NotEditable;
            }

            // Remove lod meshes
            while (lodLevels < meshObject.transform.childCount)
            {
                DestroyImmediate(meshObject.transform.GetChild(0).gameObject);
            }

            // Assign lod meshes
            for (int j = 0; j < lodDistances.Count; j++)
            {
                mesh = new Mesh();
                mesh.vertices = vertices[j + 1].ToArray();

                // Create submeshes
                mesh.subMeshCount = materials.Count;

                for (int i = 0; i < materials.Count; i++)
                {
                    mesh.SetTriangles(triangles[j + 1].ToArray(), i);
                }

                mesh.uv = uvs[j + 1].ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();

                meshObject.transform.GetChild(j).GetComponent<MeshFilter>().sharedMesh = mesh;
                meshObject.transform.GetChild(j).GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                meshObject.transform.GetChild(j).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                distance = 0;
                if (j < lodLevels - 1)
                {
                    distance = 1 - lodDistances[j + 1];
                }

                lods.Add(new LOD(distance, new MeshRenderer[] { meshObject.transform.GetChild(j).GetComponent<MeshRenderer>() }));
            }

            meshObject.GetComponent<LODGroup>().SetLODs(lods.ToArray());
            meshObject.GetComponent<LODGroup>().RecalculateBounds();
            meshObject.GetComponent<LODGroup>().enabled = true;
        }

        public void Flatten()
        {
            float centerPoint = 0;

            for (int i = 0; i < connections.Count; i++)
            {
                centerPoint += connections[i].roadPoint.transform.position.y + connections[i].GetRoad().baseYOffset;
            }

            centerPoint /= connections.Count;

            for (int i = 0; i < connections.Count; i++)
            {
                Undo.RecordObject(connections[i].roadPoint.transform, "Flatten");
                Vector3 position = connections[i].roadPoint.transform.position;
                position.y = centerPoint - connections[i].GetRoad().baseYOffset;
                connections[i].roadPoint.transform.position = position;
            }

            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].GetRoad().Regenerate(false);
            }
        }
    }
}
#endif
