using UnityEditor;
using UnityEngine;


namespace RoadCreatorPro
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PointSystemCreator))]
    public class PointSystemEditor : Editor
    {
        protected int lastPointIndex = -1; // Including control points
        protected int currentMovingPointIndex = -1; // Not including control points
        protected bool sDown = false;

        protected void OnEnable()
        {

        }

        public void OnSceneGUI()
        {
            Event currentEvent = Event.current;
            PointSystemCreator pointSystem = (PointSystemCreator)target;

            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(pointSystem.gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(pointSystem) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            RoadCreator road = pointSystem.GetComponent<RoadCreator>();
            PrefabLineCreator prefabLine = pointSystem.GetComponent<PrefabLineCreator>();

            if (prefabLine == null || !prefabLine.controlled)
            {
                GetCurrentPointIndex(pointSystem);

                if (currentEvent.type == EventType.Layout)
                {
                    if (currentEvent.shift)
                    {
                        HandleUtility.AddDefaultControl(0);
                    }

                    AddHandles(pointSystem);
                }

                if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.AddPoints)
                {
                    AddPoints(pointSystem, currentEvent);
                }
                if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.InsertPoints)
                {
                    InsertPoints(pointSystem, currentEvent);
                }
                else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.DeletePoints)
                {
                    DeletePoints(pointSystem, currentEvent);
                }
                else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.SplitRoad)
                {
                    SplitRoad(pointSystem, currentEvent);
                }
                else
                {
                    DefaultAction(pointSystem, currentEvent);
                }

                // Needs both mouse events, repaint event etc
                DrawObject(pointSystem, currentEvent);

                // Prevent scaling and rotation
                if (pointSystem.transform.hasChanged)
                {
                    pointSystem.transform.hasChanged = false;
                    pointSystem.transform.localRotation = Quaternion.identity;
                    pointSystem.transform.localScale = Vector3.one;
                }
            }
        }

        private void AddPoints(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    // Select point
                    if (!currentEvent.shift)
                    {
                        if (lastPointIndex % 3 == 0 && GetIdForIndex(pointSystem, lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                        {
                            pointSystem.addedPoint = pointSystem.transform.GetChild(0).GetChild(lastPointIndex / 3).GetComponent<Point>();
                        }
                    }
                    else
                    {
                        // Add point
                        if (pointSystem.addedPoint != null)
                        {
                            Point newPoint = pointSystem.CreatePoint(currentEvent, (pointSystem.addedPoint.transform.GetSiblingIndex() == 0 && pointSystem.transform.GetChild(0).childCount > 1) ? true : false);
                            pointSystem.addedPoint = newPoint; // Can be null
                        }
                        else if (pointSystem.GetComponent<PrefabLineCreator>() != null)
                        {
                            // Create first point for prefab lines
                            PrefabLineCreator prefabLine = pointSystem.GetComponent<PrefabLineCreator>();
                            Point newPoint = prefabLine.CreatePoint(currentEvent, false);
                            pointSystem.addedPoint = newPoint; // Can be null
                        }
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    pointSystem.addedPoint = null;
                }
            }
        }

        private void InsertPoints(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    // Select point
                    if (currentEvent.shift)
                    {
                        int closestIndex = 0;
                        float closestDistance = float.MaxValue;
                        Vector3 mousePosition = Utility.GetMousePosition(false, true);

                        // Find nearest point by getting the distance to every bezier
                        for (int i = 0; i < pointSystem.transform.GetChild(0).childCount - 1; i++)
                        {
                            float distance = HandleUtility.DistancePointBezier(mousePosition, pointSystem.transform.GetChild(0).GetChild(i).position, pointSystem.transform.GetChild(0).GetChild(i + 1).position, pointSystem.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), pointSystem.transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint());

                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestIndex = i;
                            }
                        }

                        pointSystem.InsertPoint(closestIndex);
                        pointSystem.Regenerate();
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    pointSystem.addedPoint = null;
                }
            }
        }

        private void DeletePoints(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0 && currentEvent.shift)
                {
                    if (lastPointIndex % 3 == 0 && GetIdForIndex(pointSystem, lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        pointSystem.RemovePoint(currentEvent, lastPointIndex / 3);
                        RemoveIdForIndex(pointSystem, lastPointIndex / 3);
                        lastPointIndex = -1;
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                }
            }
        }

        private void SplitRoad(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    if (GetIdForIndex(pointSystem, lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        // Only main points
                        if (lastPointIndex % 3 == 0)
                        {
                            pointSystem.SplitSegment(lastPointIndex / 3, false);
                        }

                        GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                }
            }
        }

        private void DefaultAction(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                // Start moving point
                if (currentMovingPointIndex == -1)
                {
                    if (GetIdForIndex(pointSystem, lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        // Only main points
                        if (lastPointIndex % 3 == 0)
                        {
                            currentMovingPointIndex = lastPointIndex / 3;
                        }
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseDrag)
            {
                MovePoint(currentEvent, pointSystem);
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                if (currentMovingPointIndex != -1)
                {
                    currentMovingPointIndex = -1;
                    pointSystem.Regenerate(false);
                }
            }
        }

        private void DrawObject(PointSystemCreator pointSystem, Event currentEvent)
        {
            if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.AddPoints)
            {
                Vector3 mousePosition = Utility.GetMousePosition(false, false);

                DrawAddingPoint(pointSystem, currentEvent);

                if (currentEvent.shift && pointSystem.addedPoint != null)
                {
                    // Draw line between points
                    Handles.color = Color.black;
                    Handles.DrawLine(pointSystem.addedPoint.transform.position, mousePosition);
                }
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.InsertPoints)
            {
                DrawAddingPoint(pointSystem, currentEvent);
            }

            if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.SplitRoad)
            {
                Draw(pointSystem, currentEvent, RoadRenderingMode.SplitRoad);
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.AddPoints)
            {
                Draw(pointSystem, currentEvent, RoadRenderingMode.Add);
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.InsertPoints)
            {
                Draw(pointSystem, currentEvent, RoadRenderingMode.Insert);
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.DeletePoints)
            {
                Draw(pointSystem, currentEvent, RoadRenderingMode.Delete);
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.None)
            {
                Draw(pointSystem, currentEvent, RoadRenderingMode.Normal);
            }
        }

        public enum RoadRenderingMode { Normal, SplitRoad, Add, Insert, Delete };

        private void DrawAddingPoint(PointSystemCreator pointSystem, Event currentEvent)
        {
            Vector3 mousePosition = Utility.GetMousePosition(false, false);

            if (currentEvent.shift)
            {
                // Draw point at cursor
                Handles.color = pointSystem.settings.FindProperty("selectedAnchorPointColour").colorValue;
                Handles.CapFunction shape = GetPointShape(pointSystem);
                float handleSize = pointSystem.settings.FindProperty("anchorPointSize").floatValue;
                shape(0, mousePosition, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);
            }
        }

        private Handles.CapFunction GetPointShape(PointSystemCreator pointSystem)
        {
            Handles.CapFunction shape;
            int shapeIndex = pointSystem.settings.FindProperty("pointShape").enumValueIndex;
            if (shapeIndex == 0)
            {
                shape = Handles.CylinderHandleCap;
            }
            else if (shapeIndex == 1)
            {
                shape = Handles.SphereHandleCap;
            }
            else if (shapeIndex == 2)
            {
                shape = Handles.CubeHandleCap;
            }
            else
            {
                shape = Handles.ConeHandleCap;
            }

            return shape;
        }

        private void AddHandles(PointSystemCreator pointSystem)
        {
            for (int i = 0; i < pointSystem.transform.GetChild(0).childCount; i++)
            {
                float distance = HandleUtility.DistanceToCircle(pointSystem.transform.GetChild(0).GetChild(i).position, pointSystem.settings.FindProperty("anchorPointSize").floatValue / 2);
                HandleUtility.AddControl(GetIdForIndex(pointSystem, i, 0), distance);

                if (i > 0)
                {
                    distance = HandleUtility.DistanceToCircle(pointSystem.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetLeftLocalControlPoint(), pointSystem.settings.FindProperty("controlPointSize").floatValue / 2);
                    HandleUtility.AddControl(GetIdForIndex(pointSystem, i, 1), distance);
                }

                if (i < pointSystem.transform.GetChild(0).childCount - 1)
                {
                    distance = HandleUtility.DistanceToCircle(pointSystem.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), pointSystem.settings.FindProperty("controlPointSize").floatValue / 2);
                    HandleUtility.AddControl(GetIdForIndex(pointSystem, i, 2), distance);
                }
            }
        }

        protected void GetCurrentPointIndex(PointSystemCreator pointSystem)
        {
            // Prevent focus switching to over points when moving
            if (currentMovingPointIndex == -1 && GUIUtility.hotControl == 0 && pointSystem.handleIds.Contains(HandleUtility.nearestControl))
            {
                lastPointIndex = pointSystem.handleIds.IndexOf(HandleUtility.nearestControl);

                pointSystem.currentPoint = pointSystem.transform.GetChild(0).GetChild(lastPointIndex / 3);
                pointSystem.currentPointTypeIndex = lastPointIndex % 3;
            }
        }

        protected void Draw(PointSystemCreator pointSystem, Event currentEvent, RoadRenderingMode roadRenderingMode)
        {
            Vector3 screenMousePosition = currentEvent.mousePosition;
            screenMousePosition.z = 0;

            PrefabLineCreator prefabLine = pointSystem.GetComponent<PrefabLineCreator>();

            for (int i = 0; i < pointSystem.transform.GetChild(0).childCount; i++)
            {
                Transform point = pointSystem.transform.GetChild(0).GetChild(i);
                Point localControlPoint = pointSystem.transform.GetChild(0).GetChild(i).GetComponent<Point>();

                // Only draw start/end points
                if (roadRenderingMode == RoadRenderingMode.Add && i > 0 && i < pointSystem.transform.GetChild(0).childCount - 1)
                {
                    continue;
                }

                // Don't draw start or end points
                if (roadRenderingMode == RoadRenderingMode.SplitRoad && (i == 0 || i == pointSystem.transform.GetChild(0).childCount - 1))
                {
                    continue;
                }

                #region Draw Points

                // Main points
                Vector3 screenPosition = HandleUtility.WorldToGUIPoint(pointSystem.transform.GetChild(0).GetChild(i).position);
                screenPosition.z = 0;

                if (roadRenderingMode == RoadRenderingMode.Add && pointSystem.addedPoint == localControlPoint)
                {
                    Handles.color = pointSystem.settings.FindProperty("selectedPointColour").colorValue;
                }
                else
                {
                    if (HandleUtility.nearestControl == GetIdForIndex(pointSystem, i, 0))
                    {
                        Handles.color = pointSystem.settings.FindProperty("selectedAnchorPointColour").colorValue;
                    }
                    else
                    {
                        Handles.color = pointSystem.settings.FindProperty("anchorPointColour").colorValue;
                    }
                }

                Handles.CapFunction shape = GetPointShape(pointSystem);

                float handleSize = pointSystem.settings.FindProperty("anchorPointSize").floatValue;
                handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(point.position) * handleSize);
                shape(GetIdForIndex(pointSystem, i, 0), point.position, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                // Calculate handle rotation
                Vector3 lookDirection = localControlPoint.leftLocalControlPointPosition.normalized;
                lookDirection.y = 0;
                Quaternion handleRotation = Quaternion.LookRotation(lookDirection);

                if (Tools.pivotRotation == PivotRotation.Global)
                {
                    handleRotation = Quaternion.identity;
                }

                // Don't draw first point for cyclic road
                if (lastPointIndex == i * 3 && currentMovingPointIndex == -1 && (pointSystem.transform.GetChild(0).childCount <= 3 || i > 0))
                {
                    Undo.RecordObject(point, "Move Point");
                    EditorGUI.BeginChangeCheck();
                    point.position = Utility.DrawPositionHandle(pointSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, pointSystem.settings.FindProperty("anchorPointSize").floatValue, point.position + Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue;

                    if (EditorGUI.EndChangeCheck())
                    {
                        pointSystem.Regenerate(false);
                    }
                }

                // Control points
                if (roadRenderingMode == RoadRenderingMode.Normal)
                {
                    Handles.color = pointSystem.settings.FindProperty("controlPointColour").colorValue;

                    if (i > 0)
                    {
                        handleSize = pointSystem.settings.FindProperty("controlPointSize").floatValue;
                        handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(localControlPoint.GetLeftLocalControlPoint()) * handleSize);
                        shape(GetIdForIndex(pointSystem, i, 1), localControlPoint.GetLeftLocalControlPoint(), Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                        if (lastPointIndex == i * 3 + 1 && currentMovingPointIndex == -1)
                        {
                            Undo.RecordObject(localControlPoint, "Move Point");
                            EditorGUI.BeginChangeCheck();
                            localControlPoint.leftLocalControlPointPosition = Utility.DrawPositionHandle(pointSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, pointSystem.settings.FindProperty("controlPointSize").floatValue, localControlPoint.GetLeftLocalControlPoint() + Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - point.position - Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue;

                            if (EditorGUI.EndChangeCheck())
                            {
                                if (GlobalRoadSystemSettings.yLock)
                                {
                                    localControlPoint.leftLocalControlPointPosition.y = 0;
                                }

                                // Change corresponding control point
                                if (!GlobalRoadSystemSettings.movePointsIndividually)
                                {
                                    float distance = localControlPoint.rightLocalControlPointPosition.magnitude;
                                    localControlPoint.rightLocalControlPointPosition = (-localControlPoint.leftLocalControlPointPosition).normalized * distance;
                                }

                                pointSystem.Regenerate(false);
                            }
                        }
                    }

                    if (i < pointSystem.transform.GetChild(0).childCount - 1)
                    {
                        handleSize = pointSystem.settings.FindProperty("controlPointSize").floatValue;
                        handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(localControlPoint.GetRightLocalControlPoint()) * handleSize);
                        shape(GetIdForIndex(pointSystem, i, 2), localControlPoint.GetRightLocalControlPoint(), Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                        if (lastPointIndex == i * 3 + 2 && currentMovingPointIndex == -1)
                        {
                            Undo.RecordObject(localControlPoint, "Move Point");
                            EditorGUI.BeginChangeCheck();
                            localControlPoint.rightLocalControlPointPosition = Utility.DrawPositionHandle(pointSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, pointSystem.settings.FindProperty("controlPointSize").floatValue, localControlPoint.GetRightLocalControlPoint() + Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - point.position - Vector3.up * pointSystem.settings.FindProperty("anchorPointSize").floatValue;

                            if (EditorGUI.EndChangeCheck())
                            {
                                if (GlobalRoadSystemSettings.yLock)
                                {
                                    localControlPoint.rightLocalControlPointPosition.y = 0;
                                }

                                // Change corresponding control point
                                if (!GlobalRoadSystemSettings.movePointsIndividually)
                                {
                                    float distance = localControlPoint.leftLocalControlPointPosition.magnitude;
                                    localControlPoint.leftLocalControlPointPosition = (-localControlPoint.rightLocalControlPointPosition).normalized * distance;
                                }

                                pointSystem.Regenerate(false);
                            }
                        }
                    }

                    #endregion

                    #region Draw Lines
                    Handles.color = Color.white;

                    if (i > 0)
                    {
                        Handles.DrawLine(point.position, localControlPoint.GetLeftLocalControlPoint());
                    }

                    if (i < pointSystem.transform.GetChild(0).childCount - 1)
                    {
                        Handles.DrawLine(point.position, localControlPoint.GetRightLocalControlPoint());
                    }

                    if (i < pointSystem.transform.GetChild(0).childCount - 1)
                    {
                        Handles.DrawBezier(point.transform.position, pointSystem.transform.GetChild(0).GetChild(i + 1).position, localControlPoint.GetRightLocalControlPoint(), pointSystem.transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint(), Color.black, null, 3f);
                    }

                    #endregion
                }
            }

            SceneView.lastActiveSceneView.Repaint();
        }

        private void MovePoint(Event currentEvent, PointSystemCreator pointSystem)
        {
            if (currentMovingPointIndex != -1)
            {
                Undo.RecordObject(pointSystem.transform.GetChild(0).GetChild(currentMovingPointIndex), "Move Point");
                pointSystem.transform.GetChild(0).GetChild(currentMovingPointIndex).position = Utility.GetMousePosition(false, true);
            }
        }

        #region Handle Ids

        private void RemoveIdForIndex(PointSystemCreator pointSystem, int index)
        {
            pointSystem.handleHashes.RemoveAt(index * 3);
            pointSystem.handleIds.RemoveAt(index * 3);
            pointSystem.handleHashes.RemoveAt(index * 3);
            pointSystem.handleIds.RemoveAt(index * 3);
            pointSystem.handleHashes.RemoveAt(index * 3);
            pointSystem.handleIds.RemoveAt(index * 3);
        }

        // Points 0:Main point 1:Left control point 2:Right control point
        private int GetIdForIndex(PointSystemCreator pointSystem, int index, int point)
        {
            if (point == 1)
            {
                return pointSystem.handleIds[index * 3 + 1];
            }
            else if (point == 2)
            {
                return pointSystem.handleIds[index * 3 + 2];
            }
            else
            {
                while (pointSystem.handleIds.Count <= index * 3)
                {
                    AddId(pointSystem);
                }

                return pointSystem.handleIds[index * 3];
            }
        }

        private void AddId(PointSystemCreator pointSystem)
        {
            // Main points
            int hash = ("PointHandle" + pointSystem.GetInstanceID() + pointSystem.lastHashIndex).GetHashCode();
            pointSystem.handleHashes.Add(hash);
            int id = GUIUtility.GetControlID(hash, FocusType.Passive);
            pointSystem.handleIds.Add(id);

            // Left control point
            hash = ("PointHandle" + pointSystem.GetInstanceID() + pointSystem.lastHashIndex + 1).GetHashCode();
            pointSystem.handleHashes.Add(hash);
            id = GUIUtility.GetControlID(hash, FocusType.Passive);
            pointSystem.handleIds.Add(id);

            // Right control point
            hash = ("PointHandle" + pointSystem.GetInstanceID() + pointSystem.lastHashIndex + 2).GetHashCode();
            pointSystem.handleHashes.Add(hash);
            id = GUIUtility.GetControlID(hash, FocusType.Passive);
            pointSystem.handleIds.Add(id);

            pointSystem.lastHashIndex += 3;
        }

        #endregion
    }
}
