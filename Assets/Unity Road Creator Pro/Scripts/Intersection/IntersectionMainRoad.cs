using UnityEngine;

namespace RoadCreatorPro
{
    [System.Serializable]
    public class IntersectionMainRoad
    {
        public Material material;
        public PhysicMaterial physicMaterial;
        public bool flipUvs = false;
        public float textureTilingMultiplier = 1;
        public float textureTilingOffset = 0;
        public int startIndex = 0;
        public int endIndex = 1;
        public bool wholeLeftRoad = true;
        public bool wholeRightRoad = true;
        public int startIndexLeftRoad = 0;
        public int endIndexLeftRoad = 1;
        public int startIndexRightRoad = 0;
        public int endIndexRightRoad = 1;
        public float yOffset = 0.01f;

        // Internal
        public Vector3 centerPoint = Vector3.zero;
        public bool generated = false;
    }
}
