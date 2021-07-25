#if UNITY_EDITOR
using UnityEngine;

namespace RoadCreatorPro
{
    [System.Serializable]
    public class IntersectionCrosswalk
    {
        public int connectionIndex;
        public float width = 2;
        public float insetDistance = 0f;
        public bool anchorAtConnection = true;
        public Material material;
        public float textureTilingMultiplier = 1;
        public float textureTilingOffset = 0;
        public float yOffset = 0.05f;

        // Internal
        public Vector3 centerPoint = Vector3.zero;
    }
}
#endif
