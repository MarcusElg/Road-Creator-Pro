using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    public class Utility
    {

        public static Vector3 MaxVector3 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        // Dictionary is by reference so it doesn't work
        private static List<Vector3Bool> turnMarkingsKeys = new List<Vector3Bool>();
        private static List<string> turnMarkingsValues = new List<string>();

        public static string GetTurnMarking(Vector3Bool bools)
        {
            if (turnMarkingsKeys.Count == 0)
            {
                // Initialize Dictionary
                turnMarkingsKeys.Add(new Vector3Bool(true, false, false));
                turnMarkingsValues.Add("leftTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(false, true, false));
                turnMarkingsValues.Add("forwardTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(false, false, true));
                turnMarkingsValues.Add("rightTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(true, true, false));
                turnMarkingsValues.Add("leftForwardTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(false, true, true));
                turnMarkingsValues.Add("rightForwardTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(true, false, true));
                turnMarkingsValues.Add("leftRightTurnMarking");
                turnMarkingsKeys.Add(new Vector3Bool(true, true, true));
                turnMarkingsValues.Add("leftRightForwardTurnMarking");
            }

            // Get value
            for (int i = 0; i < turnMarkingsKeys.Count; i++)
            {
                if (turnMarkingsKeys[i].one == bools.one && turnMarkingsKeys[i].two == bools.two && turnMarkingsKeys[i].three == bools.three)
                {
                    return turnMarkingsValues[i];
                }
            }

            return "";
        }

        public static Vector3 ToXZ(Vector3 input)
        {
            return new Vector3(input.x, 0, input.z);
        }

        public static Vector3[] ClosestPointOnLineSegment(Vector3 point, Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent)
        {
            float nearestDistance = float.MaxValue;
            Vector3 neareastPoint = Vector3.zero;
            Vector3 nextPoint = Vector3.zero;
            Vector3[] points = Handles.MakeBezierPoints(start, end, startTangent, endTangent, 100);

            for (float f = 0.02f; f <= 0.98f; f += 0.01f)
            {
                Vector3 lerpedPoint = points[(int)(f * 100)];
                float distance = Vector3.Distance(point, lerpedPoint);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    neareastPoint = lerpedPoint;
                    nextPoint = points[(int)((f + 0.01f) * 100)];
                }
            }

            return new Vector3[] { neareastPoint, nextPoint };
        }

        public static Vector3 ClosestPointOnLine(Vector3 point, Vector3 start, Vector3 end)
        {
            // Get direction
            Vector2 direction = (new Vector2(end.x, end.z) - new Vector2(start.x, start.z));
            float magnitudeMax = direction.magnitude;
            direction.Normalize();

            // Check if point is with line start/end
            Vector2 lhs = new Vector2(point.x, point.z) - new Vector2(start.x, start.z);
            float dotP = Vector2.Dot(lhs, direction);
            dotP = Mathf.Clamp(dotP, 0f, magnitudeMax);
            Vector2 result = new Vector2(start.x, start.z) + direction * dotP;
            return new Vector3(result.x, (start.y + end.y) / 2, result.y);
        }

        // Source: https://github.com/SebLague/Ear-Clipping-Triangulation/blob/master/Scripts/Maths2D.cs
        public static bool PointInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
            float s = 1 / (2 * area) * (a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y);
            float t = 1 / (2 * area) * (a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y);
            return s >= 0 && t >= 0 && (s + t) <= 1;
        }

        // Source: https://github.com/SebLague/Ear-Clipping-Triangulation/blob/master/Scripts/Maths2D.cs
        public static bool IsConvex(Vector3 start, Vector3 center, Vector3 end)
        {
            return (int)Mathf.Sign((end.x - start.x) * (-center.z + start.z) + (end.z - start.z) * (center.x - start.x)) == -1;
        }

        public static Vector3 DrawPositionHandle(bool alwaysScale, float handleSize, Vector3 position, Quaternion rotation)
        {
            handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(position));

            if (alwaysScale)
            {
                handleSize = HandleUtility.GetHandleSize(position);
            }

            Color color = Handles.color;
            Handles.color = Handles.xAxisColor;
            position = Handles.Slider(position, rotation * Vector3.right, handleSize, Handles.ArrowHandleCap, EditorSnapSettings.move.x);
            Handles.color = Handles.yAxisColor;
            position = Handles.Slider(position, rotation * Vector3.up, handleSize, Handles.ArrowHandleCap, EditorSnapSettings.move.y);
            Handles.color = Handles.zAxisColor;
            position = Handles.Slider(position, rotation * Vector3.forward, handleSize, Handles.ArrowHandleCap, EditorSnapSettings.move.z);
            Handles.color = Handles.centerColor;
#if UNITY_2022_1_OR_NEWER
            position = Handles.FreeMoveHandle(position, handleSize * 0.15f, EditorSnapSettings.move, Handles.RectangleHandleCap);
#else
            position = Handles.FreeMoveHandle(position, Quaternion.identity, handleSize * 0.15f, EditorSnapSettings.move, Handles.RectangleHandleCap);
#endif
            Handles.color = color;
            return position;
        }

        public static Vector3 GetMousePosition(bool collideWithPrefabs, bool collideWithRoads)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit raycastHit;

            int raycastLayers = ~(1 << LayerMask.NameToLayer("Prefab Line") | 1 << LayerMask.NameToLayer("Road"));
            if (collideWithPrefabs)
            {
                if (collideWithRoads)
                {
                    raycastLayers = Physics.DefaultRaycastLayers;
                }
                else
                {
                    raycastLayers = ~(1 << LayerMask.NameToLayer("Road"));
                }
            }
            else if (collideWithRoads)
            {
                raycastLayers = ~(1 << LayerMask.NameToLayer("Prefab Line"));
            }

            if (Physics.Raycast(mouseRay, out raycastHit, 1000, raycastLayers))
            {
                return raycastHit.point;
            }
            else
            {
                Vector3 mousePosition = mouseRay.GetPoint(10);

                float zDirection = mouseRay.direction.z;
                if (zDirection != 0)
                {
                    float dstToXYPlane = Mathf.Abs(mouseRay.origin.z / zDirection);
                    mousePosition = mouseRay.GetPoint(dstToXYPlane);
                }

                return mousePosition;
            }
        }

        public static float GetMaxValue(AnimationCurve animationCurve)
        {
            float maxHeight = 0;

            for (int i = 0; i < animationCurve.keys.Length; i++)
            {
                if (animationCurve.keys[i].value > maxHeight)
                {
                    maxHeight = animationCurve.keys[i].value;
                }
            }

            return maxHeight;
        }

        #region Copy Data

        public static void CopyLaneData(SerializedProperty original, SerializedProperty copy)
        {
            copy.FindPropertyRelative("wholeRoad").boolValue = original.FindPropertyRelative("wholeRoad").boolValue;
            copy.FindPropertyRelative("startIndex").intValue = original.FindPropertyRelative("startIndex").intValue;
            copy.FindPropertyRelative("startPercentageOffset").floatValue = original.FindPropertyRelative("startPercentageOffset").floatValue;
            copy.FindPropertyRelative("endIndex").intValue = original.FindPropertyRelative("endIndex").intValue;
            copy.FindPropertyRelative("endPercentageOffset").floatValue = original.FindPropertyRelative("endPercentageOffset").floatValue;
            copy.FindPropertyRelative("width").animationCurveValue = new AnimationCurve(original.FindPropertyRelative("width").animationCurveValue.keys);
            copy.FindPropertyRelative("yOffset").animationCurveValue = new AnimationCurve(original.FindPropertyRelative("yOffset").animationCurveValue.keys);
            copy.FindPropertyRelative("textureTilingMultiplier").floatValue = original.FindPropertyRelative("textureTilingMultiplier").floatValue;
            copy.FindPropertyRelative("constantUvWidth").boolValue = original.FindPropertyRelative("constantUvWidth").boolValue;
            copy.FindPropertyRelative("flipUvs").boolValue = original.FindPropertyRelative("flipUvs").boolValue;
            copy.FindPropertyRelative("uvXMin").floatValue = original.FindPropertyRelative("uvXMin").floatValue;
            copy.FindPropertyRelative("uvXMax").floatValue = original.FindPropertyRelative("uvXMax").floatValue;
            copy.FindPropertyRelative("ignoreForWidthCalculation").boolValue = original.FindPropertyRelative("ignoreForWidthCalculation").boolValue;

            copy.FindPropertyRelative("materials").ClearArray();

            int materialsCount = original.FindPropertyRelative("materials").arraySize;
            for (int i = 0; i < materialsCount; i++)
            {
                copy.FindPropertyRelative("materials").InsertArrayElementAtIndex(copy.FindPropertyRelative("materials").arraySize);
                copy.FindPropertyRelative("materials").GetArrayElementAtIndex(i).objectReferenceValue = original.FindPropertyRelative("materials").GetArrayElementAtIndex(i).objectReferenceValue;
            }
        }

        public static void CopyTerrainIntervalData(SerializedProperty original, SerializedProperty copy)
        {
            copy.FindPropertyRelative("wholeRoad").boolValue = original.FindPropertyRelative("wholeRoad").boolValue;
            copy.FindPropertyRelative("startIndex").intValue = original.FindPropertyRelative("startIndex").intValue;
            copy.FindPropertyRelative("startPercentageOffset").floatValue = original.FindPropertyRelative("startPercentageOffset").floatValue;
            copy.FindPropertyRelative("endIndex").intValue = original.FindPropertyRelative("endIndex").intValue;
            copy.FindPropertyRelative("endPercentageOffset").floatValue = original.FindPropertyRelative("endPercentageOffset").floatValue;
        }

        public static void CopyPrefabData(PrefabLineCreator original, PrefabLineCreator copy)
        {
            // General
            copy.startOffsetPercentage = original.startOffsetPercentage;
            copy.endOffsetPercentage = original.endOffsetPercentage;
            copy.randomizeSpacing = original.randomizeSpacing;
            copy.spacing = original.spacing;
            copy.maxSpacing = original.maxSpacing;
            copy.fillGap = original.fillGap;
            copy.deformPrefabsToCurve = original.deformPrefabsToCurve;
            copy.deformPrefabsToTerrain = original.deformPrefabsToTerrain;
            copy.yOffset = original.yOffset;
            copy.rotationDirection = original.rotationDirection;
            copy.rotationRandomization = original.rotationRandomization;
            copy.endMode = original.endMode;
            copy.bridgePillarMode = original.bridgePillarMode;
            copy.onlyYModifyBottomVertices = original.onlyYModifyBottomVertices;
            copy.centralYModification = original.centralYModification;
            copy.useCenterDistance = original.useCenterDistance;

            // Prefabs
            copy.startPrefab = original.startPrefab;
            copy.mainPrefab = original.mainPrefab;
            copy.endPrefab = original.endPrefab;
            copy.xScale = original.xScale;
            copy.yScale = new AnimationCurve(original.yScale.keys);
            copy.zScale = new AnimationCurve(original.zScale.keys);

            // Road prefab lines
            copy.controlled = true;
            copy.offsetCurve = new AnimationCurve(original.offsetCurve.keys);
            copy.wholeRoad = original.wholeRoad;
            copy.startIndex = original.startIndex;
            copy.startOffsetPercentage = original.startOffsetPercentage;
            copy.endIndex = original.endIndex;
            copy.endOffsetPercentage = original.endOffsetPercentage;
        }

        public static void CopyMainRoadsData(SerializedProperty original, SerializedProperty copy)
        {
            copy.FindPropertyRelative("material").objectReferenceValue = original.FindPropertyRelative("material").objectReferenceValue;
            copy.FindPropertyRelative("physicMaterial").objectReferenceValue = original.FindPropertyRelative("physicMaterial").objectReferenceValue;
            copy.FindPropertyRelative("flipUvs").boolValue = original.FindPropertyRelative("flipUvs").boolValue;
            copy.FindPropertyRelative("textureTilingMultiplier").floatValue = original.FindPropertyRelative("textureTilingMultiplier").floatValue;
            copy.FindPropertyRelative("textureTilingOffset").floatValue = original.FindPropertyRelative("textureTilingOffset").floatValue;
            copy.FindPropertyRelative("startIndex").intValue = original.FindPropertyRelative("startIndex").intValue;
            copy.FindPropertyRelative("endIndex").intValue = original.FindPropertyRelative("endIndex").intValue;
            copy.FindPropertyRelative("wholeLeftRoad").boolValue = original.FindPropertyRelative("wholeLeftRoad").boolValue;
            copy.FindPropertyRelative("wholeRightRoad").boolValue = original.FindPropertyRelative("wholeRightRoad").boolValue;
            copy.FindPropertyRelative("startIndexLeftRoad").intValue = original.FindPropertyRelative("startIndexLeftRoad").intValue;
            copy.FindPropertyRelative("endIndexLeftRoad").intValue = original.FindPropertyRelative("endIndexLeftRoad").intValue;
            copy.FindPropertyRelative("startIndexRightRoad").intValue = original.FindPropertyRelative("startIndexRightRoad").intValue;
            copy.FindPropertyRelative("endIndexRightRoad").intValue = original.FindPropertyRelative("endIndexRightRoad").intValue;
            copy.FindPropertyRelative("yOffset").floatValue = original.FindPropertyRelative("yOffset").floatValue;
        }

        public static void CopyCrosswalkData(SerializedProperty original, SerializedProperty copy)
        {
            copy.FindPropertyRelative("connectionIndex").intValue = original.FindPropertyRelative("connectionIndex").intValue;
            copy.FindPropertyRelative("width").floatValue = original.FindPropertyRelative("width").floatValue;
            copy.FindPropertyRelative("insetDistance").floatValue = original.FindPropertyRelative("insetDistance").floatValue;
            copy.FindPropertyRelative("anchorAtConnection").boolValue = original.FindPropertyRelative("anchorAtConnection").boolValue;
            copy.FindPropertyRelative("material").objectReferenceValue = original.FindPropertyRelative("material").objectReferenceValue;
            copy.FindPropertyRelative("textureTilingMultiplier").floatValue = original.FindPropertyRelative("textureTilingMultiplier").floatValue;
            copy.FindPropertyRelative("textureTilingOffset").floatValue = original.FindPropertyRelative("textureTilingOffset").floatValue;
            copy.FindPropertyRelative("yOffset").floatValue = original.FindPropertyRelative("yOffset").floatValue;
        }

        public static void CopyCrosswalkData(IntersectionCrosswalk original, IntersectionCrosswalk copy)
        {
            copy.connectionIndex = original.connectionIndex;
            copy.width = original.width;
            copy.insetDistance = original.insetDistance;
            copy.anchorAtConnection = original.anchorAtConnection;
            copy.material = original.material;
            copy.textureTilingMultiplier = original.textureTilingMultiplier;
            copy.textureTilingOffset = original.textureTilingOffset;
            copy.yOffset = original.yOffset;
        }

        public static void CopyRoadDataToRoadPreset(SerializedObject road, RoadPreset roadPreset)
        {
            // General
            roadPreset.baseYOffset = road.FindProperty("baseYOffset").floatValue;
            roadPreset.connectToIntersections = road.FindProperty("connectToIntersections").boolValue;
            roadPreset.generateColliders = road.FindProperty("generateColliders").boolValue;

            // LOD
            roadPreset.lodLevels = road.FindProperty("lodLevels").intValue;
            roadPreset.lodDistances = new List<float>();
            roadPreset.lodVertexDivisions = new List<int>();

            for (int i = 0; i < road.FindProperty("lodDistances").arraySize; i++)
            {
                roadPreset.lodDistances.Add(road.FindProperty("lodDistances").GetArrayElementAtIndex(i).floatValue);
                roadPreset.lodVertexDivisions.Add(road.FindProperty("lodVertexDivisions").GetArrayElementAtIndex(i).intValue);
            }

            // Terrain deformation
            roadPreset.deformMeshToTerrain = road.FindProperty("deformMeshToTerrain").boolValue;
            roadPreset.modifyTerrainHeight = road.FindProperty("modifyTerrainHeight").boolValue;
            roadPreset.terrainRadius = road.FindProperty("terrainRadius").floatValue;
            roadPreset.terrainSmoothingRadius = road.FindProperty("terrainSmoothingRadius").intValue;
            roadPreset.terrainSmoothingAmount = road.FindProperty("terrainSmoothingAmount").floatValue;
            roadPreset.modifyTerrainOnUpdate = road.FindProperty("modifyTerrainOnUpdate").boolValue;
            roadPreset.terrainDetailsRadius = road.FindProperty("terrainDetailsRadius").floatValue;
            roadPreset.terrainRemoveDetailsOnUpdate = road.FindProperty("terrainRemoveDetailsOnUpdate").boolValue;
            roadPreset.terrainTreesRadius = road.FindProperty("terrainTreesRadius").floatValue;
            roadPreset.terrainRemoveTreesOnUpdate = road.FindProperty("terrainRemoveTreesOnUpdate").boolValue;
            roadPreset.terrainAngle = road.FindProperty("terrainAngle").floatValue;
            roadPreset.terrainExtraMaxHeight = road.FindProperty("terrainExtraMaxHeight").floatValue;
            roadPreset.terrainRemoveDetails = road.FindProperty("terrainRemoveDetails").boolValue;
            roadPreset.terrainRemoveTrees = road.FindProperty("terrainRemoveTrees").boolValue;
            roadPreset.terrainModificationYOffset = road.FindProperty("terrainModificationYOffset").floatValue;

            // Note: indexes arn't saved as those change depending on road
            // Lanes
            roadPreset.lanes = new List<Lane>();
            for (int i = 0; i < road.FindProperty("lanes").arraySize; i++)
            {
                if (road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("wholeRoad").boolValue)
                {
                    Lane lane = new Lane();
                    roadPreset.lanes.Add(lane);
                    lane.startPercentageOffset = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("startPercentageOffset").floatValue;
                    lane.endPercentageOffset = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("endPercentageOffset").floatValue;
                    lane.width = new AnimationCurve(road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("width").animationCurveValue.keys);
                    lane.yOffset = new AnimationCurve(road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("yOffset").animationCurveValue.keys);
                    lane.textureTilingMultiplier = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("textureTilingMultiplier").floatValue;
                    lane.constantUvWidth = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("constantUvWidth").boolValue;
                    lane.flipUvs = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("flipUvs").boolValue;
                    lane.uvXMin = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("uvXMin").floatValue;
                    lane.uvXMax = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("uvXMax").floatValue;
                    lane.mainRoadPart = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("mainRoadPart").boolValue;
                    lane.ignoreForWidthCalculation = road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("ignoreForWidthCalculation").boolValue;

                    // Materials
                    lane.materials = new List<Material>();
                    for (int j = 0; j < road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("materials").arraySize; j++)
                    {
                        lane.materials.Add((Material)road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("materials").GetArrayElementAtIndex(j).objectReferenceValue);
                    }
                }
            }

            // Prefabs
            roadPreset.prefabLines.Clear();
            for (int i = 0; i < road.FindProperty("prefabLines").arraySize; i++)
            {
                PrefabLineData prefabLineData = new PrefabLineData();
                PrefabLineCreator prefabLine = ((PrefabLineCreator)road.FindProperty("prefabLines").GetArrayElementAtIndex(i).objectReferenceValue);
                roadPreset.prefabLines.Add(prefabLineData);

                // General
                prefabLineData.startOffsetPercentage = prefabLine.startOffsetPercentage;
                prefabLineData.endOffsetPercentage = prefabLine.endOffsetPercentage;
                prefabLineData.spacing = prefabLine.spacing;
                prefabLineData.fillGap = prefabLine.fillGap;
                prefabLineData.deformPrefabsToCurve = prefabLine.deformPrefabsToCurve;
                prefabLineData.deformPrefabsToTerrain = prefabLine.deformPrefabsToTerrain;
                prefabLineData.yOffset = prefabLine.yOffset;
                prefabLineData.rotationDirection = prefabLine.rotationDirection;
                prefabLineData.rotationRandomization = prefabLine.rotationRandomization;
                prefabLineData.endMode = prefabLine.endMode;
                prefabLineData.bridgePillarMode = prefabLine.bridgePillarMode;
                prefabLineData.onlyYModifyBottomVertices = prefabLine.onlyYModifyBottomVertices;
                prefabLineData.centralYModification = prefabLine.centralYModification;
                prefabLineData.useCenterDistance = prefabLine.useCenterDistance;

                // Prefabs
                prefabLineData.startPrefab = prefabLine.startPrefab;
                prefabLineData.mainPrefab = prefabLine.mainPrefab;
                prefabLineData.endPrefab = prefabLine.endPrefab;
                prefabLineData.xScale = prefabLine.xScale;
                prefabLineData.yScale = new AnimationCurve(prefabLine.yScale.keys);
                prefabLineData.zScale = new AnimationCurve(prefabLine.zScale.keys);

                // Road prefab lines
                prefabLineData.controlled = true;
                prefabLineData.offsetCurve = new AnimationCurve(prefabLine.offsetCurve.keys);
                prefabLineData.wholeRoad = true;
            }
        }

        public static void CopyRoadPresetToRoadData(RoadPreset roadPreset, SerializedObject road)
        {
            // General
            road.FindProperty("baseYOffset").floatValue = roadPreset.baseYOffset;
            road.FindProperty("connectToIntersections").boolValue = roadPreset.connectToIntersections;
            road.FindProperty("generateColliders").boolValue = roadPreset.generateColliders;

            // LOD
            road.FindProperty("lodLevels").intValue = roadPreset.lodLevels;
            road.FindProperty("lodDistances").ClearArray();
            road.FindProperty("lodVertexDivisions").ClearArray();

            for (int i = 0; i < roadPreset.lodDistances.Count; i++)
            {
                road.FindProperty("lodDistances").InsertArrayElementAtIndex(i);
                road.FindProperty("lodDistances").GetArrayElementAtIndex(road.FindProperty("lodDistances").arraySize - 1).floatValue = roadPreset.lodDistances[i];
                road.FindProperty("lodVertexDivisions").InsertArrayElementAtIndex(i);
                road.FindProperty("lodVertexDivisions").GetArrayElementAtIndex(road.FindProperty("lodDistances").arraySize - 1).intValue = roadPreset.lodVertexDivisions[i];
            }

            // Terrain deformation
            road.FindProperty("deformMeshToTerrain").boolValue = roadPreset.deformMeshToTerrain;
            road.FindProperty("modifyTerrainHeight").boolValue = roadPreset.modifyTerrainHeight;
            road.FindProperty("terrainRadius").floatValue = roadPreset.terrainRadius;
            road.FindProperty("terrainSmoothingRadius").intValue = roadPreset.terrainSmoothingRadius;
            road.FindProperty("terrainSmoothingAmount").floatValue = roadPreset.terrainSmoothingAmount;
            road.FindProperty("modifyTerrainOnUpdate").boolValue = roadPreset.modifyTerrainOnUpdate;
            road.FindProperty("terrainDetailsRadius").floatValue = roadPreset.terrainDetailsRadius;
            road.FindProperty("terrainRemoveDetailsOnUpdate").boolValue = roadPreset.terrainRemoveDetailsOnUpdate;
            road.FindProperty("terrainTreesRadius").floatValue = roadPreset.terrainTreesRadius;
            road.FindProperty("terrainRemoveTreesOnUpdate").boolValue = roadPreset.terrainRemoveTreesOnUpdate;
            road.FindProperty("terrainAngle").floatValue = roadPreset.terrainAngle;
            road.FindProperty("terrainExtraMaxHeight").floatValue = roadPreset.terrainExtraMaxHeight;
            road.FindProperty("terrainRemoveDetails").boolValue = roadPreset.terrainRemoveDetails;
            road.FindProperty("terrainRemoveTrees").boolValue = roadPreset.terrainRemoveTrees;
            road.FindProperty("terrainModificationYOffset").floatValue = roadPreset.terrainModificationYOffset;

            // Lanes
            road.FindProperty("lanes").ClearArray();
            road.FindProperty("lanesTab").intValue = 0;

            for (int i = 0; i < roadPreset.lanes.Count; i++)
            {
                road.FindProperty("lanes").InsertArrayElementAtIndex(0);
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("startPercentageOffset").floatValue = roadPreset.lanes[i].startPercentageOffset;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("endPercentageOffset").floatValue = roadPreset.lanes[i].endPercentageOffset;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("width").animationCurveValue = new AnimationCurve(roadPreset.lanes[i].width.keys);
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("yOffset").animationCurveValue = new AnimationCurve(roadPreset.lanes[i].yOffset.keys);
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("textureTilingMultiplier").floatValue = roadPreset.lanes[i].textureTilingMultiplier;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("constantUvWidth").boolValue = roadPreset.lanes[i].constantUvWidth;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("flipUvs").boolValue = roadPreset.lanes[i].flipUvs;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("uvXMin").floatValue = roadPreset.lanes[i].uvXMin;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("uvXMax").floatValue = roadPreset.lanes[i].uvXMax;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("mainRoadPart").boolValue = roadPreset.lanes[i].mainRoadPart;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("wholeRoad").boolValue = true;
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("centerPoint").vector3Value = Utility.MaxVector3;
                road.FindProperty("lanes").GetArrayElementAtIndex(i).FindPropertyRelative("ignoreForWidthCalculation").boolValue = roadPreset.lanes[i].ignoreForWidthCalculation;

                // Materials
                road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("materials").ClearArray();
                for (int j = 0; j < roadPreset.lanes[i].materials.Count; j++)
                {
                    road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("materials").InsertArrayElementAtIndex(0);
                    road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("materials").GetArrayElementAtIndex(0).objectReferenceValue = roadPreset.lanes[i].materials[j];
                    road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("materials").MoveArrayElement(0, road.FindProperty("lanes").GetArrayElementAtIndex(0).FindPropertyRelative("materials").arraySize - 1);
                }

                road.FindProperty("lanes").MoveArrayElement(0, road.FindProperty("lanes").arraySize - 1);
            }
        }

        public static void CopyRoadPresetToRoadPrefabData(RoadPreset roadPreset, RoadCreator road)
        {
            // Prefabs
            // Remove old prefab lines
            road.prefabLines.Clear();
            road.prefabsTab = 0;
            for (int i = road.transform.GetChild(2).childCount - 1; i >= 0; i--)
            {
                GameObject.DestroyImmediate(road.transform.GetChild(2).GetChild(0).gameObject);
            }

            for (int i = 0; i < roadPreset.prefabLines.Count; i++)
            {
                // Create new prefab line
                GameObject prefabLineObject = new GameObject("Prefab Line");
                prefabLineObject.transform.SetParent(road.transform.GetChild(2), false);
                prefabLineObject.AddComponent<PrefabLineCreator>();
                prefabLineObject.transform.hideFlags = HideFlags.NotEditable;
                PrefabLineCreator prefabLine = prefabLineObject.GetComponent<PrefabLineCreator>();
                prefabLine.settings = road.settings;
                prefabLine.InitializeSystem();
                PrefabLineData prefabLineData = roadPreset.prefabLines[i];

                // General
                prefabLine.startOffsetPercentage = prefabLineData.startOffsetPercentage;
                prefabLine.endOffsetPercentage = prefabLineData.endOffsetPercentage;
                prefabLine.spacing = prefabLineData.spacing;
                prefabLine.fillGap = prefabLineData.fillGap;
                prefabLine.deformPrefabsToCurve = prefabLineData.deformPrefabsToCurve;
                prefabLine.deformPrefabsToTerrain = prefabLineData.deformPrefabsToTerrain;
                prefabLine.yOffset = prefabLineData.yOffset;
                prefabLine.rotationDirection = prefabLineData.rotationDirection;
                prefabLine.rotationRandomization = prefabLineData.rotationRandomization;
                prefabLine.endMode = prefabLineData.endMode;
                prefabLine.bridgePillarMode = prefabLineData.bridgePillarMode;
                prefabLine.onlyYModifyBottomVertices = prefabLineData.onlyYModifyBottomVertices;
                prefabLine.centralYModification = prefabLineData.centralYModification;
                prefabLine.useCenterDistance = prefabLineData.useCenterDistance;

                // Prefabs
                prefabLine.startPrefab = prefabLineData.startPrefab;
                prefabLine.mainPrefab = prefabLineData.mainPrefab;
                prefabLine.endPrefab = prefabLineData.endPrefab;
                prefabLine.xScale = prefabLineData.xScale;
                prefabLine.yScale = new AnimationCurve(prefabLineData.yScale.keys);
                prefabLine.zScale = new AnimationCurve(prefabLineData.zScale.keys);

                // Road prefab lines
                prefabLine.controlled = true;
                prefabLine.offsetCurve = new AnimationCurve(prefabLineData.offsetCurve.keys);
                prefabLine.wholeRoad = prefabLineData.wholeRoad;

                road.prefabLines.Add(prefabLine);
            }
        }

        public static void CopyPrefabLineDataToPrefabLinePreset(SerializedObject prefabLine, PrefabLinePreset prefabLinePreset)
        {
            // General
            prefabLinePreset.prefabLineData.startOffsetPercentage = prefabLine.FindProperty("startOffsetPercentage").floatValue;
            prefabLinePreset.prefabLineData.endOffsetPercentage = prefabLine.FindProperty("endOffsetPercentage").floatValue;
            prefabLinePreset.prefabLineData.randomizeSpacing = prefabLine.FindProperty("randomizeSpacing").boolValue;
            prefabLinePreset.prefabLineData.endMode = (PrefabLineCreator.EndMode)prefabLine.FindProperty("endMode").enumValueIndex;
            prefabLinePreset.prefabLineData.spacing = prefabLine.FindProperty("spacing").floatValue;
            prefabLinePreset.prefabLineData.maxSpacing = prefabLine.FindProperty("maxSpacing").floatValue;
            prefabLinePreset.prefabLineData.fillGap = prefabLine.FindProperty("fillGap").boolValue;
            prefabLinePreset.prefabLineData.deformPrefabsToCurve = prefabLine.FindProperty("deformPrefabsToCurve").boolValue;
            prefabLinePreset.prefabLineData.deformPrefabsToTerrain = prefabLine.FindProperty("deformPrefabsToTerrain").boolValue;
            prefabLinePreset.prefabLineData.yOffset = prefabLine.FindProperty("yOffset").floatValue;
            prefabLinePreset.prefabLineData.rotationDirection = (PrefabLineCreator.RotationDirection)prefabLine.FindProperty("rotationDirection").enumValueIndex;
            prefabLinePreset.prefabLineData.rotationRandomization = prefabLine.FindProperty("rotationRandomization").floatValue;
            prefabLinePreset.prefabLineData.centralYModification = prefabLine.FindProperty("centralYModification").boolValue;
            prefabLinePreset.prefabLineData.useCenterDistance = prefabLine.FindProperty("useCenterDistance").boolValue;

            // Prefabs
            prefabLinePreset.prefabLineData.startPrefab = (GameObject)prefabLine.FindProperty("startPrefab").objectReferenceValue;
            prefabLinePreset.prefabLineData.mainPrefab = (GameObject)prefabLine.FindProperty("mainPrefab").objectReferenceValue;
            prefabLinePreset.prefabLineData.endPrefab = (GameObject)prefabLine.FindProperty("endPrefab").objectReferenceValue;
            prefabLinePreset.prefabLineData.xScale = prefabLine.FindProperty("xScale").floatValue;
            prefabLinePreset.prefabLineData.yScale = new AnimationCurve(prefabLine.FindProperty("yScale").animationCurveValue.keys);
            prefabLinePreset.prefabLineData.zScale = new AnimationCurve(prefabLine.FindProperty("zScale").animationCurveValue.keys);
        }

        public static void CopyPrefabLinePresetToPrefabLineData(PrefabLinePreset prefabLinePreset, SerializedObject prefabLine)
        {
            // General
            prefabLine.FindProperty("startOffsetPercentage").floatValue = prefabLinePreset.prefabLineData.startOffsetPercentage;
            prefabLine.FindProperty("endOffsetPercentage").floatValue = prefabLinePreset.prefabLineData.endOffsetPercentage;
            prefabLine.FindProperty("randomizeSpacing").boolValue = prefabLinePreset.prefabLineData.randomizeSpacing;
            prefabLine.FindProperty("endMode").enumValueIndex = (int)prefabLinePreset.prefabLineData.endMode;
            prefabLine.FindProperty("spacing").floatValue = prefabLinePreset.prefabLineData.spacing;
            prefabLine.FindProperty("maxSpacing").floatValue = prefabLinePreset.prefabLineData.maxSpacing;
            prefabLine.FindProperty("fillGap").boolValue = prefabLinePreset.prefabLineData.fillGap;
            prefabLine.FindProperty("deformPrefabsToCurve").boolValue = prefabLinePreset.prefabLineData.deformPrefabsToCurve;
            prefabLine.FindProperty("deformPrefabsToTerrain").boolValue = prefabLinePreset.prefabLineData.deformPrefabsToTerrain;
            prefabLine.FindProperty("yOffset").floatValue = prefabLinePreset.prefabLineData.yOffset;
            prefabLine.FindProperty("rotationDirection").enumValueIndex = (int)prefabLinePreset.prefabLineData.rotationDirection;
            prefabLine.FindProperty("rotationRandomization").floatValue = prefabLinePreset.prefabLineData.rotationRandomization;
            prefabLine.FindProperty("centralYModification").boolValue = prefabLinePreset.prefabLineData.centralYModification;
            prefabLine.FindProperty("useCenterDistance").boolValue = prefabLinePreset.prefabLineData.useCenterDistance;

            // Prefabs
            prefabLine.FindProperty("startPrefab").objectReferenceValue = prefabLinePreset.prefabLineData.startPrefab;
            prefabLine.FindProperty("mainPrefab").objectReferenceValue = prefabLinePreset.prefabLineData.mainPrefab;
            prefabLine.FindProperty("endPrefab").objectReferenceValue = prefabLinePreset.prefabLineData.endPrefab;
            prefabLine.FindProperty("xScale").floatValue = prefabLinePreset.prefabLineData.xScale;
            prefabLine.FindProperty("yScale").animationCurveValue = new AnimationCurve(prefabLinePreset.prefabLineData.yScale.keys);
            prefabLine.FindProperty("zScale").animationCurveValue = new AnimationCurve(prefabLinePreset.prefabLineData.zScale.keys);
        }

        #endregion

        public static void RemoveTerrainDetails(Terrain terrain, ref HashSet<Vector2Int> detailHeights, Vector3 forward, Vector3 left, ref ComputeShader computeShader, float terrainDetailsRadius, Vector3 position)
        {
            int detailX = Mathf.RoundToInt((position.x - terrain.transform.position.x) / terrain.terrainData.size.x * terrain.terrainData.detailWidth);
            int detailZ = Mathf.RoundToInt((position.z - terrain.transform.position.z) / terrain.terrainData.size.z * terrain.terrainData.detailHeight);
            int gridSize = Mathf.RoundToInt(terrainDetailsRadius / terrain.terrainData.size.x * terrain.terrainData.detailWidth);

            ComputeBuffer positionsBuffer = new ComputeBuffer(gridSize * 6, sizeof(float) * 2, ComputeBufferType.Append);
            positionsBuffer.SetData(new Vector2Int[gridSize * 6]);
            positionsBuffer.SetCounterValue(0);
            computeShader.SetBuffer(0, "positions", positionsBuffer);

            // Set data
            computeShader.SetInts("position", detailX, detailZ);
            computeShader.SetInt("sizeX", gridSize);
            computeShader.SetInt("sizeZ", 3);
            computeShader.SetInt("textureSize", terrain.terrainData.detailWidth);
            computeShader.SetFloats("forward", forward.x, forward.z);
            computeShader.SetFloats("left", left.x, left.z);

            // Get data
            computeShader.Dispatch(0, (gridSize * 3) / 64 + 1, 1, 1);

            // Get results
            Vector2Int[] result = new Vector2Int[gridSize * 6];
            positionsBuffer.GetData(result);

            for (int k = 0; k < result.Length; k++)
            {
                if (result[k] == Vector2Int.zero)
                {
                    break;
                }

                detailHeights.Add(result[k]);
            }

            // Clean up
            positionsBuffer.Dispose();
        }

        public static void RemoveTerrainTrees(Terrain terrain, ref List<Vector3> treePositions, ref HashSet<Vector3> treesToRemove, ref ComputeShader computeShader, float terrainTreesRadius, Vector3 position)
        {
            // No trees to remove
            if (treePositions.Count == 0)
            {
                return;
            }

            // Set data
            ComputeBuffer treePositionsBuffer = new ComputeBuffer(treePositions.Count, sizeof(float) * 3);
            treePositionsBuffer.SetData(treePositions.ToArray());
            computeShader.SetBuffer(0, "treePositions", treePositionsBuffer);

            ComputeBuffer removedTreePositionsBuffer = new ComputeBuffer(treePositions.Count, sizeof(float) * 3, ComputeBufferType.Append);
            removedTreePositionsBuffer.SetData(new Vector3[treePositions.Count]);
            removedTreePositionsBuffer.SetCounterValue(0);
            computeShader.SetBuffer(0, "removedTreePositions", removedTreePositionsBuffer);

            Vector3 localPosition = position - terrain.gameObject.transform.position;
            computeShader.SetFloats("position", localPosition.x, localPosition.y, localPosition.z);
            computeShader.SetFloat("size", terrainTreesRadius);

            // Get data
            computeShader.Dispatch(0, (treePositions.Count) / 64 + 1, 1, 1);

            // Get results
            Vector3[] positionResult = new Vector3[treePositions.Count];
            removedTreePositionsBuffer.GetData(positionResult);

            for (int k = 0; k < positionResult.Length; k++)
            {
                if (positionResult[k].Equals(Vector3.zero))
                {
                    break;
                }

                if (!treesToRemove.Contains(positionResult[k]))
                {
                    treesToRemove.Add(positionResult[k]);
                }
            }

            // Clean up
            treePositionsBuffer.Dispose();
            removedTreePositionsBuffer.Dispose();
        }

        public static void ModifyHeight(float[,] originalTerrainHeights, ref float[,] terrainHeights, ref Dictionary<Vector2Int, float> terrainDistances, float terrainX, float terrainY, float terrainZ, int roundedTerrainX, int roundedTerrainZ, bool upwards, bool ignore)
        {
            Vector2Int gridPosition = new Vector2Int(roundedTerrainZ, roundedTerrainX);
            float distance = Vector2.Distance(new Vector2(terrainX, terrainZ), new Vector2(roundedTerrainX, roundedTerrainZ));

            if (!terrainDistances.ContainsKey(gridPosition))
            {
                terrainDistances.Add(gridPosition, distance);
            }

            if (!terrainDistances.ContainsKey(gridPosition) || distance < terrainDistances[gridPosition])
            {
                if (ignore)
                {
                    terrainHeights[roundedTerrainZ, roundedTerrainX] = terrainY;
                }
                else if (!upwards)
                {
                    terrainHeights[roundedTerrainZ, roundedTerrainX] = Mathf.Max(terrainY, originalTerrainHeights[roundedTerrainZ, roundedTerrainX]);
                }
                else
                {
                    terrainHeights[roundedTerrainZ, roundedTerrainX] = Mathf.Min(terrainY, originalTerrainHeights[roundedTerrainZ, roundedTerrainX]);
                }

                terrainDistances[gridPosition] = distance;
            }
        }

        public static Vector3 FindNearestPointOnLine(Vector3 start, Vector3 end, Vector3 point)
        {
            // Get heading
            Vector3 heading = (end - start);
            float magnitudeMax = heading.magnitude;
            heading.Normalize();

            // Do projection from the point but clamp it
            Vector3 lhs = point - start;
            float dotProduct = Vector3.Dot(lhs, heading);
            dotProduct = Mathf.Clamp(dotProduct, 0f, magnitudeMax);
            return start + heading * dotProduct;
        }

        public static Vector3 Lerp4(Vector3 startPoint, Vector3 endPoint, Vector3 startTangent, Vector3 endTangent, float t)
        {
            if (t == 1)
            {
                return endPoint;
            }

            return Mathf.Pow(1 - t, 3) * startPoint + 3 * t * Mathf.Pow(1 - t, 2) * startTangent + 3 * Mathf.Pow(t, 2) * (1 - t) * endTangent + Mathf.Pow(t, 3) * endPoint;
        }

        public static Vector3 Center(Vector3 one, Vector3 two)
        {
            return (one + two) / 2;
        }

        public static Vector3 GetLineIntersection(Vector3 point1, Vector3 direction1, Vector3 point2, Vector3 direction2, float maxDistance = float.MaxValue)
        {
            float originalY = (point1.y + point2.y) / 2;

            // Only check in XZ-plane
            point1.y = 0;
            direction1 = new Vector3(direction1.x, 0, direction1.z).normalized;
            point2.y = 0;
            direction2 = new Vector3(direction2.x, 0, direction2.z).normalized;

            Vector3 lineDirection = point2 - point1;
            Vector3 crossVector1and2 = Vector3.Cross(direction1, direction2);
            Vector3 crossVector3and2 = Vector3.Cross(lineDirection, direction2);

            float planarFactor = Vector3.Dot(lineDirection, crossVector1and2);

            // Is coplanar, and not parrallel
            if (Mathf.Abs(planarFactor) < 0.01f && crossVector1and2.sqrMagnitude > 0.01f)
            {
                float distance = Vector3.Dot(crossVector3and2, crossVector1and2) / crossVector1and2.sqrMagnitude;
                Vector3 point = point1 + (direction1 * distance);
                distance = Vector3.Distance(point, point1);
                float distance2 = Vector3.Distance(point, point2);
                float pointDistance = Vector3.Distance(point1, point2);

                // Check if they intersection in front of the points and not behind
                if (AlmostEqual(point2 + distance2 * direction2, point, 0.001f) && AlmostEqual(point1 + distance * direction1, point, 0.001f) && distance < pointDistance * 3 && distance2 < pointDistance * 3 && distance < maxDistance)
                {
                    return new Vector3(point.x, originalY, point.z);
                }
                else
                {
                    return MaxVector3;
                }
            }
            else
            {
                return MaxVector3;
            }
        }

        public static bool AlmostEqual(Vector3 point1, Vector3 point2, float precision)
        {
            bool equal = true;

            if (Mathf.Abs(point1.x - point2.x) > precision) equal = false;
            if (Mathf.Abs(point1.y - point2.y) > precision) equal = false;
            if (Mathf.Abs(point1.z - point2.z) > precision) equal = false;

            return equal;
        }

        public static bool IsLeft(Vector2 vector1, Vector2 vector2)
        {
            return (-vector1.x * vector2.y + vector1.y * vector2.x < 0);
        }

        public static void DisplayCurveEditor(SerializedProperty curveProperty, string name, SerializedObject settings)
        {
            curveProperty.animationCurveValue = EditorGUILayout.CurveField(name, curveProperty.animationCurveValue);

            if (settings.FindProperty("exposeCurveKeysToEditor").boolValue)
            {
                // Animationcurve has a hidden m_Curve value inside it
                for (int i = 0; i < curveProperty.FindPropertyRelative("m_Curve").arraySize; i++)
                {
                    curveProperty.FindPropertyRelative("m_Curve").GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = EditorGUILayout.FloatField("\tKey #" + (i + 1) + " Value", curveProperty.FindPropertyRelative("m_Curve").GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue);
                }
            }
        }

        public static void DisplayCurveEditor(AnimationCurve curve, string name, SerializedObject settings)
        {
            curve = EditorGUILayout.CurveField(name, curve);

            if (settings.FindProperty("exposeCurveKeysToEditor").boolValue)
            {
                // Animationcurve has a hidden m_Curve value inside it
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    Keyframe[] keys = curve.keys;

                    float value = keys[i].value;
                    value = EditorGUILayout.FloatField("\tKey #" + (i + 1) + " Value", value);
                    keys[i] = new Keyframe(curve.keys[i].time, value);

                    curve.keys = keys;
                }
            }
        }

        public static void AddLayers()
        {
            AddLayer("Road");
            AddLayer("Intersection");
            AddLayer("Prefab Line");
        }

        public static void AddLayer(string name)
        {
            // Get tag project settings
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProperty = tagManager.FindProperty("layers");

            // Check if layer exists
            for (int i = 0; i < layersProperty.arraySize; i++)
            {
                SerializedProperty layer = layersProperty.GetArrayElementAtIndex(i);

                if (layer.stringValue.Equals(name))
                {
                    return;
                }
            }

            int index = -1;
            for (int i = 8; i < 32; i++)
            {
                if (layersProperty.GetArrayElementAtIndex(i).stringValue.Length == 0)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                Debug.LogError("Layer can not be added as no empty spaces are left");
            }

            layersProperty.InsertArrayElementAtIndex(index);
            SerializedProperty newLayer = layersProperty.GetArrayElementAtIndex(index);
            newLayer.stringValue = name;
            Debug.Log("Added layer: " + name);
            tagManager.ApplyModifiedProperties();
        }

        public static void AdjustTerrain(Vector3 point, Vector3 left, int sideWidth, Terrain terrain, float terrainRadius, Vector3 terrainDataSize, int terrainResolution, Vector3 terrainPosition, float terrainAngle, float terrainExtraMaxHeight, float terrainModificationYOffset, float[,] originalTerrainHeights, ref float[,] terrainHeights, ref HashSet<Vector2Int> terrainPoints, ref HashSet<Vector2Int> finishedTerrainPoints, ref Dictionary<Vector2Int, float> terrainDistances)
        {
            // Smooth hill to neighboring terrain
            Vector3 targetPositionLeft = point + left * (sideWidth + terrainRadius + 1);

            RaycastHit raycastHit;
            if (terrain.GetComponent<TerrainCollider>().Raycast(new Ray(targetPositionLeft + Vector3.up * 50, Vector3.down), out raycastHit, 100))
            {
                targetPositionLeft = raycastHit.point;
            }
            else
            {
                targetPositionLeft -= new Vector3(0, 1, 0);
            }

            Vector3 targetPositionRight = point - left * (sideWidth + terrainRadius + 1);

            if (terrain.GetComponent<TerrainCollider>().Raycast(new Ray(targetPositionRight + Vector3.up * 50, Vector3.down), out raycastHit, 100))
            {
                targetPositionRight = raycastHit.point;
            }
            else
            {
                targetPositionRight -= new Vector3(0, 1, 0);
            }

            float worldUnitPerTerrainUnit = 1f / terrainResolution * terrainDataSize.x; // How many world unit does one terrain unit represent      
            float terrainHeightDifferencePerUnit = (1f / Mathf.Tan(Mathf.Deg2Rad * terrainAngle)) / terrainDataSize.y; // outer = nearby / tan(angle)
            Vector2 leftXZ = new Vector2(left.x, left.z);

            // World space to local terrain space
            float terrainX = (point.x - terrainPosition.x) / terrainDataSize.x * terrainResolution;
            float terrainZ = (point.z - terrainPosition.z) / terrainDataSize.z * terrainResolution;
            Vector2Int roundedTerrainPosition = new Vector2Int(Mathf.RoundToInt(terrainX), Mathf.RoundToInt(terrainZ));

            float globalStartY = point.y;
            float startY = (globalStartY - terrainPosition.y - terrainModificationYOffset) / terrainDataSize.y;
            float clampedHeight = Mathf.Tan(Mathf.Deg2Rad * terrainAngle) * (sideWidth + terrainExtraMaxHeight); // tan(angle) * nearby = outer

            // Smooth hill
            int sizeX = (int)Mathf.Abs(leftXZ.y * (terrainExtraMaxHeight + terrainRadius)) + sideWidth;
            int sizeY = (int)Mathf.Abs(leftXZ.x * (terrainExtraMaxHeight + terrainRadius)) + sideWidth;

            for (int x = -sizeX; x <= sizeX; x++)
            {
                for (int y = -sizeY; y <= sizeY; y++)
                {
                    float distanceFromCenter = Vector2.Distance(new Vector2(terrainX, terrainZ), roundedTerrainPosition + new Vector2(x, y)); // Distance to referenced height point (not rounded terrain point)

                    Vector2Int newPosition = roundedTerrainPosition + Vector2Int.up * x + Vector2Int.left * y;
                    int roundedTerrainX = newPosition.x;
                    int roundedTerrainZ = newPosition.y;
                    Vector2Int roundedTerrainXZ = new Vector2Int(roundedTerrainX, roundedTerrainZ);

                    // Outside of terrain
                    if (roundedTerrainX < 0 || roundedTerrainZ < 0 || roundedTerrainX > terrainResolution || roundedTerrainZ > terrainResolution)
                    {
                        continue;
                    }

                    // Check if points has been deformed more accurately before
                    if (terrainDistances.ContainsKey(roundedTerrainXZ) && terrainDistances[roundedTerrainXZ] < distanceFromCenter)
                    {
                        continue;
                    }

                    terrainDistances[roundedTerrainXZ] = distanceFromCenter;

                    float roundedTerrainY = 0;

                    // Slope
                    float targetY = targetPositionLeft.y;
                    if (!Utility.IsLeft(new Vector2(x, y).normalized, leftXZ))
                    {
                        targetY = targetPositionRight.y;
                    }

                    // Deform downwards
                    if (targetY < (point.y - terrainModificationYOffset))
                    {
                        roundedTerrainY = startY + clampedHeight / terrainDataSize.y - distanceFromCenter * worldUnitPerTerrainUnit * terrainHeightDifferencePerUnit;
                        roundedTerrainY = Mathf.Max(originalTerrainHeights[roundedTerrainZ, roundedTerrainX], roundedTerrainY);

                        // Clamp to create plateu
                        if (distanceFromCenter <= sideWidth + 1)
                        {
                            roundedTerrainY = Mathf.Min(roundedTerrainY, startY);
                        }
                    }
                    else
                    {
                        // Deform upwards
                        roundedTerrainY = startY - clampedHeight / terrainDataSize.y + distanceFromCenter * worldUnitPerTerrainUnit * terrainHeightDifferencePerUnit;
                        roundedTerrainY = Mathf.Min(originalTerrainHeights[roundedTerrainZ, roundedTerrainX], roundedTerrainY);

                        // Clamp to create plateu
                        if (distanceFromCenter <= sideWidth + 1)
                        {
                            roundedTerrainY = Mathf.Max(roundedTerrainY, startY);
                        }
                    }

                    // Add to smoothing list
                    if (!finishedTerrainPoints.Contains(roundedTerrainXZ) && roundedTerrainY != originalTerrainHeights[roundedTerrainZ, roundedTerrainX])
                    {
                        if (distanceFromCenter > sideWidth)
                        {
                            terrainPoints.Add(roundedTerrainXZ);
                        }
                        else
                        {
                            finishedTerrainPoints.Add(roundedTerrainXZ);
                            terrainPoints.Remove(roundedTerrainXZ); // Remove if added before
                        }
                    }

                    // Terrain coordinates to world coordinates
                    float globalX = (float)roundedTerrainX / terrainResolution * terrainDataSize.x + terrainPosition.x;
                    float globalZ = (float)roundedTerrainZ / terrainResolution * terrainDataSize.z + terrainPosition.z;

                    // Adapt to road/intersection above point
                    if (Physics.BoxCast(new Vector3(globalX, point.y, globalZ) + Vector3.up * 50, new Vector3(0.05f, 0, 0.05f), Vector3.down, out raycastHit, Quaternion.identity, 100, (1 << LayerMask.NameToLayer("Road")) | (1 << LayerMask.NameToLayer("Intersection"))))
                    {
                        roundedTerrainY = (raycastHit.point.y - terrainPosition.y - terrainModificationYOffset) / terrainDataSize.y;
                    }

                    if (roundedTerrainY >= 0 && roundedTerrainY <= 1)
                    {
                        terrainHeights[roundedTerrainZ, roundedTerrainX] = roundedTerrainY;
                    }
                }
            }
        }

        public static void SmoothTerrain(ComputeShader terrainSmoothShader, int terrainResolution, int terrainSmoothingRadius, float terrainSmoothingAmount, ref float[,] terrainHeights, HashSet<Vector2Int> terrainPoints)
        {
            if (terrainSmoothingRadius == 0)
            {
                return;
            }

            if (terrainPoints.Count == 0)
            {
                return;
            }

            // Pass data to shader
            ComputeBuffer positionsBuffer = new ComputeBuffer(terrainPoints.Count, sizeof(int) * 2);
            Vector2Int[] positionsArray = new Vector2Int[terrainPoints.Count];
            terrainPoints.CopyTo(positionsArray);
            positionsBuffer.SetData(positionsArray);
            terrainSmoothShader.SetBuffer(0, "positions", positionsBuffer);

            ComputeBuffer heightsBuffer = new ComputeBuffer(terrainHeights.GetLength(0) * terrainHeights.GetLength(1), sizeof(float));
            heightsBuffer.SetData(terrainHeights);
            terrainSmoothShader.SetBuffer(0, "heights", heightsBuffer);
            terrainSmoothShader.SetInt("terrainSize", terrainResolution + 1);
            terrainSmoothShader.SetInt("smoothRadius", terrainSmoothingRadius);
            terrainSmoothShader.SetFloat("smoothingAmount", terrainSmoothingAmount);

            // Result buffers
            ComputeBuffer resultHeightsBuffer = new ComputeBuffer(terrainPoints.Count, sizeof(float));
            terrainSmoothShader.SetBuffer(0, "resultHeights", resultHeightsBuffer);

            // Call shader
            terrainSmoothShader.Dispatch(0, terrainPoints.Count / 64 + 1, 1, 1);

            // Get results
            float[] returnedHeights = new float[terrainPoints.Count];
            resultHeightsBuffer.GetData(returnedHeights);

            for (int i = 0; i < positionsArray.Length; i++)
            {
                terrainHeights[positionsArray[i].y, positionsArray[i].x] = returnedHeights[i];
            }

            // Clean up
            positionsBuffer.Dispose();
            heightsBuffer.Dispose();
            resultHeightsBuffer.Dispose();
        }

        public static void SetRoadSystemParent(Transform transform)
        {
            if (transform.parent != null && transform.parent.GetComponent<RoadSystem>() != null)
            {
                return;
            }

            RoadSystem roadSystem;
            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<RoadSystem>() != null)
            {
                roadSystem = Selection.activeGameObject.GetComponent<RoadSystem>();
            }
            else
            {
                roadSystem = GameObject.FindObjectOfType<RoadSystem>();
            }

            if (roadSystem == null)
            {
                GameObject newSystem = new GameObject("Road System");
                newSystem.AddComponent<RoadSystem>();
                transform.SetParent(newSystem.transform);
            }
            else
            {
                transform.SetParent(roadSystem.transform);
            }

            Selection.activeGameObject = roadSystem.gameObject;
        }

        public static void AddCollidableMeshAndOtherComponents(ref GameObject gameObject, List<System.Type> components)
        {
            components.Add(typeof(MeshCollider));
            AddMeshAndOtherComponents(ref gameObject, components);
        }

        public static void AddMeshAndOtherComponents(ref GameObject gameObject, List<System.Type> components)
        {
            components.AddRange(new System.Type[] { typeof(MeshFilter), typeof(MeshRenderer) });
            AddComponents(ref gameObject, components);
        }

        public static void AddComponents(ref GameObject gameObject, List<System.Type> components)
        {
            foreach (System.Type component in components)
            {
                gameObject.AddComponent(component);
            }
        }

        public static float GetCurveLenth(Vector3 startPoint, Vector3 endPoint, Vector3 startTangent, Vector3 endTangent, bool xz)
        {
            Vector3[] points = Handles.MakeBezierPoints(startPoint, endPoint, startTangent, endTangent, (int)(Vector3.Distance(startPoint, endPoint) * 1.5f));

            // Calculate distance between points
            float distance = 0;
            for (int j = 0; j < points.Length - 1; j++)
            {
                if (xz)
                {
                    distance += Vector2.Distance(new Vector2(points[j].x, points[j].z), new Vector2(points[j + 1].x, points[j + 1].z));
                }
                else
                {
                    distance += Vector3.Distance(points[j], points[j + 1]);
                }
            }

            return distance;
        }

        public static Vector3 CalculateLeft(Vector3 forward)
        {
            return new Vector3(-forward.z, 0, forward.x).normalized;
        }

        public static void ShowListInspector<T>(string title, ref List<T> list) where T : Object
        {
            DrawUILine(Color.black, 2);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            int size = list.Count;
            size = Mathf.Max(0, EditorGUILayout.IntField("Count", size));

            // Increase list size
            while (size > list.Count)
            {
                list.Add(null);
            }

            // Decrease list size
            while (size < list.Count)
            {
                list.RemoveAt(list.Count - 1);
            }

            // Show all elements
            for (int i = 0; i < size; i++)
            {
                list[i] = (T)EditorGUILayout.ObjectField("Element #" + (i + 1), list[i], typeof(T), false);
            }
            DrawUILine(Color.black, 2);
        }

        // Source: https://forum.unity.com/threads/horizontal-line-in-editor-window.520812/
        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            rect.height = thickness;
            rect.y += padding / 2;
            rect.x -= 2;
            rect.width += 6;
            EditorGUI.DrawRect(rect, color);
        }
    }
}
#endif
