using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    public class RoadSystem : MonoBehaviour
    {
        public GameObject selectedObject;
        public HashSet<RoadCreator> roads = new HashSet<RoadCreator>();
        public HashSet<Intersection> intersections = new HashSet<Intersection>();

        public List<Point> currentlyCreatedIntersectionPoints = new List<Point>();
        public RoadCreator currentlyCreatedRoad = null;
        public Point intersectionConnectionPoint = null;
        public Point addedPoint = null;
        public int lastPointIndex = -1; // Including control points
        public RoadCreator lastPointRoad = null;

        public SerializedObject settings;

        public void ClearAction()
        {
            currentlyCreatedIntersectionPoints.Clear();
            intersectionConnectionPoint = null;
            currentlyCreatedRoad = null;
            addedPoint = null;
        }
    }
}
#endif
