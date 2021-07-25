using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    [HelpURL("https://mcrafterzz.itch.io/road-creator-pro")]
    [SelectionBase]
    public class PointSystemCreator : MonoBehaviour
    {
        public float detailLevel = 10;

        // Internal
        public List<int> handleHashes = new List<int>();
        public List<int> handleIds = new List<int>();
        public int lastHashIndex = 0;
        public SerializedObject settings;

        public Point addedPoint = null;
        public Transform currentPoint;
        public int currentPointTypeIndex; // 0 == main point, 1 and 2 are control points

        public virtual void Regenerate(bool updateTerrain = false, bool updateDetails = false, bool updateTrees = false) { }

        public virtual void InitializeSystem() { }

        // Returns newly created point
        public Point SplitSegment(int segmentId, bool largeMovement)
        {
            if (segmentId > 0 && segmentId < transform.GetChild(0).childCount - 1)
            {
                GameObject newPointSystem;
                if (GetComponent<RoadCreator>() != null)
                {
                    newPointSystem = new GameObject("Road");
                    newPointSystem.AddComponent<RoadCreator>();
                }
                else
                {
                    newPointSystem = new GameObject("Prefab Line");
                    newPointSystem.AddComponent<PrefabLineCreator>();
                }

                Undo.RegisterCreatedObjectUndo(newPointSystem, "Split Segment");
                newPointSystem.transform.SetParent(newPointSystem.transform.parent, false);
                newPointSystem.transform.hideFlags = HideFlags.NotEditable;
                newPointSystem.GetComponent<PointSystemCreator>().InitializeSystem();

                // Split points
                int segments = transform.GetChild(0).childCount;

                // Copy this point
                GameObject copiedPoint = Instantiate(transform.GetChild(0).GetChild(segmentId).gameObject);
                copiedPoint.name = "Point";
                Undo.RegisterCreatedObjectUndo(copiedPoint, "Split Segment");
                copiedPoint.transform.SetParent(newPointSystem.transform.GetChild(0));
                copiedPoint.transform.position = transform.GetChild(0).GetChild(segmentId).position;

                // Move points from eacthother to prevent overlap
                Transform startPoint = transform.GetChild(0).GetChild(segmentId - 1);
                Transform endPoint = transform.GetChild(0).GetChild(segmentId);

                // Lerped point (99%) - point
                Vector3 lerpedPoint = Utility.Lerp4(startPoint.position, endPoint.position, startPoint.GetComponent<Point>().GetRightLocalControlPoint(), endPoint.GetComponent<Point>().GetLeftLocalControlPoint(), 0.99f);
                Vector3 forward = (lerpedPoint - endPoint.position).normalized;

                float distance = 2;

                if (largeMovement)
                {
                    distance = 5;
                }

                endPoint.position += forward * distance;
                copiedPoint.transform.position -= forward * distance;

                // Move points after this one to next segment
                for (int i = segments - 1; i >= segmentId + 1; i--)
                {
                    // Always move the same index as the next point will takes the previous space
                    Undo.SetTransformParent(transform.GetChild(0).GetChild(segmentId + 1), newPointSystem.transform.GetChild(0), "Split Segment");
                }

                if (GetComponent<RoadCreator>() != null)
                {
                    RoadCreator road = GetComponent<RoadCreator>();
                    RoadCreator newRoad = newPointSystem.GetComponent<RoadCreator>();
                    Undo.RegisterCompleteObjectUndo(road, "Split Segment");
                    Undo.RegisterCompleteObjectUndo(newRoad, "Split Segment");

                    // Move intersection connection
                    if (road.endIntersection != null)
                    {
                        newRoad.endIntersection = road.endIntersection;
                        newRoad.endIntersectionConnection = road.endIntersectionConnection;
                        road.endIntersection = null;
                        road.endIntersectionConnection = null;
                    }

                    // Move lanes
                    for (int i = road.lanes.Count - 1; i >= 0; i--)
                    {
                        if (road.lanes[i].wholeRoad)
                        {
                            Lane newLane = new Lane(road.lanes[i]);
                            newRoad.lanes.Insert(0, newLane);
                        }
                        else if (road.lanes[i].endIndex >= segmentId)
                        {
                            Lane newLane = new Lane(road.lanes[i]);
                            newLane.startIndex -= segmentId;
                            newLane.endIndex -= segmentId;
                            newRoad.lanes.Insert(0, newLane);

                            if (road.lanes[i].startIndex >= segmentId)
                            {
                                road.lanes.RemoveAt(i);
                            }
                            else
                            {
                                road.lanes[i].endIndex = Mathf.Min(segmentId - 1, road.lanes[i].endIndex);
                                newRoad.lanes[0].startIndex = 0;
                            }
                        }
                        else
                        {
                            road.lanes[i].endIndex = Mathf.Min(segmentId - 1, road.lanes[i].endIndex);
                        }
                    }

                    // Move prefab lines
                    for (int i = road.prefabLines.Count - 1; i >= 0; i--)
                    {
                        if (road.prefabLines[i].wholeRoad)
                        {
                            PrefabLineCreator newPrefabLine = Instantiate(road.transform.GetChild(2).GetChild(i).gameObject).GetComponent<PrefabLineCreator>();
                            newPrefabLine.transform.SetParent(newRoad.transform.GetChild(2));
                            newRoad.prefabLines.Insert(0, newPrefabLine);
                        }
                        else if (road.prefabLines[i].endIndex >= segmentId)
                        {
                            PrefabLineCreator prefabLine = Instantiate(road.transform.GetChild(2).GetChild(i).GetComponent<PrefabLineCreator>());
                            prefabLine.startIndex -= segmentId;
                            prefabLine.endIndex -= segmentId;
                            prefabLine.transform.SetParent(newRoad.transform.GetChild(2));
                            newRoad.prefabLines.Insert(0, prefabLine);

                            if (road.prefabLines[i].startIndex >= segmentId)
                            {
                                road.prefabLines.RemoveAt(i);
                                Undo.DestroyObjectImmediate(road.transform.GetChild(2).GetChild(i).gameObject);
                            }
                            else
                            {
                                road.prefabLines[i].endIndex = Mathf.Min(segmentId - 1, road.prefabLines[i].endIndex);
                                newRoad.prefabLines[0].startIndex = 0;
                            }
                        }
                        else
                        {
                            road.prefabLines[i].endIndex = Mathf.Min(segmentId - 1, road.prefabLines[i].endIndex);
                        }
                    }
                }

                Regenerate(false);
                newPointSystem.GetComponent<PointSystemCreator>().Regenerate(false);

                return copiedPoint.GetComponent<Point>();
            }
            return null;
        }

        public void SnapPointsToTerrain()
        {
            for (int i = 0; i < transform.GetChild(0).childCount; i++)
            {
                // Snap anchor point
                RaycastHit raycastHit;
                if (Physics.Raycast(new Ray(transform.GetChild(0).GetChild(i).position + Vector3.up * 50, Vector3.down), out raycastHit, 100, ~(1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection") | 1 << LayerMask.NameToLayer("Prefab Line"))))
                {
                    transform.GetChild(0).GetChild(i).position = raycastHit.point;
                }
            }
        }

        public Point CreatePoint(Event currentEvent, bool start = false, bool updateRoad = true, Vector3 overidePosition = new Vector3())
        {
            // Prevent adding more points to connected road
            if (GetComponent<RoadCreator>() != null && ((start && GetComponent<RoadCreator>().startIntersection != null) || (!start && GetComponent<RoadCreator>().endIntersection != null)))
            {
                Debug.Log("Can not continue a road in the direction that it is attached to an intersection");
                return null;
            }

            // Prevent adding points to cyclic road
            if (GetComponent<RoadCreator>() != null && GetComponent<RoadCreator>().cyclic)
            {
                Debug.Log("Can not continue a cyclic road");
                return null;
            }

            GameObject point = new GameObject("Point");

            if (overidePosition != new Vector3())
            {
                // Set custom position
                point.transform.position = overidePosition;
            }
            else
            {
                if (GetComponent<RoadCreator>() != null)
                {
                    point.transform.position = Utility.GetMousePosition(false, false);
                }
                else
                {
                    point.transform.position = Utility.GetMousePosition(false, true);
                }
            }

            point.AddComponent<Point>();
            Undo.RegisterCreatedObjectUndo(point, "Create Point");
            Undo.SetTransformParent(point.transform, transform.GetChild(0), "Create Point");
            point.hideFlags = HideFlags.NotEditable;

            if (start)
            {
                point.transform.SetAsFirstSibling();

                // Set control points
                if (transform.GetChild(0).childCount > 1)
                {
                    Point nextPoint = transform.GetChild(0).GetChild(1).GetComponent<Point>();

                    // Uncurved/straight segment
                    if (GlobalRoadSystemSettings.createStraightSegment)
                    {
                        point.GetComponent<Point>().rightLocalControlPointPosition = Vector3.Lerp(point.transform.position, nextPoint.transform.position, 0.25f) - point.transform.position;
                        nextPoint.leftLocalControlPointPosition = (point.transform.position - nextPoint.transform.position).normalized * 3;
                    }
                    else
                    {
                        Vector3 direction;
                        Vector3 offset;
                        float distance;

                        if (transform.GetChild(0).childCount == 2)
                        {
                            direction = Vector3.zero;
                            offset = point.transform.position - nextPoint.transform.position;
                            direction += offset.normalized;
                            distance = offset.magnitude;
                            direction.Normalize();

                            nextPoint.leftLocalControlPointPosition = direction * distance * 0.45f;
                        }
                        else
                        {
                            direction = Vector3.zero;
                            offset = nextPoint.rightLocalControlPointPosition;
                            direction -= offset.normalized;
                            distance = (point.transform.position - nextPoint.transform.position).magnitude;
                            direction.Normalize();

                            nextPoint.leftLocalControlPointPosition = direction * distance * 0.45f;
                        }

                        direction = Vector3.zero;
                        offset = nextPoint.transform.position - point.transform.position + nextPoint.leftLocalControlPointPosition;
                        direction += offset.normalized;
                        distance = (nextPoint.transform.position - point.transform.position).magnitude;
                        direction.Normalize();

                        point.GetComponent<Point>().rightLocalControlPointPosition = direction * distance * 0.45f;
                    }

                    point.GetComponent<Point>().leftLocalControlPointPosition = -point.GetComponent<Point>().rightLocalControlPointPosition;
                }
                else
                {
                    point.GetComponent<Point>().rightLocalControlPointPosition = Vector3.left;
                    point.GetComponent<Point>().leftLocalControlPointPosition = -point.GetComponent<Point>().rightLocalControlPointPosition;
                }

                if (GetComponent<RoadCreator>() != null)
                {
                    // Change lane positions so that they are in the same place as before
                    for (int i = 0; i < GetComponent<RoadCreator>().lanes.Count; i++)
                    {
                        if (GetComponent<RoadCreator>().lanes[i].startIndex == 0)
                        {
                            Undo.RecordObject(GetComponent<RoadCreator>(), "Create Point");
                            GetComponent<RoadCreator>().lanes[i].startIndex += 1;
                            GetComponent<RoadCreator>().lanes[i].endIndex += 1;
                        }
                    }

                    // Change prefab lines so that they are in the same place as before
                    for (int i = 0; i < GetComponent<RoadCreator>().prefabLines.Count; i++)
                    {
                        if (GetComponent<RoadCreator>().prefabLines[i].startIndex == 0)
                        {
                            Undo.RecordObject(GetComponent<RoadCreator>().prefabLines[i], "Create Point");
                            GetComponent<RoadCreator>().prefabLines[i].startIndex += 1;
                            GetComponent<RoadCreator>().prefabLines[i].endIndex += 1;
                        }
                    }
                }
            }
            else
            {
                // Set control points
                if (transform.GetChild(0).childCount > 1)
                {
                    Point lastPoint = transform.GetChild(0).GetChild(transform.GetChild(0).childCount - 2).GetComponent<Point>();

                    // Uncurved/straight segment
                    if (GlobalRoadSystemSettings.createStraightSegment)
                    {
                        point.GetComponent<Point>().leftLocalControlPointPosition = Vector3.Lerp(point.transform.position, lastPoint.transform.position, 0.25f) - point.transform.position;
                        lastPoint.rightLocalControlPointPosition = (point.transform.position - lastPoint.transform.position).normalized * 3;
                    }
                    else
                    {
                        Vector3 direction;
                        Vector3 offset;
                        float distance;

                        if (transform.GetChild(0).childCount == 2)
                        {
                            direction = Vector3.zero;
                            offset = point.transform.position - lastPoint.transform.position;
                            direction += offset.normalized;
                            distance = offset.magnitude;
                            direction.Normalize();

                            lastPoint.rightLocalControlPointPosition = direction * distance * 0.45f;
                        }
                        else
                        {
                            direction = Vector3.zero;
                            offset = lastPoint.leftLocalControlPointPosition;
                            direction -= offset.normalized;
                            distance = (point.transform.position - lastPoint.transform.position).magnitude;
                            direction.Normalize();

                            lastPoint.rightLocalControlPointPosition = direction * distance * 0.45f;
                        }

                        direction = Vector3.zero;
                        offset = lastPoint.transform.position - point.transform.position + lastPoint.rightLocalControlPointPosition;
                        direction += offset.normalized;
                        distance = (lastPoint.transform.position - point.transform.position).magnitude;
                        direction.Normalize();

                        point.GetComponent<Point>().leftLocalControlPointPosition = direction * distance * 0.45f;
                    }

                    point.GetComponent<Point>().rightLocalControlPointPosition = -point.GetComponent<Point>().leftLocalControlPointPosition;
                }
                else
                {
                    point.GetComponent<Point>().rightLocalControlPointPosition = Vector3.left;
                    point.GetComponent<Point>().leftLocalControlPointPosition = -point.GetComponent<Point>().rightLocalControlPointPosition;
                }
            }

            if (GetComponent<RoadCreator>() != null)
            {
                // Make sure intersections are attached to correct points
                GetComponent<RoadCreator>().UpdateConnectedLanes();
            }

            if (updateRoad)
            {
                Regenerate(false);
            }

            return point.GetComponent<Point>();
        }

        public void InsertPoint(int closestIndex)
        {
            Vector3 mousePosition = Utility.GetMousePosition(false, true);
            Vector3[] positions = Utility.ClosestPointOnLineSegment(mousePosition, transform.GetChild(0).GetChild(closestIndex).position, transform.GetChild(0).GetChild(closestIndex + 1).position, transform.GetChild(0).GetChild(closestIndex).GetComponent<Point>().GetRightLocalControlPoint(), transform.GetChild(0).GetChild(closestIndex + 1).GetComponent<Point>().GetLeftLocalControlPoint());

            if (Vector3.Distance(positions[0], transform.GetChild(0).GetChild(closestIndex).position) > 1 && Vector3.Distance(positions[0], transform.GetChild(0).GetChild(closestIndex + 1).position) > 1)
            {
                int newIndex = closestIndex + 1;
                GameObject point = new GameObject("Point");
                Undo.RegisterCreatedObjectUndo(point, "Insert Point");
                point.transform.SetParent(transform.GetChild(0));
                point.transform.SetSiblingIndex(newIndex);
                point.transform.position = positions[0];
                point.AddComponent<Point>();
                point.hideFlags = HideFlags.NotEditable;

                Vector3 previousPoint = point.transform.parent.GetChild(newIndex - 1).transform.position;
                Vector3 previousControlPoint = point.transform.parent.GetChild(newIndex - 1).GetComponent<Point>().rightLocalControlPointPosition;

                Vector3 nextPoint = point.transform.parent.GetChild(newIndex + 1).transform.position;
                Vector3 nextControlPoint = point.transform.parent.GetChild(newIndex + 1).GetComponent<Point>().leftLocalControlPointPosition;

                float previousDistance = Vector3.Distance(previousPoint, nextPoint + nextControlPoint);
                float nextDistance = Vector3.Distance(previousPoint + previousControlPoint, nextPoint);

                Vector3 leftIntersection = Utility.GetLineIntersection(previousPoint, previousControlPoint.normalized, point.transform.position, (positions[0] - positions[1]).normalized, previousDistance);
                Vector3 rightIntersection = Utility.GetLineIntersection(nextPoint, nextControlPoint.normalized, point.transform.position, (positions[1] - positions[0]).normalized, nextDistance);

                if (leftIntersection != Utility.MaxVector3)
                {
                    // Towards intersected point
                    point.GetComponent<Point>().leftLocalControlPointPosition = leftIntersection - point.transform.position;
                }
                else
                {
                    // Slightly out from existing point
                    point.GetComponent<Point>().leftLocalControlPointPosition = (positions[0] - positions[1]).normalized * 3;
                }

                if (rightIntersection != Utility.MaxVector3)
                {
                    // Towards intersected point
                    point.GetComponent<Point>().rightLocalControlPointPosition = rightIntersection - point.transform.position;
                }
                else
                {
                    // Slightly out from existing point
                    point.GetComponent<Point>().rightLocalControlPointPosition = (positions[1] - positions[0]).normalized * 3;
                }

                // Adapt to new point, control point half way to new control point
                // Last point
                Point previousPointObject = point.transform.parent.GetChild(newIndex - 1).GetComponent<Point>();
                Undo.RecordObject(previousPointObject, "Insert Point");
                previousPointObject.rightLocalControlPointPosition = Vector3.Lerp(previousPointObject.transform.position, point.GetComponent<Point>().GetLeftLocalControlPoint(), 0.8f) - previousPointObject.transform.position;

                // Next point
                Point nextPointObject = point.transform.parent.GetChild(newIndex + 1).GetComponent<Point>();
                Undo.RecordObject(nextPointObject, "Insert Point");
                nextPointObject.leftLocalControlPointPosition = Vector3.Lerp(nextPointObject.transform.position, point.GetComponent<Point>().GetRightLocalControlPoint(), 0.8f) - nextPointObject.transform.position;

                // Update lanes
                if (GetComponent<RoadCreator>() != null)
                {
                    for (int i = 0; i < GetComponent<RoadCreator>().lanes.Count; i++)
                    {
                        if (GetComponent<RoadCreator>().lanes[i].startIndex >= newIndex)
                        {
                            Undo.RecordObject(this, "Insert Point");
                            GetComponent<RoadCreator>().lanes[i].startIndex += 1;
                        }

                        if (GetComponent<RoadCreator>().lanes[i].endIndex >= newIndex)
                        {
                            Undo.RecordObject(this, "Insert Point");
                            GetComponent<RoadCreator>().lanes[i].endIndex += 1;
                        }
                    }
                }

                Regenerate(false);
            }
        }

        public void RemovePoint(Event currentEvent, int id)
        {
            RoadCreator road = GetComponent<RoadCreator>();

            if (road != null)
            {
                // Update connected intersections
                if (id == 0 || id == transform.GetChild(0).childCount - 1)
                {
                    road.RemoveConnectedPoint(id);
                }
            }

            // Destroy point object
            Undo.DestroyObjectImmediate(transform.GetChild(0).GetChild(id).gameObject);

            if (road != null)
            {
                // Change lane indexes to try and keep them at the same positions as before
                List<Lane> lanes = road.lanes;
                for (int i = 0; i < lanes.Count; i++)
                {
                    Undo.RecordObject(this, "Remove Point");
                    if (lanes[i].startIndex > id)
                    {
                        lanes[i].startIndex -= 1;
                        lanes[i].endIndex -= 1;
                    }
                    else if (lanes[i].endIndex >= id - 1 && lanes[i].endIndex > lanes[i].startIndex)
                    {
                        lanes[i].endIndex -= 1;
                    }
                }

                // Change prefab line indexes to try and keep them at the same positions as before
                List<PrefabLineCreator> prefabLines = road.prefabLines;
                for (int i = 0; i < prefabLines.Count; i++)
                {
                    Undo.RecordObject(prefabLines[i], "Remove Point");
                    if (prefabLines[i].startIndex > id)
                    {
                        prefabLines[i].startIndex -= 1;
                        prefabLines[i].endIndex -= 1;
                    }
                    else if (prefabLines[i].endIndex >= id - 1 && prefabLines[i].endIndex > prefabLines[i].startIndex)
                    {
                        prefabLines[i].endIndex -= 1;
                    }
                }
            }

            Regenerate(false);
        }
    }
}
#endif
