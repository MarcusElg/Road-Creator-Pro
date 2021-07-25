using UnityEditor;
using UnityEngine;


namespace RoadCreatorPro
{
    [CustomEditor(typeof(RoadSystem))]
    public class RoadSystemEditor : Editor
    {
        private int currentMovingPointIndex = -1; // Not including control points
        private RoadCreator currentMovingPointRoad = null;

        private void OnEnable()
        {
            Tools.current = Tool.None;
            Undo.undoRedoPerformed += UndoSystem;

            RoadSystem roadSystem = (RoadSystem)target;
            roadSystem.settings = RoadCreatorSettings.GetSerializedSettings();
            roadSystem.roads.Clear();
            roadSystem.intersections.Clear();
            roadSystem.ClearAction();

            // Reset current action as it isn't rendered (unless it has been immediately created)
            if (roadSystem.transform.childCount > 0)
            {
                GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
            }
        }

        private void OnDisable()
        {
            Tools.current = Tool.Move;
            Undo.undoRedoPerformed -= UndoSystem;

            RoadSystem roadSystem = (RoadSystem)target;
            // Disselect object
            if (roadSystem.selectedObject != null)
            {
                HideSelectedObject(roadSystem);
                roadSystem.selectedObject = null;
            }
        }

        private void UndoSystem()
        {
            RoadSystem roadSystem = (RoadSystem)target;

            // Regenerate everything as it isn't possible to tell which objects have been undoed
            if (roadSystem != null)
            {
                foreach (RoadCreator road in roadSystem.roads)
                {
                    if (road != null)
                    {
                        road.Regenerate();
                    }
                }

                foreach (Intersection intersection in roadSystem.intersections)
                {
                    if (intersection != null)
                    {
                        intersection.Regenerate(true);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            Event currentEvent = Event.current;
            RoadSystem roadSystem = (RoadSystem)target;

            HandleUtility.AddDefaultControl(0);

            // Remove null objects
            roadSystem.roads.RemoveWhere(item => item == null);
            roadSystem.intersections.RemoveWhere(item => item == null);

            foreach (RoadCreator road in roadSystem.roads)
            {
                GetCurrentPointIndex(road);
            }

            if (currentEvent.type == EventType.Layout)
            {
                if (currentEvent.shift)
                {
                    HandleUtility.AddDefaultControl(0);
                }

                foreach (RoadCreator road in roadSystem.roads)
                {
                    AddRoadHandles(roadSystem, road);
                }

                // Add intersection handles
                if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Connect && roadSystem.intersectionConnectionPoint != null)
                {
                    foreach (Intersection intersection in roadSystem.intersections)
                    {
                        float distance = HandleUtility.DistanceToCircle(intersection.transform.position, roadSystem.settings.FindProperty("anchorPointSize").floatValue / 2);
                        HandleUtility.AddControl(GetIntersectionID(intersection), distance);
                    }
                }
            }

            switch (GlobalRoadSystemSettings.currentAction)
            {
                case GlobalRoadSystemSettings.Action.Create:
                    {
                        CreateIntersection(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.Disconnect:
                    {
                        DisconnectFromIntersection(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.Connect:
                    {
                        ConnectToIntersection(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.CreateRoad:
                    {
                        CreateRoad(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.AddPoints:
                    {
                        AddPoints(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.InsertPoints:
                    {
                        InsertPoints(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.DeletePoints:
                    {
                        DeletePoints(roadSystem, currentEvent);
                        break;
                    }
                case GlobalRoadSystemSettings.Action.SplitRoad:
                    {
                        SplitRoad(roadSystem, currentEvent);
                        break;
                    }
                default:
                    {
                        DefaultAction(roadSystem, currentEvent);
                        break;
                    }
            }

            Draw(roadSystem, currentEvent);

            // Prevent scaling and rotation
            if (roadSystem != null && roadSystem.transform.hasChanged)
            {
                roadSystem.transform.hasChanged = false;
                roadSystem.transform.localRotation = Quaternion.identity;
                roadSystem.transform.localScale = Vector3.one;
            }
        }

        private void DefaultAction(RoadSystem roadSystem, Event currentEvent)
        {
            // Select road/intersection
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                if (HandleUtility.nearestControl == 0)
                {
                    SelectObject(roadSystem);
                }

                // Start moving point
                if (currentMovingPointIndex == -1)
                {
                    if (roadSystem.lastPointRoad != null && GetIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        // Only main points
                        if (roadSystem.lastPointIndex % 3 == 0)
                        {
                            currentMovingPointIndex = roadSystem.lastPointIndex / 3;
                            currentMovingPointRoad = roadSystem.lastPointRoad;
                        }
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseDrag)
            {
                MovePoint(currentEvent, currentMovingPointRoad);
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                if (currentMovingPointIndex != -1)
                {
                    currentMovingPointIndex = -1;
                    currentMovingPointRoad.Regenerate(false);
                    currentMovingPointRoad = null;
                }
            }
        }

        private void MovePoint(Event currentEvent, RoadCreator road)
        {
            if (currentMovingPointIndex != -1)
            {
                Undo.RecordObject(road.transform.GetChild(0).GetChild(currentMovingPointIndex), "Move Point");
                road.transform.GetChild(0).GetChild(currentMovingPointIndex).position = Utility.GetMousePosition(false, true);
            }
        }

        private void CreateIntersection(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    if (roadSystem.lastPointRoad != null && GetIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        Point point = roadSystem.lastPointRoad.transform.GetChild(0).GetChild(roadSystem.lastPointIndex / 3).GetComponent<Point>();

                        if (!roadSystem.currentlyCreatedIntersectionPoints.Contains(point))
                        {
                            roadSystem.currentlyCreatedIntersectionPoints.Add(point);

                            // Create intersection
                            // If three points or the second one is a one that will be split
                            if (roadSystem.currentlyCreatedIntersectionPoints.Count > 1)
                            {
                                if (roadSystem.currentlyCreatedIntersectionPoints.Count == 3)
                                {
                                    roadSystem.currentlyCreatedIntersectionPoints[0].transform.parent.parent.GetComponent<RoadCreator>().CreateIntersection(roadSystem.currentlyCreatedIntersectionPoints[0], roadSystem.currentlyCreatedIntersectionPoints[1], roadSystem.currentlyCreatedIntersectionPoints[2], roadSystem.currentlyCreatedIntersectionPoints[1].transform.position);
                                }
                                else if (point.transform.GetSiblingIndex() > 0 && point.transform.GetSiblingIndex() < point.transform.parent.childCount - 1)
                                {
                                    roadSystem.currentlyCreatedIntersectionPoints[0].transform.parent.parent.GetComponent<RoadCreator>().CreateIntersection(roadSystem.currentlyCreatedIntersectionPoints[0], roadSystem.currentlyCreatedIntersectionPoints[1], null, roadSystem.currentlyCreatedIntersectionPoints[1].transform.position);
                                }
                                else
                                {
                                    // Just add second point
                                    return;
                                }

                                roadSystem.currentlyCreatedIntersectionPoints.Clear();
                                GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                            }
                        }
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    roadSystem.currentlyCreatedIntersectionPoints.Clear();
                }
            }
        }

        private void ConnectToIntersection(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    // Select road point
                    if (roadSystem.intersectionConnectionPoint == null)
                    {
                        if (roadSystem.lastPointIndex > -1 && roadSystem.lastPointIndex % 3 == 0)
                        {
                            roadSystem.intersectionConnectionPoint = roadSystem.lastPointRoad.transform.GetChild(0).GetChild(roadSystem.lastPointIndex / 3).GetComponent<Point>();
                        }
                    }
                    else
                    {
                        // Check if intersection point is clicked
                        foreach (Intersection intersection in roadSystem.intersections)
                        {
                            if (GetIntersectionID(intersection) == HandleUtility.nearestControl)
                            {
                                RoadCreator road = roadSystem.intersectionConnectionPoint.transform.parent.parent.GetComponent<RoadCreator>();
                                road.ConnectToIntersection(roadSystem.intersectionConnectionPoint, intersection);
                                GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                                roadSystem.intersectionConnectionPoint = null;
                                break;
                            }
                        }
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    roadSystem.intersectionConnectionPoint = null;
                }
            }
        }

        private void DisconnectFromIntersection(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    if (roadSystem.lastPointIndex != -1 && roadSystem.lastPointIndex % 3 == 0)
                    {
                        // Disconnect point
                        Point point = roadSystem.lastPointRoad.transform.GetChild(0).GetChild(roadSystem.lastPointIndex / 3).GetComponent<Point>();
                        roadSystem.lastPointRoad.DisconnectFromIntersection(point);
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

        private void SplitRoad(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    if (roadSystem.lastPointRoad != null && GetIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        // Only main points
                        if (roadSystem.lastPointIndex % 3 == 0)
                        {
                            roadSystem.lastPointRoad.SplitSegment(roadSystem.lastPointIndex / 3, false);
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

        private void AddRoadHandles(RoadSystem roadSystem, RoadCreator road)
        {
            for (int i = 0; i < road.transform.GetChild(0).childCount; i++)
            {
                float distance = HandleUtility.DistanceToCircle(road.transform.GetChild(0).GetChild(i).position, roadSystem.settings.FindProperty("anchorPointSize").floatValue / 2);
                HandleUtility.AddControl(GetIdForIndex(road, i, 0), distance);

                if (i > 0)
                {
                    distance = HandleUtility.DistanceToCircle(road.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetLeftLocalControlPoint(), roadSystem.settings.FindProperty("controlPointSize").floatValue / 2);
                    HandleUtility.AddControl(GetIdForIndex(road, i, 1), distance);
                }

                if (i < road.transform.GetChild(0).childCount - 1)
                {
                    distance = HandleUtility.DistanceToCircle(road.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), roadSystem.settings.FindProperty("controlPointSize").floatValue / 2);
                    HandleUtility.AddControl(GetIdForIndex(road, i, 2), distance);
                }
            }
        }

        private void CreateRoad(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    // First point
                    if (roadSystem.currentlyCreatedRoad == null)
                    {
                        GameObject road = new GameObject("Road");
                        Undo.RegisterCreatedObjectUndo(road, "Create road");
                        road.AddComponent<RoadCreator>();
                        road.GetComponent<RoadCreator>().InitializeSystem();
                        road.GetComponent<RoadCreator>().CreatePoint(currentEvent, false, false);
                        road.transform.parent = roadSystem.transform;

                        roadSystem.currentlyCreatedRoad = road.GetComponent<RoadCreator>();
                        roadSystem.selectedObject = road;
                    }
                    else
                    {
                        // Second point
                        roadSystem.currentlyCreatedRoad.CreatePoint(currentEvent);
                        roadSystem.currentlyCreatedRoad.Regenerate();

                        // Select created road
                        HideSelectedObject(roadSystem);
                        roadSystem.selectedObject = roadSystem.currentlyCreatedRoad.gameObject;
                        ShowSelectedObject(roadSystem);
                        GetNearbyPoints(roadSystem.currentlyCreatedRoad.gameObject);

                        // Continue adding points
                        GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.AddPoints;
                        roadSystem.addedPoint = roadSystem.currentlyCreatedRoad.transform.GetChild(0).GetChild(roadSystem.currentlyCreatedRoad.transform.GetChild(0).childCount - 1).GetComponent<Point>();

                        roadSystem.currentlyCreatedRoad = null;
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;

                    if (roadSystem.currentlyCreatedRoad != null)
                    {
                        DestroyImmediate(roadSystem.currentlyCreatedRoad.gameObject);
                    }

                    roadSystem.currentlyCreatedRoad = null;
                }
            }
        }

        private void AddPoints(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0)
                {
                    // Select point
                    if (!currentEvent.shift)
                    {
                        if (roadSystem.lastPointRoad != null && roadSystem.lastPointIndex % 3 == 0 && GetIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                        {
                            roadSystem.addedPoint = roadSystem.lastPointRoad.transform.GetChild(0).GetChild(roadSystem.lastPointIndex / 3).GetComponent<Point>();
                        }
                    }
                    else
                    {
                        // Add point
                        if (roadSystem.addedPoint != null)
                        {
                            RoadCreator road = roadSystem.addedPoint.transform.parent.parent.GetComponent<RoadCreator>();

                            // Split and create new road
                            if (roadSystem.addedPoint.transform.GetSiblingIndex() > 0 && roadSystem.addedPoint.transform.GetSiblingIndex() < roadSystem.addedPoint.transform.parent.childCount - 1)
                            {
                                Point originalPoint = roadSystem.addedPoint;

                                // Create new road
                                GameObject newRoad = new GameObject("Road");
                                Undo.RegisterCreatedObjectUndo(newRoad, "Create road");
                                newRoad.AddComponent<RoadCreator>();
                                newRoad.GetComponent<RoadCreator>().InitializeSystem();

                                // Second point position - original point position
                                Vector3 forward = (Utility.GetMousePosition(false, false) - originalPoint.transform.position).normalized;
                                newRoad.GetComponent<RoadCreator>().CreatePoint(currentEvent, false, false, roadSystem.addedPoint.transform.position + forward * 5);
                                roadSystem.addedPoint = newRoad.GetComponent<RoadCreator>().CreatePoint(currentEvent, false, true);
                                newRoad.GetComponent<RoadCreator>().Regenerate();

                                // Select newly created road
                                roadSystem.currentlyCreatedRoad = newRoad.GetComponent<RoadCreator>();
                                HideSelectedObject(roadSystem);
                                roadSystem.selectedObject = newRoad.gameObject;
                                ShowSelectedObject(roadSystem);
                                GetNearbyPoints(roadSystem.currentlyCreatedRoad.gameObject);

                                // Create intersection
                                newRoad.GetComponent<RoadCreator>().CreateIntersection(newRoad.transform.GetChild(0).GetChild(0).GetComponent<Point>(), originalPoint, null, originalPoint.transform.position);
                            }
                            else
                            {
                                Point newPoint = road.CreatePoint(currentEvent, roadSystem.addedPoint.transform.GetSiblingIndex() == 0 ? true : false);
                                roadSystem.addedPoint = newPoint; // Can be null
                            }
                        }
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    roadSystem.addedPoint = null;
                }
            }
        }

        private void InsertPoints(RoadSystem roadSystem, Event currentEvent)
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
                        RoadCreator closestRoad = null;
                        float closestDistance = float.MaxValue;
                        Vector3 mousePosition = Utility.GetMousePosition(false, true);

                        // Find nearest point by getting the distance to every bezier
                        foreach (RoadCreator road in roadSystem.roads)
                        {
                            for (int i = 0; i < road.transform.GetChild(0).childCount - 1; i++)
                            {
                                float distance = HandleUtility.DistancePointBezier(mousePosition, road.transform.GetChild(0).GetChild(i).position, road.transform.GetChild(0).GetChild(i + 1).position, road.transform.GetChild(0).GetChild(i).GetComponent<Point>().GetRightLocalControlPoint(), road.transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint());

                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    closestIndex = i;
                                    closestRoad = road;
                                }
                            }
                        }

                        closestRoad.InsertPoint(closestIndex);
                        closestRoad.Regenerate();
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                    roadSystem.addedPoint = null;
                }
            }
        }


        private void DeletePoints(RoadSystem roadSystem, Event currentEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                // Left click
                if (currentEvent.button == 0 && currentEvent.shift)
                {
                    if (roadSystem.lastPointRoad != null && roadSystem.lastPointIndex % 3 == 0 && GetIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3, 0) == HandleUtility.nearestControl)
                    {
                        roadSystem.lastPointRoad.RemovePoint(currentEvent, roadSystem.lastPointIndex / 3);
                        RemoveIdForIndex(roadSystem.lastPointRoad, roadSystem.lastPointIndex / 3);
                        roadSystem.lastPointIndex = -1;
                        roadSystem.lastPointRoad = null;

                        // Delete road incase it's now null
                        roadSystem.roads.RemoveWhere(item => item == null);
                    }
                }
                else if (currentEvent.button == 1)
                {
                    // Right click
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                }
            }
        }

        private void SelectObject(RoadSystem roadSystem)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit raycastHit;

            if (Physics.Raycast(mouseRay, out raycastHit, 1000, 1 << LayerMask.NameToLayer("Intersection") | 1 << LayerMask.NameToLayer("Road")))
            {
                // Get road or intersection object
                Transform parent = raycastHit.transform;
                while (parent != null)
                {
                    parent = parent.parent;

                    if (parent.GetComponent<RoadCreator>() || parent.GetComponent<Intersection>())
                    {
                        HideSelectedObject(roadSystem);
                        roadSystem.selectedObject = parent.gameObject;
                        ShowSelectedObject(roadSystem);

                        // Don't select a point
                        roadSystem.lastPointRoad = null;
                        roadSystem.lastPointIndex = -1;

                        if (parent.GetComponent<RoadCreator>() != null)
                        {
                            parent.GetComponent<RoadCreator>().currentPoint = null;
                        }

                        GetNearbyPoints(parent.gameObject);
                        break;
                    }
                }
            }
        }

        private void HideSelectedObject(RoadSystem roadSystem)
        {
            if (roadSystem.selectedObject != null)
            {
                int childIndex = roadSystem.selectedObject.GetComponent<RoadCreator>() != null ? 1 : 0;
                roadSystem.selectedObject.transform.GetChild(childIndex).gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
        }

        private void ShowSelectedObject(RoadSystem roadSystem)
        {
            if (roadSystem.selectedObject != null)
            {
                int childIndex = roadSystem.selectedObject.GetComponent<RoadCreator>() != null ? 1 : 0;
                roadSystem.selectedObject.transform.GetChild(childIndex).gameObject.hideFlags = HideFlags.HideInInspector;
            }
        }

        private void GetNearbyPoints(GameObject roadObject)
        {
            RoadSystem roadSystem = (RoadSystem)target;
            roadSystem.roads.Clear();
            roadSystem.intersections.Clear();

            RoadCreator road = roadObject.GetComponent<RoadCreator>();

            // Road
            if (road != null)
            {
                roadSystem.roads.Add(road);

                if (road.startIntersection != null)
                {
                    GetNearbyPointsIntersection(roadSystem, road.startIntersection);
                }
                else
                {
                    // Find nearby road
                    Vector3 position = road.transform.GetChild(0).GetChild(0).position;
                    AddNearbyRoads(roadSystem, position);
                }

                if (road.endIntersection != null)
                {
                    GetNearbyPointsIntersection(roadSystem, road.endIntersection);
                }
                else
                {
                    // Find nearby road
                    Vector3 position = road.transform.GetChild(0).GetChild(road.transform.GetChild(0).childCount - 1).position;
                    AddNearbyRoads(roadSystem, position);
                }
            }
            else
            {
                // Intersection
                GetNearbyPointsIntersection(roadSystem, roadObject.GetComponent<Intersection>());
            }
        }

        private void AddNearbyRoads(RoadSystem roadSystem, Vector3 position)
        {
            // Find all as it otherwise will only find itself
            RaycastHit[] raycastHits = Physics.BoxCastAll(position + Vector3.up * 5, new Vector3(20, 1, 20), Vector3.down, Quaternion.identity, 100, 1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection"));
            foreach (RaycastHit raycastHit in raycastHits)
            {
                if (raycastHit.transform.parent != null && raycastHit.transform.parent.parent != null)
                {
                    if (raycastHit.transform.parent.parent.GetComponent<RoadCreator>() != null)
                    {
                        roadSystem.roads.Add(raycastHit.transform.parent.parent.GetComponent<RoadCreator>());
                    }
                    else if (raycastHit.transform.parent.parent.GetComponent<Intersection>() != null)
                    {
                        roadSystem.intersections.Add(raycastHit.transform.parent.parent.GetComponent<Intersection>());
                    }
                }
            }
        }

        private void GetNearbyPointsIntersection(RoadSystem roadSystem, Intersection intersection)
        {
            roadSystem.intersections.Add(intersection);

            for (int i = 0; i < intersection.connections.Count; i++)
            {
                RoadCreator road = intersection.connections[i].GetRoad();
                roadSystem.roads.Add(road);
            }
        }

        public override void OnInspectorGUI()
        {
            RoadSystem roadSystem = (RoadSystem)target;

            if (roadSystem.selectedObject != null)
            {
                RoadCreator road = roadSystem.selectedObject.GetComponent<RoadCreator>();
                Intersection intersection = roadSystem.selectedObject.GetComponent<Intersection>();

                if (road)
                {
                    RoadEditor.ShowInspector(new SerializedObject(road), road);
                }
                else if (intersection)
                {
                    IntersectionEditor.ShowInspector(new SerializedObject(intersection), intersection);
                }
            }
        }

        private void Draw(RoadSystem roadSystem, Event currentEvent)
        {
            // Prevent rendering when no object is selected
            if (roadSystem.selectedObject == null && GlobalRoadSystemSettings.currentAction != GlobalRoadSystemSettings.Action.CreateRoad)
            {
                return;
            }

            // Draw road points
            if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.AddPoints)
            {
                Vector3 mousePosition = Utility.GetMousePosition(false, false);

                DrawAddingPoint(roadSystem, currentEvent);

                if (currentEvent.shift && roadSystem.addedPoint != null)
                {
                    // Draw line between points
                    Handles.color = Color.black;
                    Handles.DrawLine(roadSystem.addedPoint.transform.position, mousePosition);
                }
            }
            else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.InsertPoints)
            {
                DrawAddingPoint(roadSystem, currentEvent);
            }

            if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.CreateRoad)
            {
                Vector3 mousePosition = Utility.GetMousePosition(false, false);

                DrawAddingPoint(roadSystem, currentEvent);

                // Draw created road
                if (roadSystem.currentlyCreatedRoad != null)
                {
                    DrawRoad(roadSystem, roadSystem.currentlyCreatedRoad, RoadRenderingMode.CreateRoad);

                    if (currentEvent.shift)
                    {
                        // Draw line between points
                        Handles.color = Color.black;
                        Handles.DrawLine(roadSystem.currentlyCreatedRoad.transform.GetChild(0).GetChild(0).position, mousePosition);
                    }
                }
                else
                {
                    // Draw first point
                    DrawAddingPoint(roadSystem, currentEvent);
                }
            }
            else
            {
                foreach (RoadCreator road in roadSystem.roads)
                {
                    switch (GlobalRoadSystemSettings.currentAction)
                    {
                        case GlobalRoadSystemSettings.Action.Create:
                            {
                                if (roadSystem.currentlyCreatedIntersectionPoints.Count == 0)
                                {
                                    DrawRoad(roadSystem, road, RoadRenderingMode.CreateIntersection);
                                }
                                else
                                {
                                    DrawRoad(roadSystem, road, RoadRenderingMode.CreateIntersectionLast);
                                }
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.Disconnect:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.DisconnectIntersection);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.Connect:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.ConnectIntersection);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.SplitRoad:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.SplitRoad);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.AddPoints:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.Add);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.InsertPoints:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.Insert);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.DeletePoints:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.Delete);
                                break;
                            }
                        case GlobalRoadSystemSettings.Action.None:
                            {
                                DrawRoad(roadSystem, road, RoadRenderingMode.Normal);
                                break;
                            }
                        default:
                            break;
                    }
                }
            }

            // Draw select lane arrow
            if (roadSystem.selectedObject != null && roadSystem.selectedObject.GetComponent<RoadCreator>() != null)
            {
                RoadCreator road = roadSystem.selectedObject.GetComponent<RoadCreator>();
                if (road.tab == 1 && road.transform.GetChild(0).childCount > 1 && road.lanes.Count > 0)
                {
                    Handles.color = roadSystem.settings.FindProperty("selectedObjectColour").colorValue;
                    Handles.ArrowHandleCap(0, road.lanes[road.lanesTab].centerPoint + new Vector3(0, roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue * 1.15f, 0), Quaternion.Euler(90, 0, 0), roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue, EventType.Repaint);
                }
            }

            // Draw intersection points
            foreach (Intersection intersection in roadSystem.intersections)
            {
                if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Create || GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Disconnect || GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.CreateRoad || (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Connect && roadSystem.intersectionConnectionPoint == null))
                {
                    DrawIntersection(roadSystem, intersection, IntersectionRenderingMode.None);
                }
                else if (GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Connect && roadSystem.intersectionConnectionPoint != null)
                {
                    DrawIntersection(roadSystem, intersection, IntersectionRenderingMode.ConnectIntersection);
                }
                else
                {
                    DrawIntersection(roadSystem, intersection, IntersectionRenderingMode.Normal);
                }
            }

            SceneView.lastActiveSceneView.Repaint();
        }

        private void DrawAddingPoint(RoadSystem roadSystem, Event currentEvent)
        {
            Vector3 mousePosition = Utility.GetMousePosition(false, false);

            if (currentEvent.shift)
            {
                // Draw point at cursor
                Handles.color = roadSystem.settings.FindProperty("selectedAnchorPointColour").colorValue;
                Handles.CapFunction shape = GetPointShape(roadSystem);
                float handleSize = roadSystem.settings.FindProperty("anchorPointSize").floatValue;
                shape(0, mousePosition, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);
            }
        }

        private enum RoadRenderingMode { Normal, CreateIntersection, CreateIntersectionLast, DisconnectIntersection, ConnectIntersection, CreateRoad, SplitRoad, Add, Insert, Delete };

        private enum IntersectionRenderingMode { Normal, None, ConnectIntersection };

        private void DrawRoad(RoadSystem roadSystem, RoadCreator road, RoadRenderingMode roadRenderingMode)
        {
            Event currentEvent = Event.current;

            // Don't draw at all
            if (!road.connectToIntersections && (roadRenderingMode == RoadRenderingMode.CreateIntersection || roadRenderingMode == RoadRenderingMode.CreateIntersectionLast || roadRenderingMode == RoadRenderingMode.ConnectIntersection))
            {
                return;
            }

            if (road.cyclic && (roadRenderingMode == RoadRenderingMode.CreateIntersection || roadRenderingMode == RoadRenderingMode.CreateIntersectionLast || roadRenderingMode == RoadRenderingMode.ConnectIntersection || roadRenderingMode == RoadRenderingMode.SplitRoad || roadRenderingMode == RoadRenderingMode.Add))
            {
                return;
            }

            for (int i = 0; i < road.transform.GetChild(0).childCount; i++)
            {
                Transform point = road.transform.GetChild(0).GetChild(i);
                Point localControlPoint = point.GetComponent<Point>();

                // Don't draw if not selected connection point
                if (roadRenderingMode == RoadRenderingMode.ConnectIntersection && roadSystem.intersectionConnectionPoint != null && roadSystem.intersectionConnectionPoint != localControlPoint)
                {
                    continue;
                }

                // Only draw start/end points
                if ((roadRenderingMode == RoadRenderingMode.CreateIntersection || roadRenderingMode == RoadRenderingMode.DisconnectIntersection || roadRenderingMode == RoadRenderingMode.ConnectIntersection || (roadRenderingMode == RoadRenderingMode.CreateIntersectionLast && roadSystem.currentlyCreatedIntersectionPoints.Count == 2)) && i > 0 && i < road.transform.GetChild(0).childCount - 1)
                {
                    continue;
                }

                // Don't draw if connected to intersection
                if ((roadRenderingMode == RoadRenderingMode.CreateIntersection || roadRenderingMode == RoadRenderingMode.CreateIntersectionLast || roadRenderingMode == RoadRenderingMode.ConnectIntersection || roadRenderingMode == RoadRenderingMode.Add) && ((i == 0 && road.startIntersection != null) || (i == road.transform.GetChild(0).childCount - 1 && road.endIntersection != null) || road.cyclic))
                {
                    continue;
                }

                // Only draw points connected to intersection
                if (roadRenderingMode == RoadRenderingMode.DisconnectIntersection && ((i == 0 && road.startIntersection == null) || (i == road.transform.GetChild(0).childCount - 1 && road.endIntersection == null)))
                {
                    continue;
                }

                // Don't draw start or end points
                if (roadRenderingMode == RoadRenderingMode.SplitRoad && (i == 0 || i == road.transform.GetChild(0).childCount - 1))
                {
                    continue;
                }

                // Only render if nearby selected point   
                if (roadRenderingMode == RoadRenderingMode.CreateIntersectionLast && Vector3.Distance(roadSystem.currentlyCreatedIntersectionPoints[0].transform.position, point.position) > 20)
                {
                    continue;
                }

                #region Draw Points

                // Main points
                Vector3 screenPosition = HandleUtility.WorldToGUIPoint(road.transform.GetChild(0).GetChild(i).position);
                screenPosition.z = 0;

                if ((roadRenderingMode == RoadRenderingMode.CreateIntersectionLast && roadSystem.currentlyCreatedIntersectionPoints.Contains(localControlPoint)) || (roadRenderingMode == RoadRenderingMode.ConnectIntersection && roadSystem.intersectionConnectionPoint == localControlPoint) || (roadRenderingMode == RoadRenderingMode.Add && roadSystem.addedPoint == localControlPoint))
                {
                    Handles.color = roadSystem.settings.FindProperty("selectedPointColour").colorValue;
                }
                else
                {
                    // Don't render points for a road that has already been selected
                    // Two points can be selected
                    if (roadRenderingMode == RoadRenderingMode.CreateIntersectionLast && roadSystem.currentlyCreatedIntersectionPoints.Count > 0 && (road == roadSystem.currentlyCreatedIntersectionPoints[0].transform.parent.parent.GetComponent<RoadCreator>() || (roadSystem.currentlyCreatedIntersectionPoints.Count > 1 && road == roadSystem.currentlyCreatedIntersectionPoints[1].transform.parent.parent.GetComponent<RoadCreator>())))
                    {
                        continue;
                    }

                    if (HandleUtility.nearestControl == GetIdForIndex(road, i, 0))
                    {
                        Handles.color = roadSystem.settings.FindProperty("selectedAnchorPointColour").colorValue;
                    }
                    else
                    {
                        Handles.color = roadSystem.settings.FindProperty("anchorPointColour").colorValue;
                    }
                }

                Handles.CapFunction shape = GetPointShape(roadSystem);

                float handleSize = roadSystem.settings.FindProperty("anchorPointSize").floatValue;
                handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(point.position) * handleSize);
                if (road == null || !road.cyclic || road.transform.GetChild(0).childCount <= 3 || i > 0)
                {
                    shape(GetIdForIndex(road, i, 0), point.position, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);
                }

                if (roadRenderingMode == RoadRenderingMode.Normal)
                {
                    // Calculate handle rotation
                    Vector3 lookDirection = localControlPoint.leftLocalControlPointPosition.normalized;
                    lookDirection.y = 0;
                    Quaternion handleRotation = Quaternion.LookRotation(lookDirection);

                    if (Tools.pivotRotation == PivotRotation.Global)
                    {
                        handleRotation = Quaternion.identity;
                    }

                    // Don't draw first point for cyclic road
                    if (roadSystem.lastPointRoad == road && roadSystem.lastPointIndex == i * 3 && currentMovingPointIndex == -1 && (road == null || !road.cyclic || road.transform.GetChild(0).childCount <= 3 || i > 0))
                    {
                        Undo.RecordObject(point, "Move Point");
                        EditorGUI.BeginChangeCheck();
                        point.position = Utility.DrawPositionHandle(roadSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, roadSystem.settings.FindProperty("anchorPointSize").floatValue, point.position + Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue;

                        if (EditorGUI.EndChangeCheck())
                        {
                            currentEvent.Use();
                            road.Regenerate(false);
                        }
                    }

                    // Control points
                    Handles.color = roadSystem.settings.FindProperty("controlPointColour").colorValue;

                    if (i > 0)
                    {
                        handleSize = roadSystem.settings.FindProperty("controlPointSize").floatValue;
                        handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(localControlPoint.GetLeftLocalControlPoint()) * handleSize);
                        shape(GetIdForIndex(road, i, 1), localControlPoint.GetLeftLocalControlPoint(), Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                        if (roadSystem.lastPointRoad == road && roadSystem.lastPointIndex == i * 3 + 1 && currentMovingPointIndex == -1)
                        {
                            Undo.RecordObject(localControlPoint, "Move Point");
                            EditorGUI.BeginChangeCheck();
                            localControlPoint.leftLocalControlPointPosition = Utility.DrawPositionHandle(roadSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, roadSystem.settings.FindProperty("controlPointSize").floatValue, localControlPoint.GetLeftLocalControlPoint() + Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - point.position - Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue;

                            if (EditorGUI.EndChangeCheck())
                            {
                                currentEvent.Use();
                                if (GlobalRoadSystemSettings.yLock)
                                {
                                    localControlPoint.leftLocalControlPointPosition.y = 0;
                                }

                                // Change corresponding control point
                                if (!GlobalRoadSystemSettings.movePointsIndividually)
                                {
                                    float distance = localControlPoint.rightLocalControlPointPosition.magnitude;
                                    localControlPoint.rightLocalControlPointPosition = (-localControlPoint.leftLocalControlPointPosition).normalized * distance;

                                    // Change next point in cyclic road
                                    RoadCreator roadCreator = road;
                                    if (roadCreator != null && roadCreator.cyclic && i == roadCreator.transform.GetChild(0).childCount - 1)
                                    {
                                        distance = roadCreator.transform.GetChild(0).GetChild(0).GetComponent<Point>().rightLocalControlPointPosition.magnitude;
                                        roadCreator.transform.GetChild(0).GetChild(0).GetComponent<Point>().rightLocalControlPointPosition = (-localControlPoint.leftLocalControlPointPosition).normalized * distance;
                                    }
                                }

                                road.Regenerate(false);
                            }
                        }
                    }

                    if (i < road.transform.GetChild(0).childCount - 1)
                    {
                        handleSize = roadSystem.settings.FindProperty("controlPointSize").floatValue;
                        handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(localControlPoint.GetRightLocalControlPoint()) * handleSize);
                        shape(GetIdForIndex(road, i, 2), localControlPoint.GetRightLocalControlPoint(), Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                        if (roadSystem.lastPointRoad == road && roadSystem.lastPointIndex == i * 3 + 2 && currentMovingPointIndex == -1)
                        {
                            Undo.RecordObject(localControlPoint, "Move Point");
                            EditorGUI.BeginChangeCheck();
                            localControlPoint.rightLocalControlPointPosition = Utility.DrawPositionHandle(roadSystem.settings.FindProperty("scalePointsWhenZoomed").boolValue, roadSystem.settings.FindProperty("controlPointSize").floatValue, localControlPoint.GetRightLocalControlPoint() + Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - point.position - Vector3.up * roadSystem.settings.FindProperty("anchorPointSize").floatValue;

                            if (EditorGUI.EndChangeCheck())
                            {
                                currentEvent.Use();
                                if (GlobalRoadSystemSettings.yLock)
                                {
                                    localControlPoint.rightLocalControlPointPosition.y = 0;
                                }

                                // Change corresponding control point
                                if (!GlobalRoadSystemSettings.movePointsIndividually)
                                {
                                    float distance = localControlPoint.leftLocalControlPointPosition.magnitude;
                                    localControlPoint.leftLocalControlPointPosition = (-localControlPoint.rightLocalControlPointPosition).normalized * distance;

                                    // Change next point in cyclic road
                                    RoadCreator roadCreator = road;
                                    if (roadCreator != null && roadCreator.cyclic && i == 0)
                                    {
                                        distance = roadCreator.transform.GetChild(0).GetChild(roadCreator.transform.GetChild(0).childCount - 1).GetComponent<Point>().leftLocalControlPointPosition.magnitude;
                                        roadCreator.transform.GetChild(0).GetChild(roadCreator.transform.GetChild(0).childCount - 1).GetComponent<Point>().leftLocalControlPointPosition = (-localControlPoint.rightLocalControlPointPosition).normalized * distance;
                                    }
                                }

                                road.Regenerate(false);
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

                    if (i < road.transform.GetChild(0).childCount - 1)
                    {
                        Handles.DrawLine(point.position, localControlPoint.GetRightLocalControlPoint());
                    }

                    if (i < road.transform.GetChild(0).childCount - 1)
                    {
                        Handles.DrawBezier(point.transform.position, road.transform.GetChild(0).GetChild(i + 1).position, localControlPoint.GetRightLocalControlPoint(), road.transform.GetChild(0).GetChild(i + 1).GetComponent<Point>().GetLeftLocalControlPoint(), Color.black, null, 3f);
                    }

                    #endregion
                }
            }
        }

        private Handles.CapFunction GetPointShape(RoadSystem roadSystem)
        {
            Handles.CapFunction shape;
            int shapeIndex = roadSystem.settings.FindProperty("pointShape").enumValueIndex;
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

        private void DrawIntersection(RoadSystem roadSystem, Intersection intersection, IntersectionRenderingMode intersectionRenderingMode)
        {
            if (intersection == null)
            {
                return;
            }

            // Render point at intersection
            if (intersectionRenderingMode == IntersectionRenderingMode.ConnectIntersection)
            {
                // Only render if nearby selected point
                if (Vector3.Distance(intersection.transform.position, roadSystem.intersectionConnectionPoint.transform.position) > 20)
                {
                    return;
                }

                if (HandleUtility.nearestControl == GetIntersectionID(intersection))
                {
                    Handles.color = roadSystem.settings.FindProperty("selectedAnchorPointColour").colorValue;
                }
                else
                {
                    Handles.color = roadSystem.settings.FindProperty("anchorPointColour").colorValue;
                }

                Handles.CapFunction shape;
                int shapeIndex = roadSystem.settings.FindProperty("pointShape").enumValueIndex;
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

                float handleSize = roadSystem.settings.FindProperty("anchorPointSize").floatValue;
                handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(intersection.transform.position) * handleSize);
                shape(GetIntersectionID(intersection), intersection.transform.position, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);
            }

            if (roadSystem.selectedObject == intersection.gameObject)
            {
                if (intersection.tab == 1)
                {
                    // Draw selected connection
                    Handles.color = roadSystem.settings.FindProperty("selectedObjectColour").colorValue;
                    Handles.ArrowHandleCap(0, intersection.connections[intersection.connectionTab].roadPoint.transform.position + new Vector3(0, roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue * 1.15f, 0), Quaternion.Euler(90, 0, 0), roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue, EventType.Repaint);
                }
                else if (intersection.tab == 3)
                {
                    if (intersection.mainRoads.Count > 0)
                    {
                        // Prevent error
                        if (intersection.mainRoadTab > intersection.mainRoads.Count - 1)
                        {
                            intersection.mainRoadTab = 0;
                        }

                        // Draw selected main road
                        Handles.color = roadSystem.settings.FindProperty("selectedObjectColour").colorValue;
                        Handles.ArrowHandleCap(0, intersection.mainRoads[intersection.mainRoadTab].centerPoint + new Vector3(0, roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue * 1.15f, 0), Quaternion.Euler(90, 0, 0), roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue, EventType.Repaint);
                    }
                }
                else if (intersection.tab == 4 && !intersection.generateSameCrosswalkForAllConnections && intersection.crosswalks.Count > 0)
                {
                    // Draw selected crosswalk
                    Handles.color = roadSystem.settings.FindProperty("selectedObjectColour").colorValue;
                    Handles.ArrowHandleCap(0, intersection.crosswalks[intersection.crosswalkTab].centerPoint + new Vector3(0, roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue * 1.15f, 0), Quaternion.Euler(90, 0, 0), roadSystem.settings.FindProperty("selectedObjectArrowSize").floatValue, EventType.Repaint);
                }
            }


            // Render y-axis movement handle
            if (Tools.current == Tool.Move)
            {
                EditorGUI.BeginChangeCheck();

                float handleSize = Mathf.Min(intersection.settings.FindProperty("anchorPointSize").floatValue, HandleUtility.GetHandleSize(intersection.transform.position));
                if (intersection.settings.FindProperty("scalePointsWhenZoomed").boolValue)
                {
                    handleSize = HandleUtility.GetHandleSize(intersection.transform.position);
                }

                Handles.color = Handles.yAxisColor;
                intersection.heightMovement = (Handles.Slider(intersection.transform.position + new Vector3(0, intersection.heightMovement, 0), Vector3.up, handleSize, Handles.ArrowHandleCap, 0) - intersection.transform.position).y;

                if (EditorGUI.EndChangeCheck())
                {
                    for (int i = 0; i < intersection.connections.Count; i++)
                    {
                        intersection.connections[i].GetRoad().Regenerate();
                    }
                    intersection.Regenerate(false);
                }
            }
        }

        protected void GetCurrentPointIndex(RoadCreator road)
        {
            // Prevent focus switching to over points when moving
            if (currentMovingPointIndex == -1 && GUIUtility.hotControl == 0 && road.handleIds.Contains(HandleUtility.nearestControl))
            {
                RoadSystem roadSystem = (RoadSystem)target;
                roadSystem.lastPointIndex = road.handleIds.IndexOf(HandleUtility.nearestControl);
                roadSystem.lastPointRoad = road;

                road.currentPoint = road.transform.GetChild(0).GetChild(roadSystem.lastPointIndex / 3);
                road.currentPointTypeIndex = roadSystem.lastPointIndex % 3;
            }
        }

        #region Handle Ids

        private void RemoveIdForIndex(RoadCreator road, int index)
        {
            road.handleHashes.RemoveAt(index * 3);
            road.handleIds.RemoveAt(index * 3);
            road.handleHashes.RemoveAt(index * 3);
            road.handleIds.RemoveAt(index * 3);
            road.handleHashes.RemoveAt(index * 3);
            road.handleIds.RemoveAt(index * 3);
        }

        // Points 0:Main point 1:Left control point 2:Right control point
        private int GetIdForIndex(RoadCreator road, int index, int point)
        {
            if (point == 1)
            {
                return road.handleIds[index * 3 + 1];
            }
            else if (point == 2)
            {
                return road.handleIds[index * 3 + 2];
            }
            else
            {
                while (road.handleIds.Count <= index * 3)
                {
                    AddId(road);
                }

                return road.handleIds[index * 3];
            }
        }

        private void AddId(RoadCreator road)
        {
            // Main points
            int hash = ("PointHandle" + road.GetInstanceID() + road.lastHashIndex).GetHashCode();
            road.handleHashes.Add(hash);
            int id = GUIUtility.GetControlID(hash, FocusType.Passive);
            road.handleIds.Add(id);

            // Left control point
            hash = ("PointHandle" + road.GetInstanceID() + road.lastHashIndex + 1).GetHashCode();
            road.handleHashes.Add(hash);
            id = GUIUtility.GetControlID(hash, FocusType.Passive);
            road.handleIds.Add(id);

            // Right control point
            hash = ("PointHandle" + road.GetInstanceID() + road.lastHashIndex + 2).GetHashCode();
            road.handleHashes.Add(hash);
            id = GUIUtility.GetControlID(hash, FocusType.Passive);
            road.handleIds.Add(id);

            road.lastHashIndex += 3;
        }

        private int GetIntersectionID(Intersection intersection)
        {
            if (intersection.handleHash == 0)
            {
                int hash = ("IntersectionPointHandle" + intersection.GetInstanceID()).GetHashCode();
                intersection.handleHash = hash;
                int id = GUIUtility.GetControlID(hash, FocusType.Passive);
                intersection.handleId = id;
            }

            return intersection.handleHash;
        }

        #endregion
    }
}
