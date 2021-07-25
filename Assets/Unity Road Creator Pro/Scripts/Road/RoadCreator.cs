#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadCreatorPro
{
    public class RoadCreator : PointSystemCreator
    {
        // General
        public int tab = 0;
        public float baseYOffset = 0.02f;
        public bool connectToIntersections = true;
        public bool generateColliders = true;
        public bool cyclic = false;
        [SerializeReference]
        public RoadPreset roadPreset;

        // LOD
        public int lodLevels = 3;
        public List<float> lodDistances = new List<float>();
        public List<int> lodVertexDivisions = new List<int>();

        // Lanes
        public int lanesTab = 0;
        public List<Lane> lanes = new List<Lane>();

        // Terrain modification
        public int terrainTab = 0;
        public bool deformMeshToTerrain = false;
        public bool modifyTerrainHeight = true;
        public Terrain terrain;
        public float terrainRadius = 20;
        public int terrainSmoothingRadius = 1;
        public float terrainSmoothingAmount = 0.5f;
        public float terrainAngle = 45;
        public float terrainExtraMaxHeight = 5;
        public float terrainModificationYOffset = 0.2f;
        public bool modifyTerrainOnUpdate = false;

        public bool terrainRemoveDetails = true;
        public float terrainDetailsRadius = 10;
        public bool terrainRemoveDetailsOnUpdate = false;

        public bool terrainRemoveTrees = true;
        public float terrainTreesRadius = 10;
        public bool terrainRemoveTreesOnUpdate = false;

        public List<TerrainModificationInterval> terrainModificationIntervals = new List<TerrainModificationInterval>();

        // Prefab lines
        public int prefabsTab = 0;
        public List<PrefabLineCreator> prefabLines = new List<PrefabLineCreator>();

        // Internal
        PointData pointData;
        public Lane defaultLane;
        public TerrainModificationInterval defaultTerrainInterval;
        public ComputeShader detailComputeShader;
        public ComputeShader treeComputeShader;
        public bool iDown;

        public AnimationCurve laneCurve = AnimationCurve.Linear(0, 0, 1, 0);
        public Material[] laneMaterials = new Material[] { };
        public bool oneMaterialPerLane = false;

        private ComputeShader terrainSmoothShader;
        private int terrainResolution;
        private Vector3 terrainDataSize;
        private Vector3 terrainPosition;

        // Intersections
        [SerializeReference]
        public Intersection startIntersection;
        [SerializeReference]
        public Intersection endIntersection;
        [SerializeReference]
        public IntersectionConnection startIntersectionConnection;
        [SerializeReference]
        public IntersectionConnection endIntersectionConnection;

        // In progress meshes
        // Lane, lod-level, vertex
        public List<Transform> meshObjects = new List<Transform>();
        public List<List<List<Vector3>>> meshVertices = new List<List<List<Vector3>>>();
        public List<List<List<Vector2>>> meshUvs = new List<List<List<Vector2>>>();
        public List<List<List<int>>> meshTriangles = new List<List<List<int>>>();
        public List<float> meshMaxWidths = new List<float>();

        private void Start()
        {
            // Disable all colliders
            if (!generateColliders)
            {
                MeshCollider[] colliders = gameObject.GetComponentsInChildren<MeshCollider>();

                foreach (MeshCollider collider in colliders)
                {
                    collider.enabled = false;
                }
            }
        }

        public override void Regenerate(bool updateTerrain = false, bool updateDetails = false, bool updateTrees = false)
        {
            // Remove road if less that two points as it won't be selectable
            if (transform.GetChild(0).childCount < 2)
            {
                // Update intersection
                if (startIntersection != null)
                {
                    Intersection intersection = startIntersection;

                    Undo.RegisterCompleteObjectUndo(intersection, "Remove Road");
                    intersection.connections.Remove(startIntersectionConnection);

                    Undo.RegisterCompleteObjectUndo(this, "Remove Road");
                    startIntersection = null;

                    intersection.Regenerate(false);
                }

                if (endIntersection != null)
                {
                    Intersection intersection = endIntersection;

                    Undo.RegisterCompleteObjectUndo(intersection, "Remove Road");
                    intersection.connections.Remove(endIntersectionConnection);

                    Undo.RegisterCompleteObjectUndo(this, "Remove Road");
                    endIntersection = null;

                    intersection.Regenerate(false);
                }

                Undo.DestroyObjectImmediate(gameObject);
                return;
            }

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

            // Prevent updating terrain when mesh deform is enabled
            if (deformMeshToTerrain)
            {
                updateTerrain = false;
            }

            if (transform.GetChild(0).childCount > 1)
            {
                MakeRoadCyclic();

                pointData = CalculatePoints();
                int currentSegment = 0;
                int currentStartIndex = 0;
                int nextStartIndex = 0;

                if (CheckVariables(pointData))
                {
                    CreateAndRemoveMeshes();
                    SetupMeshes(pointData);

                    // Calculate current terrain
                    Terrain activeTerrain = null;
                    float[,] heightMap = null;
                    float[,] originalHeightmap = null;
                    HashSet<Vector2Int> detailHeights = new HashSet<Vector2Int>();
                    List<TreeInstance> trees = null;
                    List<Vector3> treePositions = new List<Vector3>();
                    HashSet<Vector3> treesToRemove = new HashSet<Vector3>();
                    HashSet<Vector2Int> terrainPoints = new HashSet<Vector2Int>(); // Points to smooth
                    HashSet<Vector2Int> finishedTerrainPoints = new HashSet<Vector2Int>(); // Points not to smooth
                    Dictionary<Vector2Int, float> terrainDistances = new Dictionary<Vector2Int, float>();

                    // Calculate terrain if none is specified
                    activeTerrain = terrain;
                    RaycastHit raycastHit;
                    if ((updateTerrain || updateDetails || updateTrees) && activeTerrain == null && Physics.Raycast(transform.GetChild(0).GetChild(0).position + Vector3.up * 50, Vector3.down, out raycastHit, 1000, ~(1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Prefab Line") | 1 << LayerMask.NameToLayer("Intersection"))))
                    {
                        if (raycastHit.transform.GetComponent<Terrain>() != null)
                        {
                            activeTerrain = raycastHit.transform.GetComponent<Terrain>();
                        }
                    }

                    if ((updateTerrain || updateDetails || updateTrees) && activeTerrain != null)
                    {
                        heightMap = activeTerrain.terrainData.GetHeights(0, 0, activeTerrain.terrainData.heightmapResolution, activeTerrain.terrainData.heightmapResolution);
                        originalHeightmap = (float[,])heightMap.Clone();
                        terrainResolution = activeTerrain.terrainData.heightmapResolution - 1;
                        terrainDataSize = activeTerrain.terrainData.size;
                        terrainPosition = activeTerrain.transform.position;

                        if (terrainRemoveTrees && updateTrees)
                        {
                            trees = new List<TreeInstance>(activeTerrain.terrainData.treeInstances);

                            for (int i = 0; i < trees.Count; i++)
                            {
                                Vector3 position = Vector3.Scale(trees[i].position, activeTerrain.terrainData.size) + activeTerrain.transform.position;
                                treePositions.Add(position);
                            }
                        }
                    }

                    float[] widths = new float[pointData.positions.Count];
                    float[] extraWidthsRight = new float[pointData.positions.Count];
                    float[] distances = new float[pointData.positions.Count];

                    // Calculate distances
                    for (int i = 0; i < pointData.positions.Count; i++)
                    {
                        if (i == 0)
                        {
                            distances[i] = 0;
                        }
                        else
                        {
                            // Distance from start not distance between points
                            if (i > 1)
                            {
                                distances[i] = distances[i - 1];
                            }

                            distances[i] += Vector3.Distance(pointData.positions[i - 1], pointData.positions[i]);
                        }
                    }

                    // Generate widths so that the road can be centered instead of to right of the curve
                    // Calculate the index of the point where the next segment starts
                    if (pointData.segmentStartIndexes.Count == 1)
                    {
                        nextStartIndex = pointData.positions.Count - 1;
                    }
                    else
                    {
                        nextStartIndex = pointData.segmentStartIndexes[1];
                    }

                    for (int i = 0; i < pointData.positions.Count; i++)
                    {
                        if (currentSegment != pointData.segmentStartIndexes.Count - 1 && i > pointData.segmentStartIndexes[currentSegment + 1])
                        {
                            currentSegment += 1;
                            currentStartIndex = pointData.segmentStartIndexes[currentSegment];

                            // Calculate the index of the point where the next segment starts
                            if (currentSegment < transform.GetChild(0).childCount - 2)
                            {
                                nextStartIndex = pointData.segmentStartIndexes[currentSegment + 1];
                            }
                            else
                            {
                                nextStartIndex = pointData.positions.Count - 1;
                            }
                        }

                        float indexProgress = (i - currentStartIndex) / ((float)(nextStartIndex - currentStartIndex));
                        for (int j = 0; j < lanes.Count; j++)
                        {
                            bool insideStart = (currentSegment > lanes[j].startIndex || currentSegment == lanes[j].startIndex && indexProgress >= lanes[j].startPercentageOffset);
                            bool insideEnd = (currentSegment < lanes[j].endIndex || currentSegment == lanes[j].endIndex && indexProgress <= lanes[j].endPercentageOffset);

                            if (insideStart && insideEnd)
                            {
                                // Make sure that lanes have the right ignore width value
                                if (lanes[j].ignoreForWidthCalculation && j < lanes.Count - 1 && !lanes[j + 1].ignoreForWidthCalculation)
                                {
                                    lanes[j].ignoreForWidthCalculation = false;
                                }

                                // Prevent error with road of length 0
                                if (lanes[j].startIndex == lanes[j].endIndex && lanes[j].startPercentageOffset == lanes[j].endPercentageOffset)
                                {
                                    continue;
                                }

                                // Calculate current start and end segment indexes
                                int lastStartIndex = pointData.positions.Count - 1;
                                int lastEndIndex = pointData.positions.Count - 1;

                                if (lanes[j].startIndex < pointData.segmentStartIndexes.Count - 1)
                                {
                                    lastStartIndex = pointData.segmentStartIndexes[lanes[j].startIndex + 1];
                                }

                                if (lanes[j].endIndex < pointData.segmentStartIndexes.Count - 1)
                                {
                                    lastEndIndex = pointData.segmentStartIndexes[lanes[j].endIndex + 1];
                                }

                                // Calculate current progress in interval
                                int startIndex = Mathf.CeilToInt(lanes[j].startPercentageOffset * (lastStartIndex - pointData.segmentStartIndexes[lanes[j].startIndex]));
                                if (lanes[j].startIndex == currentSegment && lanes[j].startPercentageOffset == 0)
                                {
                                    startIndex += 1;
                                }

                                int endIndex = Mathf.FloorToInt(lanes[j].endPercentageOffset * (lastEndIndex - 1 - pointData.segmentStartIndexes[lanes[j].endIndex]));
                                // (current distance - start distance) / (end distance - start distance)
                                float progress = (distances[i] - distances[pointData.segmentStartIndexes[lanes[j].startIndex] + startIndex]) / (distances[pointData.segmentStartIndexes[lanes[j].endIndex] + endIndex] - distances[pointData.segmentStartIndexes[lanes[j].startIndex] + startIndex]);

                                // Ignore lanes that shouldn't count
                                if (lanes[j].ignoreForWidthCalculation)
                                {
                                    extraWidthsRight[i] += lanes[j].width.Evaluate(progress);
                                    continue;
                                }

                                // Make sure that the start of the road connects to the right side of gap vertices
                                if (progress == 0 && i < pointData.positions.Count - 1 && pointData.positions[i] == pointData.positions[i + 1])
                                {
                                    continue;
                                }

                                // Make sure that the end of the road connects to the right side of gap vertices
                                if (progress == 1 && i > 1 && pointData.positions[i] == pointData.positions[i - 1])
                                {
                                    continue;
                                }

                                // Prevent meshes with no size
                                if (!float.IsNaN(progress))
                                {
                                    widths[i] += lanes[j].width.Evaluate(progress);
                                }
                            }
                        }
                    }

                    currentSegment = 0;
                    currentStartIndex = 0;

                    // Calculate the index of the point where the next segment starts
                    if (pointData.segmentStartIndexes.Count == 1)
                    {
                        nextStartIndex = pointData.positions.Count - 1;
                    }
                    else
                    {
                        nextStartIndex = pointData.segmentStartIndexes[1];
                    }

                    List<Vector3> lastLeftPoints = new List<Vector3>();
                    List<Vector3> lastRightPoints = new List<Vector3>();

                    for (int i = 0; i < lanes.Count; i++)
                    {
                        lastLeftPoints.Add(Utility.MaxVector3);
                        lastRightPoints.Add(Utility.MaxVector3);
                    }

                    // Used for terrain modification
                    Vector3 lastleftPoint = Vector3.zero;
                    Vector3 lastRightPoint = Vector3.zero;

                    for (int i = 0; i < pointData.positions.Count; i++)
                    {
                        // Calculate forward direction
                        Vector3 forward = Vector3.zero;
                        Vector3 left = Vector3.zero;

                        if (i == 0)
                        {
                            forward = (pointData.positions[1] - pointData.positions[0]);
                            left = Utility.CalculateLeft(forward);
                            forward = forward.normalized;
                        }
                        else
                        {
                            if (i > 1 && Utility.AlmostEqual(pointData.positions[i], pointData.positions[i - 1], 0.001f))
                            {
                                forward = (pointData.positions[i] - pointData.positions[i - 2]);
                                left = Utility.CalculateLeft(forward);
                                forward = forward.normalized;
                            }
                            else
                            {
                                forward = (pointData.positions[i] - pointData.positions[i - 1]);

                                // Align to tangent at last point, mostly to make cyclic roads work
                                if (i == pointData.positions.Count - 1 && currentSegment == transform.GetChild(0).childCount - 2)
                                {
                                    forward = -transform.GetChild(0).GetChild(currentSegment + 1).GetComponent<Point>().leftLocalControlPointPosition;
                                }

                                left = Utility.CalculateLeft(forward);
                                forward = forward.normalized;
                            }
                        }

                        // Prevent strange results
                        if (left.magnitude == 0)
                        {
                            continue;
                        }

                        // Adapt mesh to terrain
                        if (deformMeshToTerrain)
                        {
                            // Cast one ray to the left and one to the right and take the highest value
                            float terrainY = pointData.positions[i].y;
                            if (Physics.Raycast(new Ray(pointData.positions[i] + left * widths[i] / 2 + Vector3.up * 50, Vector3.down), out raycastHit, 100, ~(1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection") | 1 << LayerMask.NameToLayer("Prefab Line"))))
                            {
                                terrainY = raycastHit.point.y + 0.1f; // Offset a bit for better default value
                            }

                            if (Physics.Raycast(new Ray(pointData.positions[i] - left * widths[i] / 2 + Vector3.up * 50, Vector3.down), out raycastHit, 100, ~(1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection") | 1 << LayerMask.NameToLayer("Prefab Line"))))
                            {
                                terrainY = Mathf.Max(terrainY, raycastHit.point.y + 0.1f); // Offset a bit for better default value
                            }

                            pointData.positions[i] = new Vector3(pointData.positions[i].x, terrainY, pointData.positions[i].z);
                        }

                        if (currentSegment != pointData.segmentStartIndexes.Count - 1 && i > pointData.segmentStartIndexes[currentSegment + 1])
                        {
                            currentSegment += 1;
                            currentStartIndex = pointData.segmentStartIndexes[currentSegment];

                            // Calculate the index of the point where the next segment starts
                            if (currentSegment < transform.GetChild(0).childCount - 2)
                            {
                                nextStartIndex = pointData.segmentStartIndexes[currentSegment + 1];
                            }
                            else
                            {
                                nextStartIndex = pointData.positions.Count - 1;
                            }
                        }

                        // Generate meshes for appropriate lanes
                        float currentWidthOffset = 0;
                        float currentYOffset = baseYOffset;
                        float lastYOffset = 0;

                        float indexProgress = (i - currentStartIndex) / ((float)(nextStartIndex - currentStartIndex));
                        for (int j = 0; j < lanes.Count; j++)
                        {
                            bool insideStart = (currentSegment > lanes[j].startIndex || (currentSegment == lanes[j].startIndex && indexProgress >= lanes[j].startPercentageOffset));
                            bool insideEnd = (currentSegment < lanes[j].endIndex || (currentSegment == lanes[j].endIndex && indexProgress <= lanes[j].endPercentageOffset));

                            if (insideStart && insideEnd)
                            {
                                // Prevent error with road of length 0
                                if (lanes[j].startIndex == lanes[j].endIndex && lanes[j].startPercentageOffset == lanes[j].endPercentageOffset)
                                {
                                    continue;
                                }

                                // Prevent generating mesh that is infinitly small
                                if (lanes[j].width.keys[0].value == 0 && lanes[j].width.keys[1].value == 0 && lanes[j].yOffset.keys[0].value == 0 && lanes[j].yOffset.keys[1].value == 0)
                                {
                                    continue;
                                }

                                float progress = 0;

                                // Calculate current start and end segment indexes
                                int lastStartIndex = pointData.positions.Count - 1;
                                int lastEndIndex = pointData.positions.Count - 1;

                                if (lanes[j].startIndex < pointData.segmentStartIndexes.Count - 1)
                                {
                                    lastStartIndex = pointData.segmentStartIndexes[lanes[j].startIndex + 1];
                                }

                                if (lanes[j].endIndex < pointData.segmentStartIndexes.Count - 1)
                                {
                                    lastEndIndex = pointData.segmentStartIndexes[lanes[j].endIndex + 1];
                                }

                                // Calculate current progress in interval
                                int startIndex = Mathf.CeilToInt(lanes[j].startPercentageOffset * (lastStartIndex - pointData.segmentStartIndexes[lanes[j].startIndex]));
                                if (lanes[j].startIndex == currentSegment && lanes[j].startPercentageOffset == 0)
                                {
                                    startIndex += 1;
                                }

                                int endIndex = Mathf.FloorToInt(lanes[j].endPercentageOffset * (lastEndIndex - pointData.segmentStartIndexes[lanes[j].endIndex]));
                                // (current distance - start distance) / (end distance - start distance)
                                progress = (distances[i] - distances[pointData.segmentStartIndexes[lanes[j].startIndex] + startIndex]) / (distances[pointData.segmentStartIndexes[lanes[j].endIndex] + endIndex] - distances[pointData.segmentStartIndexes[lanes[j].startIndex] + startIndex]);

                                // Make sure that the start of the road connects to the right side of gap vertices
                                if (progress == 0 && i < pointData.positions.Count - 1 && pointData.positions[i] == pointData.positions[i + 1])
                                {
                                    continue;
                                }

                                // Make sure that the end of the road connects to the right side of gap vertices
                                if (progress == 1 && i > 1 && pointData.positions[i] == pointData.positions[i - 1])
                                {
                                    continue;
                                }

                                // Prevent meshes with no size
                                if (!float.IsNaN(progress))
                                {
                                    Vector3 nextPoint = pointData.positions[pointData.positions.Count - 1];
                                    if (i < pointData.positions.Count - 1)
                                    {
                                        nextPoint = pointData.positions[i + 1];
                                    }

                                    Vector3 lastPoint = pointData.positions[0];
                                    if (i > 0)
                                    {
                                        lastPoint = pointData.positions[i - 1];
                                    }

                                    GenerateMesh(j, pointData.positions[i], lastPoint, nextPoint, forward, distances[i], lanes[j].width.Evaluate(progress), currentWidthOffset - widths[i] / 2, currentYOffset, currentYOffset + lanes[j].yOffset.Evaluate(progress), progress, i, ref lastLeftPoints, ref lastRightPoints);
                                    currentWidthOffset += lanes[j].width.Evaluate(progress);
                                    currentYOffset += lanes[j].yOffset.Evaluate(progress);
                                }

                                // Calculate variables for terrain deformation
                                lastYOffset = currentYOffset;
                            }
                        }

                        // Remove details and trees
                        if (activeTerrain != null)
                        {
                            if (terrainRemoveDetails && updateDetails)
                            {
                                Utility.RemoveTerrainDetails(activeTerrain, ref detailHeights, forward, left, ref detailComputeShader, terrainDetailsRadius, pointData.positions[i]);
                            }

                            if (terrainRemoveTrees && updateTrees)
                            {
                                Utility.RemoveTerrainTrees(activeTerrain, ref treePositions, ref treesToRemove, ref treeComputeShader, terrainTreesRadius, pointData.positions[i]);
                            }
                        }

                        // Deform terrain on sides
                        if (modifyTerrainHeight && updateTerrain)
                        {
                            for (int j = 0; j < terrainModificationIntervals.Count; j++)
                            {
                                // Only modify between start and end point
                                bool insideStart = (currentSegment > terrainModificationIntervals[j].startIndex || (currentSegment == terrainModificationIntervals[j].startIndex && indexProgress >= terrainModificationIntervals[j].startPercentageOffset));
                                bool insideEnd = (currentSegment < terrainModificationIntervals[j].endIndex || (currentSegment == terrainModificationIntervals[j].endIndex && indexProgress <= terrainModificationIntervals[j].endPercentageOffset));

                                if (insideStart && insideEnd)
                                {
                                    if (activeTerrain != null)
                                    {
                                        // Widths array does not contain extra widths
                                        float widthLeft = widths[i] / 2 + terrainExtraMaxHeight;
                                        float widthRight = widths[i] / 2 + extraWidthsRight[i] + terrainExtraMaxHeight;
                                        Vector3 leftPoint = pointData.positions[i] + left * (widthLeft + terrainRadius) - new Vector3(0, baseYOffset, 0);
                                        Vector3 rightPoint = pointData.positions[i] - left * (widthRight + terrainRadius) - new Vector3(0, baseYOffset - lastYOffset, 0);

                                        // First point
                                        if (lastleftPoint == Vector3.zero)
                                        {
                                            lastleftPoint = leftPoint;
                                            lastRightPoint = rightPoint;
                                        }

                                        float distanceToLast = Mathf.Min(Vector3.Distance(lastleftPoint, leftPoint), Vector3.Distance(lastRightPoint, rightPoint));
                                        float worldUnitPerTerrainUnit = 1f / terrainResolution * terrainDataSize.x; // How many world unit does one terrain unit represent
                                        float terrainUnitsBetween = distanceToLast / worldUnitPerTerrainUnit;

                                        for (float t = 0; t < 1; t += 1 / terrainUnitsBetween)
                                        {
                                            Utility.AdjustTerrain(Vector3.Lerp(pointData.positions[Mathf.Max(0, i - 1)], pointData.positions[i], t), left, Mathf.Max((int)(widthLeft / worldUnitPerTerrainUnit), (int)(widthRight / worldUnitPerTerrainUnit)), activeTerrain,
                                                terrainRadius, terrainDataSize, terrainResolution, terrainPosition, terrainAngle, terrainExtraMaxHeight, terrainModificationYOffset, originalHeightmap, ref heightMap, ref terrainPoints, ref finishedTerrainPoints, ref terrainDistances);
                                        }

                                        lastleftPoint = leftPoint;
                                        lastRightPoint = rightPoint;
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    AssignMeshes();
                    if (activeTerrain != null && (updateTerrain || updateDetails || updateTrees))
                    {
                        Undo.RegisterCompleteObjectUndo(activeTerrain.terrainData, "Modify Terrain");

                        // Smooth out terrain
                        Utility.SmoothTerrain(terrainSmoothShader, terrainResolution, terrainSmoothingRadius, terrainSmoothingAmount, ref heightMap, terrainPoints);

                        // Fill heightmap
                        if (modifyTerrainHeight && updateTerrain)
                        {
                            activeTerrain.terrainData.SetHeights(0, 0, heightMap);
                        }

                        // Remove details
                        if (terrainRemoveDetails && updateDetails)
                        {
                            for (int i = 0; i < activeTerrain.terrainData.detailPrototypes.Length; i++)
                            {
                                int[,] detailMap = activeTerrain.terrainData.GetDetailLayer(0, 0, activeTerrain.terrainData.detailWidth, activeTerrain.terrainData.detailHeight, i);

                                foreach (Vector2Int pair in detailHeights)
                                {
                                    detailMap[pair.y, pair.x] = 0;
                                }

                                activeTerrain.terrainData.SetDetailLayer(0, 0, i, detailMap);
                            }
                        }

                        // Remove trees
                        if (terrainRemoveTrees && updateTrees)
                        {
                            for (int i = trees.Count - 1; i >= 0; i--)
                            {
                                if (treesToRemove.Contains(Vector3.Scale(trees[i].position, activeTerrain.terrainData.size) + activeTerrain.transform.position))
                                {
                                    trees.RemoveAt(i);
                                }
                            }

                            activeTerrain.terrainData.SetTreeInstances(trees.ToArray(), false);
                        }

                        activeTerrain.Flush();
                    }

                    if (startIntersection != null)
                    {
                        for (int i = 0; i < startIntersection.connections.Count; i++)
                        {
                            RoadCreator otherRoad = startIntersection.connections[i].GetRoad();

                            // This roads start intersection could be other's end intersection
                            if (startIntersection == otherRoad.startIntersection)
                            {
                                otherRoad.GenerateIntersectionConnectionIndexes(otherRoad.startIntersection, ref otherRoad.startIntersectionConnection, 0, true);
                            }
                            else
                            {
                                otherRoad.GenerateIntersectionConnectionIndexes(otherRoad.endIntersection, ref otherRoad.endIntersectionConnection, otherRoad.transform.GetChild(0).childCount - 2, false);
                            }
                        }

                        startIntersection.Regenerate(false, false);
                    }

                    if (endIntersection != null)
                    {
                        for (int i = 0; i < endIntersection.connections.Count; i++)
                        {
                            RoadCreator otherRoad = endIntersection.connections[i].GetRoad();

                            // This roads start intersection could be other's end intersection
                            if (endIntersection == otherRoad.endIntersection)
                            {
                                otherRoad.GenerateIntersectionConnectionIndexes(otherRoad.endIntersection, ref otherRoad.endIntersectionConnection, otherRoad.transform.GetChild(0).childCount - 2, false);
                            }
                            else
                            {
                                otherRoad.GenerateIntersectionConnectionIndexes(otherRoad.startIntersection, ref otherRoad.startIntersectionConnection, 0, true);
                            }
                        }

                        endIntersection.Regenerate(false, false);
                    }

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
            else
            {
                // Reset meshes
                for (int i = 0; i < transform.GetChild(1).childCount; i++)
                {
                    Transform child = transform.GetChild(1).GetChild(i);
                    child.GetComponent<MeshFilter>().sharedMesh = null;
                    child.GetComponent<MeshCollider>().sharedMesh = null;

                    // Reset LODs
                    for (int j = 0; j < child.childCount; j++)
                    {
                        child.GetChild(j).GetComponent<MeshFilter>().sharedMesh = null;
                    }
                }
            }

            GeneratePrefabLines();
        }

        public void MakeRoadCyclic()
        {
            if (cyclic)
            {
                // Prevent cyclic when connectd to intersections
                if (startIntersection != null || endIntersection != null)
                {
                    cyclic = false;
                }

                // Move first point to last point
                transform.GetChild(0).GetChild(0).transform.position = transform.GetChild(0).GetChild(transform.GetChild(0).childCount - 1).transform.position;
            }
        }

        public void GenerateIntersectionConnectionIndexes(Intersection intersection, ref IntersectionConnection intersectionConnection, int index, bool start)
        {
            if (intersection != null)
            {
                intersectionConnection.startIndex = -1;
                intersectionConnection.endIndex = -1;
                int offset = 0;

                // Start and end indexes are the indexes of the connected lanes, not the total lanes
                // Find first connected end lane
                for (int i = 0; i < lanes.Count; i++)
                {
                    Lane lane = lanes[i];

                    // Start
                    if (start)
                    {
                        if (lane.startIndex > index || (lane.startIndex == index && lane.startPercentageOffset > 0))
                        {
                            offset++;
                            continue;
                        }
                    }
                    else
                    {
                        if (lane.endIndex < index || (lane.endIndex == index && lane.endPercentageOffset < 1))
                        {
                            offset++;
                            continue;
                        }
                    }

                    if (lane.mainRoadPart)
                    {
                        intersectionConnection.startIndex = i - offset;
                        break;
                    }
                }

                offset = 0;
                // Find last connected end lane
                for (int i = 0; i < lanes.Count; i++)
                {
                    Lane lane = lanes[i];

                    if (start)
                    {
                        if (lane.startIndex > index || (lane.startIndex == index && lane.startPercentageOffset > 0))
                        {
                            offset++;
                            continue;
                        }
                    }
                    else
                    {
                        if (lane.endIndex < index || (lane.endIndex == index && lane.endPercentageOffset < 1))
                        {
                            offset++;
                            continue;
                        }
                    }

                    if (lane.mainRoadPart)
                    {
                        intersectionConnection.endIndex = i - offset;
                    }
                }

                // Check if no indexes were finded
                if (intersectionConnection.startIndex == -1 && intersectionConnection.endIndex == -1)
                {
                    Debug.Log("Road does not have any main road that connect. Either no lanes have the main road part toggle enabled or all lanes end before the intersection");
                    // Prevent errors
                    intersectionConnection.startIndex = 0;
                    intersectionConnection.endIndex = 0;
                }
            }
        }

        private void GeneratePrefabLines()
        {
            for (int i = 0; i < prefabLines.Count; i++)
            {
                PrefabLineCreator prefabLine = prefabLines[i];
                int startIndex = prefabLine.startIndex;
                int endIndex = prefabLine.endIndex;

                if (prefabLine.wholeRoad)
                {
                    startIndex = 0;
                    endIndex = transform.GetChild(0).childCount - 2;
                }

                int points = endIndex - startIndex + 2;

                if (prefabLine.wholeRoad)
                {
                    points = transform.GetChild(0).childCount;
                }

                if (transform.GetChild(0).childCount < 2)
                {
                    points = 0;
                }

                if (prefabLine.transform.GetChild(0).childCount > points)
                {
                    // Remove points
                    for (int j = prefabLine.transform.GetChild(0).childCount; j > points; j--)
                    {
                        DestroyImmediate(prefabLine.transform.GetChild(0).GetChild(0).gameObject);
                    }
                }
                else if (points > prefabLine.transform.GetChild(0).childCount)
                {
                    for (int j = prefabLine.transform.GetChild(0).childCount; j < points; j++)
                    {
                        // Add points
                        GameObject point = new GameObject("Point");
                        point.AddComponent<Point>();
                        point.transform.SetParent(prefabLine.transform.GetChild(0));
                        point.hideFlags = HideFlags.NotEditable;
                    }
                }

                // Move points
                for (int j = 0; j < points; j++)
                {
                    Transform point = prefabLine.transform.GetChild(0).GetChild(j);
                    Transform roadPoint = transform.GetChild(0).GetChild(startIndex + j);
                    point.transform.position = roadPoint.position;
                    point.GetComponent<Point>().leftLocalControlPointPosition = roadPoint.GetComponent<Point>().leftLocalControlPointPosition;
                    point.GetComponent<Point>().rightLocalControlPointPosition = roadPoint.GetComponent<Point>().rightLocalControlPointPosition;
                }

                prefabLine.GetComponent<PrefabLineCreator>().Regenerate(false);
            }
        }

        private bool CheckVariables(PointData pointData)
        {
            // No terrain modification intervals
            if (modifyTerrainHeight && terrainModificationIntervals.Count == 0)
            {
                terrainModificationIntervals.Add(new TerrainModificationInterval());
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

            // No lanes
            if (lanes.Count == 0)
            {
                // Left
                Lane lane = new Lane();
                lane.materials = new List<Material>();
                lane.materials.Add(Resources.Load("Materials/Asphalt") as Material);
                lane.materials.Add(Resources.Load("Materials/Road/Lane Edge") as Material);
                lanes.Add(lane);

                // Right
                lane = new Lane();
                lane.flipUvs = true;
                lane.materials = new List<Material>();
                lane.materials.Add(Resources.Load("Materials/Asphalt") as Material);
                lane.materials.Add(Resources.Load("Materials/Road/Lane Edge") as Material);
                lanes.Add(lane);
                return false;
            }

            // Clamp offsets so that they are not outside segments
            for (int i = 0; i < lanes.Count; i++)
            {
                // Materials
                if (lanes[i].materials == null || lanes[i].materials.Count == 0)
                {
                    List<Material> materials = new List<Material>();
                    for (int j = 0; j < settings.FindProperty("defaultLaneMaterials").arraySize; j++)
                    {
                        materials.Add((Material)settings.FindProperty("defaultLaneMaterials").GetArrayElementAtIndex(j).objectReferenceValue);
                    }

                    lanes[i].materials = materials;
                    Regenerate(false);
                    return false;
                }

                // Reset center point
                lanes[i].centerPoint = Utility.MaxVector3;

                // Update indexes if whole road
                if (lanes[i].wholeRoad)
                {
                    lanes[i].startIndex = 0;
                    lanes[i].endIndex = transform.GetChild(0).childCount - 2;
                }
            }

            // Update indexes if whole road
            for (int i = 0; i < terrainModificationIntervals.Count; i++)
            {
                if (terrainModificationIntervals[i].wholeRoad)
                {
                    terrainModificationIntervals[i].startIndex = 0;
                    terrainModificationIntervals[i].endIndex = transform.GetChild(0).childCount - 2;
                }
            }

            for (int i = 0; i < prefabLines.Count; i++)
            {
                if (prefabLines[i].wholeRoad)
                {
                    prefabLines[i].startIndex = 0;
                    prefabLines[i].endIndex = transform.GetChild(0).childCount - 2;
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

            // Fix materials
            if (laneMaterials.Length == 0)
            {
                laneMaterials = new Material[] { Resources.Load("Materials/Concrete") as Material };
            }

            return true;
        }

        private void CreateAndRemoveMeshes()
        {
            while (transform.GetChild(1).childCount > lanes.Count)
            {
                // Remove mesh
                DestroyImmediate(transform.GetChild(1).GetChild(transform.GetChild(1).childCount - 1).gameObject);
            }

            while (transform.GetChild(1).childCount < lanes.Count)
            {
                // Create mesh
                GameObject mesh = new GameObject("Mesh");
                mesh.transform.SetParent(transform.GetChild(1), false);
                mesh.hideFlags = HideFlags.NotEditable;
                Utility.AddCollidableMeshAndOtherComponents(ref mesh, new List<System.Type> { typeof(SelectParent) });
                mesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mesh.layer = LayerMask.NameToLayer("Road");
            }
        }

        private void SetupMeshes(PointData pointData)
        {
            meshObjects.Clear();
            meshVertices.Clear();
            meshTriangles.Clear();
            meshUvs.Clear();
            meshMaxWidths.Clear();

            for (int i = 0; i < lanes.Count; i++)
            {
                Transform mesh = transform.GetChild(1).GetChild(i);
                mesh.position = transform.GetChild(0).GetChild(lanes[i].startIndex).position;

                meshObjects.Add(mesh);
                meshVertices.Add(new List<List<Vector3>>());
                meshTriangles.Add(new List<List<int>>());
                meshUvs.Add(new List<List<Vector2>>());

                for (int j = 0; j < lodLevels + 1; j++)
                {
                    meshVertices[i].Add(new List<Vector3>());
                    meshTriangles[i].Add(new List<int>());
                    meshUvs[i].Add(new List<Vector2>());
                }

                meshMaxWidths.Add(Utility.GetMaxValue(lanes[i].width));

                // Setup materials
                mesh.GetComponent<MeshRenderer>().sharedMaterials = lanes[i].materials.ToArray();
            }
        }

        private void AssignMeshes()
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                // First lod level is stored in LOD group object
                Mesh mesh = new Mesh();
                mesh.vertices = meshVertices[i][0].ToArray();

                // Create submeshes
                mesh.subMeshCount = lanes[i].materials.Count;

                for (int h = 0; h < lanes[i].materials.Count; h++)
                {
                    mesh.SetTriangles(meshTriangles[i][0].ToArray(), h);
                }

                mesh.uv = meshUvs[i][0].ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();

                transform.GetChild(1).GetChild(i).GetComponent<MeshFilter>().sharedMesh = mesh;

                // Collider for main lod level as physics otherwise would be unaccurate
                transform.GetChild(1).GetChild(i).GetComponent<MeshCollider>().sharedMesh = mesh;
                transform.GetChild(1).GetChild(i).GetComponent<MeshCollider>().sharedMaterial = lanes[i].physicMaterial;

                // Setup LOD
                if (transform.GetChild(1).GetChild(i).GetComponent<LODGroup>() == null)
                {
                    transform.GetChild(1).GetChild(i).gameObject.AddComponent<LODGroup>();
                }
                transform.GetChild(1).GetChild(i).GetComponent<LODGroup>().fadeMode = LODFadeMode.CrossFade;

                List<LOD> lods = new List<LOD>();
                // Main mesh
                float distance = 0;
                if (lodLevels > 0)
                {
                    distance = 1 - lodDistances[0];
                }

                lods.Add(new LOD(distance, new MeshRenderer[] { transform.GetChild(1).GetChild(i).GetComponent<MeshRenderer>() }));

                // Add meshes
                while (lodLevels > transform.GetChild(1).GetChild(i).childCount)
                {
                    GameObject lodObject = new GameObject("LOD Object");
                    lodObject.transform.SetParent(transform.GetChild(1).GetChild(i), false);
                    Utility.AddMeshAndOtherComponents(ref lodObject, new List<System.Type> { typeof(SelectParent) });
                    lodObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lodObject.hideFlags = HideFlags.NotEditable;
                }

                // Remove lod meshes
                while (lodLevels < transform.GetChild(1).GetChild(i).childCount)
                {
                    DestroyImmediate(transform.GetChild(1).GetChild(i).GetChild(0).gameObject);
                }

                // Assign lod meshes
                for (int j = 0; j < lodDistances.Count; j++)
                {
                    mesh = new Mesh();
                    mesh.vertices = meshVertices[i][j + 1].ToArray();

                    // Create submeshes
                    mesh.subMeshCount = lanes[i].materials.Count;

                    for (int h = 0; h < lanes[i].materials.Count; h++)
                    {
                        mesh.SetTriangles(meshTriangles[i][j + 1].ToArray(), h);
                    }

                    mesh.uv = meshUvs[i][j + 1].ToArray();
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();

                    transform.GetChild(1).GetChild(i).GetChild(j).GetComponent<MeshFilter>().sharedMesh = mesh;
                    transform.GetChild(1).GetChild(i).GetChild(j).GetComponent<MeshRenderer>().sharedMaterials = lanes[i].materials.ToArray();

                    distance = 0;
                    if (j < lodLevels - 1)
                    {
                        distance = 1 - lodDistances[j + 1];
                    }

                    lods.Add(new LOD(distance, new MeshRenderer[] { transform.GetChild(1).GetChild(i).GetChild(j).GetComponent<MeshRenderer>() }));
                }

                transform.GetChild(1).GetChild(i).GetComponent<LODGroup>().SetLODs(lods.ToArray());
                transform.GetChild(1).GetChild(i).GetComponent<LODGroup>().RecalculateBounds();
                transform.GetChild(1).GetChild(i).GetComponent<LODGroup>().enabled = true;
            }
        }

        private void GenerateMesh(int laneIndex, Vector3 point, Vector3 lastPoint, Vector3 nextPoint, Vector3 forward, float currentDistance, float currentWidth, float currentWidthOffset, float previousYOffset, float currentYOffset, float progress, int pointIndex, ref List<Vector3> lastLeftPoints, ref List<Vector3> lastRightPoints)
        {
            // Main lod level
            Vector3 left = Utility.CalculateLeft(forward);
            meshVertices[laneIndex][0].Add(point - left * (currentWidthOffset) + new Vector3(0, previousYOffset, 0) - transform.GetChild(0).GetChild(lanes[laneIndex].startIndex).position);
            meshVertices[laneIndex][0].Add(point - left * (currentWidth + currentWidthOffset) + new Vector3(0, currentYOffset, 0) - transform.GetChild(0).GetChild(lanes[laneIndex].startIndex).position);

            // Set center point
            if (progress >= 0.5f && lanes[laneIndex].centerPoint == Utility.MaxVector3)
            {
                lanes[laneIndex].centerPoint = (meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 1] + meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 2]) / 2 + transform.GetChild(0).GetChild(lanes[laneIndex].startIndex).position;
            }

            if (lanes[laneIndex].flipUvs == true)
            {
                meshUvs[laneIndex][0].Add(new Vector2(lanes[laneIndex].uvXMin, currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
                if (lanes[laneIndex].constantUvWidth)
                {
                    meshUvs[laneIndex][0].Add(new Vector2(currentWidth / meshMaxWidths[laneIndex], currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
                }
                else
                {
                    meshUvs[laneIndex][0].Add(new Vector2(lanes[laneIndex].uvXMax, currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
                }
            }
            else
            {
                if (lanes[laneIndex].constantUvWidth)
                {
                    meshUvs[laneIndex][0].Add(new Vector2(currentWidth / meshMaxWidths[laneIndex], currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
                }
                else
                {
                    meshUvs[laneIndex][0].Add(new Vector2(lanes[laneIndex].uvXMax, currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
                }

                meshUvs[laneIndex][0].Add(new Vector2(lanes[laneIndex].uvXMin, currentDistance * lanes[laneIndex].textureTilingMultiplier * 1 / 3));
            }

            if (meshVertices[laneIndex][0].Count > 2)
            {
                // Add triangles
                meshTriangles[laneIndex][0] = MeshUtility.AddSquare(meshTriangles[laneIndex][0], meshVertices[laneIndex][0].Count - 4,
                    meshVertices[laneIndex][0].Count - 2, meshVertices[laneIndex][0].Count - 3, meshVertices[laneIndex][0].Count - 1);
            }

            lastLeftPoints[laneIndex] = meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 2] + transform.GetChild(0).GetChild(lanes[laneIndex].startIndex).position;
            lastRightPoints[laneIndex] = meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 1] + transform.GetChild(0).GetChild(lanes[laneIndex].startIndex).position;

            // Add to other lod levels
            for (int i = 0; i < lodLevels; i++)
            {
                // Always generate first last and lane intersection vertices
                if (progress == 0 || progress == 1 || pointIndex % lodVertexDivisions[i] == 0 || point == lastPoint || point == nextPoint)
                {
                    // Vertices
                    meshVertices[laneIndex][i + 1].Add(meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 2]);
                    meshVertices[laneIndex][i + 1].Add(meshVertices[laneIndex][0][meshVertices[laneIndex][0].Count - 1]);

                    // Uvs
                    meshUvs[laneIndex][i + 1].Add(meshUvs[laneIndex][0][meshUvs[laneIndex][0].Count - 2]);
                    meshUvs[laneIndex][i + 1].Add(meshUvs[laneIndex][0][meshUvs[laneIndex][0].Count - 1]);

                    // Triangles
                    if (meshVertices[laneIndex][i + 1].Count > 2)
                    {
                        meshTriangles[laneIndex][i + 1] = MeshUtility.AddSquare(meshTriangles[laneIndex][i + 1], meshVertices[laneIndex][i + 1].Count - 1, meshVertices[laneIndex][i + 1].Count - 3,
                            meshVertices[laneIndex][i + 1].Count - 2, meshVertices[laneIndex][i + 1].Count - 4);
                    }
                }
            }
        }

        public PointData GetPointData()
        {
            if (pointData == null)
            {
                pointData = CalculatePoints();
            }

            return pointData;
        }

        public override void InitializeSystem()
        {
            if (settings == null)
            {
                settings = RoadCreatorSettings.GetSerializedSettings();
            }

            if (transform.childCount == 0)
            {
                GameObject points = new GameObject("Points");
                points.transform.SetParent(transform, false);
                points.hideFlags = HideFlags.HideInHierarchy;

                GameObject meshes = new GameObject("Meshes");
                meshes.transform.SetParent(transform, false);
                meshes.hideFlags = HideFlags.HideInHierarchy;

                GameObject prefabLines = new GameObject("Prefab Lines");
                prefabLines.transform.SetParent(transform, false);
                prefabLines.hideFlags = HideFlags.HideInHierarchy;

                // Add lod levels
                lodDistances.Add(0.3f);
                lodDistances.Add(0.5f);
                lodDistances.Add(0.8f);

                lodVertexDivisions.Add(5);
                lodVertexDivisions.Add(10);
                lodVertexDivisions.Add(20);

                // Create tags
                Utility.AddLayers();
            }
        }

        public PointData CalculatePoints()
        {
            List<Vector3> points = new List<Vector3>();
            List<int> segmentIndexes = new List<int>();

            for (int i = 0; i < transform.GetChild(0).childCount - 1; i++)
            {
                float curveLength = Utility.GetCurveLenth(transform.GetChild(0).GetChild(i).position, transform.GetChild(0).GetChild(i + 1).position, transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint(), false);
                int pointAmount = Mathf.Max(3, (int)(curveLength * detailLevel / 10));
                segmentIndexes.Add(points.Count);
                HashSet<int> extraPoints = new HashSet<int>();
                int addedPoints = 0;

                // Calculate which points should have an extra point
                for (int j = 0; j < lanes.Count; j++)
                {
                    // Add extra points at starts of lanes
                    if (lanes[j].startIndex == i && lanes[j].startPercentageOffset > 0)
                    {
                        extraPoints.Add(Mathf.FloorToInt(lanes[j].startPercentageOffset * pointAmount));
                    }
                }

                float pointPercentage = 1f / pointAmount;
                for (float t = 0; t <= 1; t += pointPercentage)
                {
                    float realT = t;
                    if (realT > 1)
                    {
                        realT = 1;
                    }

                    Vector3 point = Utility.Lerp4(transform.GetChild(0).GetChild(i).position, transform.GetChild(0).GetChild(i + 1).position, transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint(), realT);
                    // Prevent more than 2 points at the same position
                    if (points.Count > 2 && Utility.AlmostEqual(points[points.Count - 1], points[points.Count - 2], 0.001f) && Utility.AlmostEqual(points[points.Count - 1], point, 0.001f))
                    {
                        continue;
                    }

                    points.Add(point);

                    if (t > 0 && extraPoints.Contains(addedPoints))
                    {
                        // Prevent more than 2 points at the same position
                        if (points.Count > 2 && Utility.AlmostEqual(points[points.Count - 1], points[points.Count - 2], 0.001f) && Utility.AlmostEqual(points[points.Count - 1], point, 0.001f))
                        {
                            break;
                        }

                        points.Add(point);
                        extraPoints.Remove(addedPoints);
                    }

                    addedPoints++;
                }

                // Add last point
                Vector3 lastPoint = Utility.Lerp4(transform.GetChild(0).GetChild(i).position, transform.GetChild(0).GetChild(i + 1).position, transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint(), 1);
                if (points[points.Count - 1] != lastPoint)
                {
                    points.Add(lastPoint);
                }

                if (i < transform.GetChild(0).childCount - 1)
                {
                    if (i > 0)
                    {
                        segmentIndexes[segmentIndexes.Count - 1] -= 1;
                    }
                }
            }

            return new PointData(points, segmentIndexes);
        }

        #region Intersections

        public void CheckForIntersections(int index)
        {
            if (index == -1)
            {
                return;
            }

            // Don't connect cyclic road
            if (cyclic)
            {
                return;
            }

            // Move point to nearby point
            if (connectToIntersections && ((index == 0 && startIntersection == null) || (index == transform.GetChild(0).childCount - 1 && endIntersection == null)))
            {
                Point movedPoint = transform.GetChild(0).GetChild(index).GetComponent<Point>();
                Point nearbyPoint = null;

                RaycastHit raycastHit;
                if (Physics.Raycast(movedPoint.transform.position + Vector3.up, Vector3.down, out raycastHit, 10, LayerMask.GetMask("Road")))
                {
                    for (int i = 0; i < raycastHit.transform.transform.parent.parent.GetChild(0).childCount; i++)
                    {
                        Transform point = raycastHit.transform.transform.parent.parent.GetChild(0).GetChild(i);
                        // Make sure it's a point from a road and not from a prefab line
                        if (point != movedPoint && point.parent.parent.GetComponent<RoadCreator>() != null && Vector3.Distance(movedPoint.transform.position, point.position) < 3)
                        {
                            int pointSiblingIndex = point.GetSiblingIndex();
                            int movedPointSiblingIndex = movedPoint.transform.GetSiblingIndex();

                            // Only allow start point to connect to end point
                            if (point.parent == movedPoint.transform.parent && (Mathf.Abs(pointSiblingIndex - movedPointSiblingIndex) <= 1 || (pointSiblingIndex > 0 && pointSiblingIndex < point.parent.childCount - 1) || (movedPointSiblingIndex > 0 && movedPointSiblingIndex < movedPoint.transform.parent.childCount - 1)))
                            {
                                return;
                            }

                            // Don't connect to point that is connected to itself
                            RoadCreator parentRoad = point.transform.parent.parent.GetComponent<RoadCreator>();
                            if (parentRoad.startIntersection != null && parentRoad.startIntersection == parentRoad.endIntersection)
                            {
                                return;
                            }

                            // Disable cyclic property to prevent issues
                            if (parentRoad.cyclic)
                            {
                                parentRoad.cyclic = false;
                            }

                            movedPoint.transform.position = point.position;
                            nearbyPoint = point.GetComponent<Point>();
                            break;
                        }
                    }

                    if (nearbyPoint == null)
                    {
                        return;
                    }

                    Point extraPoint = null;
                    if (nearbyPoint.transform.GetSiblingIndex() > 0 && nearbyPoint.transform.GetSiblingIndex() < nearbyPoint.transform.parent.childCount - 1)
                    {
                        // If a middle point then split segment
                        extraPoint = nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().SplitSegment(nearbyPoint.transform.GetSiblingIndex(), true);
                    }

                    // Move points
                    PointData pointData = GetPointData();

                    // Calculate forward direction
                    Vector3 forward = Vector3.zero;
                    if (index == 0)
                    {
                        forward = (pointData.positions[0] - pointData.positions[1]).normalized;
                    }
                    else
                    {
                        forward = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
                    }

                    Vector3 centerPosition = movedPoint.transform.position;
                    Undo.RecordObject(movedPoint.transform, "Moved Point");
                    movedPoint.transform.position -= forward * 5f;

                    pointData = nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().GetPointData();

                    // Calculate forward direction
                    if (nearbyPoint.transform.GetSiblingIndex() == 0)
                    {
                        forward = (pointData.positions[0] - pointData.positions[1]).normalized;
                    }
                    else
                    {
                        forward = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
                    }
                    Undo.RecordObject(nearbyPoint.transform, "Moved Point");
                    nearbyPoint.transform.position -= forward * 4f;

                    if (extraPoint != null)
                    {
                        pointData = extraPoint.transform.parent.parent.GetComponent<RoadCreator>().GetPointData();

                        // Calculate forward direction
                        if (extraPoint.transform.GetSiblingIndex() == 0)
                        {
                            forward = (pointData.positions[0] - pointData.positions[1]).normalized;
                        }
                        else
                        {
                            forward = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
                        }

                        Undo.RecordObject(extraPoint.transform, "Moved Point");
                        extraPoint.transform.position -= forward * 4f;
                    }

                    Regenerate(false);
                    nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().Regenerate(false);

                    if (extraPoint != null)
                    {
                        extraPoint.transform.parent.parent.GetComponent<RoadCreator>().Regenerate(false);
                    }

                    // Create intersection
                    CreateIntersection(movedPoint, nearbyPoint, extraPoint, centerPosition);
                }
                else
                {
                    if (Physics.Raycast(movedPoint.transform.position + Vector3.up, Vector3.down, out raycastHit, 10, LayerMask.GetMask("Intersection")))
                    {
                        ConnectToIntersection(movedPoint, raycastHit.transform.parent.parent.GetComponent<Intersection>());
                    }
                }
            }
            else if ((index == 0 && startIntersection != null) || (index == transform.GetChild(0).childCount - 1 && endIntersection != null))
            {
                if (!iDown)
                {
                    Point movedPoint = transform.GetChild(0).GetChild(index).GetComponent<Point>();
                    DisconnectFromIntersection(movedPoint);
                }
            }
        }

        public void CreateIntersection(Point movedPoint, Point nearbyPoint, Point extraPoint, Vector3 intersectionPosition)
        {
            // Split if neccecary
            if (nearbyPoint.transform.GetSiblingIndex() > 0 && nearbyPoint.transform.GetSiblingIndex() < nearbyPoint.transform.parent.childCount - 1)
            {
                extraPoint = nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().SplitSegment(nearbyPoint.transform.GetSiblingIndex(), true);
            }

            GameObject intersection = new GameObject("Intersection");
            Undo.RegisterCreatedObjectUndo(intersection, "Moved Point");
            intersection.transform.SetParent(transform.parent);
            intersection.transform.position = intersectionPosition;
            intersection.AddComponent<Intersection>();
            intersection.GetComponent<Intersection>().InitializeIntersection();

            // This connection
            Vector3 tangent = Vector3.zero;
            bool end = true;

            if (movedPoint.transform.GetSiblingIndex() == 0)
            {
                end = false;
            }

            IntersectionConnection thisConnection = new IntersectionConnection(movedPoint, tangent, tangent, end);
            intersection.GetComponent<Intersection>().connections.Add(thisConnection);

            Undo.RegisterCompleteObjectUndo(this, "Moved Point");
            if (movedPoint.transform.GetSiblingIndex() == 0)
            {
                startIntersection = intersection.GetComponent<Intersection>();
                startIntersectionConnection = thisConnection;
            }
            else
            {
                endIntersection = intersection.GetComponent<Intersection>();
                endIntersectionConnection = thisConnection;
            }

            // Other connection
            end = true;
            if (nearbyPoint.transform.GetSiblingIndex() == 0)
            {
                end = false;
            }

            IntersectionConnection otherConnection = new IntersectionConnection(nearbyPoint, tangent, tangent, end);
            intersection.GetComponent<Intersection>().connections.Add(otherConnection);

            Undo.RegisterCompleteObjectUndo(nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>(), "Moved Point");
            if (nearbyPoint.transform.GetSiblingIndex() == 0)
            {
                nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().startIntersection = intersection.GetComponent<Intersection>();
                nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().startIntersectionConnection = otherConnection;
            }
            else
            {
                nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().endIntersection = intersection.GetComponent<Intersection>();
                nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().endIntersectionConnection = otherConnection;
            }

            // Third connection
            if (extraPoint != null)
            {
                end = true;
                if (extraPoint.transform.GetSiblingIndex() == 0)
                {
                    end = false;
                }

                IntersectionConnection thirdConnection = new IntersectionConnection(extraPoint, tangent, tangent, end);
                intersection.GetComponent<Intersection>().connections.Add(thirdConnection);

                Undo.RegisterCompleteObjectUndo(extraPoint.transform.parent.parent.GetComponent<RoadCreator>(), "Moved Point");
                if (extraPoint.transform.GetSiblingIndex() == 0)
                {
                    extraPoint.transform.parent.parent.GetComponent<RoadCreator>().startIntersection = intersection.GetComponent<Intersection>();
                    extraPoint.transform.parent.parent.GetComponent<RoadCreator>().startIntersectionConnection = thirdConnection;
                }
                else
                {
                    extraPoint.transform.parent.parent.GetComponent<RoadCreator>().endIntersection = intersection.GetComponent<Intersection>();
                    extraPoint.transform.parent.parent.GetComponent<RoadCreator>().endIntersectionConnection = thirdConnection;
                }
            }

            UpdateConnectedLanes();
            nearbyPoint.transform.parent.parent.GetComponent<RoadCreator>().UpdateConnectedLanes();

            if (extraPoint != null)
            {
                extraPoint.transform.parent.parent.GetComponent<RoadCreator>().UpdateConnectedLanes();
            }

            intersection.GetComponent<Intersection>().RecalculateTangents();
            intersection.GetComponent<Intersection>().Regenerate(true);
        }

        public void ConnectToIntersection(Point movedPoint, Intersection intersection)
        {
            // Create new connection
            Vector3 tangent = Vector3.zero;
            bool end = true;
            PointData pointData = GetPointData();

            if (movedPoint.transform.GetSiblingIndex() == 0)
            {
                end = false;
                tangent = (pointData.positions[0] - pointData.positions[1]).normalized;
            }
            else
            {
                tangent = (pointData.positions[pointData.positions.Count - 1] - pointData.positions[pointData.positions.Count - 2]).normalized;
            }

            IntersectionConnection connection = new IntersectionConnection(movedPoint, tangent, tangent, end);
            Undo.RegisterCompleteObjectUndo(this, "Moved Point");
            Undo.RegisterCompleteObjectUndo(intersection.GetComponent<Intersection>(), "Moved Point");
            intersection.GetComponent<Intersection>().connections.Add(connection);

            Undo.RegisterCompleteObjectUndo(this, "Moved Point");
            if (movedPoint.transform.GetSiblingIndex() == 0)
            {
                startIntersection = intersection.GetComponent<Intersection>();
                startIntersectionConnection = connection;
            }
            else
            {
                endIntersection = intersection.GetComponent<Intersection>();
                endIntersectionConnection = connection;
            }

            UpdateConnectedLanes();
            Regenerate();
            intersection.GetComponent<Intersection>().RecalculateTangents();
            intersection.GetComponent<Intersection>().Regenerate(false, false);
        }

        public void DisconnectFromIntersection(Point movedPoint)
        {
            Undo.RegisterCompleteObjectUndo(this, "Moved Point");
            if (movedPoint.transform.GetSiblingIndex() == 0)
            {
                Intersection intersection = startIntersection;
                startIntersection = null;
                Undo.RegisterCompleteObjectUndo(intersection, "Moved Point");
                intersection.connections.Remove(startIntersectionConnection);
                startIntersectionConnection = null;
                intersection.Regenerate(false, false);
            }
            else
            {
                Intersection intersection = endIntersection;
                endIntersection = null;
                Undo.RegisterCompleteObjectUndo(intersection, "Moved Point");
                intersection.connections.Remove(endIntersectionConnection);
                endIntersectionConnection = null;
                intersection.Regenerate(false, false);
            }
        }

        public void UpdateConnectedLanes()
        {
            // TODO: Fix inspector lock
            if (startIntersection != null || endIntersection != null)
            {
                if (startIntersection != null)
                {
                    startIntersectionConnection.connectedLanes.Clear();
                    startIntersectionConnection.connectedLaneIndexes.Clear();
                }

                if (endIntersection != null)
                {
                    endIntersectionConnection.connectedLanes.Clear();
                    endIntersectionConnection.connectedLaneIndexes.Clear();
                }

                for (int i = 0; i < lanes.Count; i++)
                {
                    // Add lanes that start at the start
                    if (startIntersection != null)
                    {
                        if (lanes[i].startIndex == 0 && lanes[i].startPercentageOffset == 0)
                        {
                            startIntersectionConnection.connectedLanes.Add(lanes[i]);
                            startIntersectionConnection.connectedLaneIndexes.Add(i);
                        }
                    }

                    // Add lanes that end at the end
                    if (endIntersection != null)
                    {
                        if (lanes[i].endIndex == transform.GetChild(0).childCount - 2 && lanes[i].endPercentageOffset == 1)
                        {
                            endIntersectionConnection.connectedLanes.Add(lanes[i]);
                            endIntersectionConnection.connectedLaneIndexes.Add(i);
                        }
                    }
                }
            }
        }

        public void RemoveConnectedPoint(int index)
        {
            if (index == 0 && startIntersection != null)
            {
                Intersection intersection = startIntersection;
                Undo.RegisterCompleteObjectUndo(this, "Remove Point");
                startIntersection = null;

                Undo.RegisterCompleteObjectUndo(intersection, "Remove Point");
                intersection.connections.Remove(startIntersectionConnection);
                intersection.Regenerate(true, false);

                startIntersectionConnection = null;
            }
            else if (index != 0 && endIntersection != null)
            {
                Intersection intersection = endIntersection;
                Undo.RegisterCompleteObjectUndo(this, "Remove Point");
                endIntersection = null;

                Undo.RegisterCompleteObjectUndo(intersection, "Remove Point");
                intersection.connections.Remove(endIntersectionConnection);
                intersection.Regenerate(true, false);

                endIntersectionConnection = null;
            }
        }

        #endregion
    }
}
#endif
