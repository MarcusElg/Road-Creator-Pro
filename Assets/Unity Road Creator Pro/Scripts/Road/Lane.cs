#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace RoadCreatorPro
{
    [System.Serializable]
    public class Lane
    {
        public bool wholeRoad;
        public int startIndex;
        public float startPercentageOffset;
        public int endIndex;
        public float endPercentageOffset;
        public bool mainRoadPart = true;
        public AnimationCurve width;
        public AnimationCurve yOffset;
        public List<Material> materials;
        public PhysicMaterial physicMaterial;
        public float textureTilingMultiplier;
        public bool constantUvWidth;
        public bool flipUvs;
        public float uvXMin;
        public float uvXMax;
        public bool ignoreForWidthCalculation;

        // Internal
        public Vector3 centerPoint = Utility.MaxVector3;

        public Lane()
        {
            wholeRoad = true;
            startIndex = 0;
            startPercentageOffset = 0;
            endIndex = 0;
            endPercentageOffset = 1;
            mainRoadPart = true;
            width = AnimationCurve.Constant(0, 1, 3);
            yOffset = AnimationCurve.Constant(0, 1, 0);
            textureTilingMultiplier = 1;
            constantUvWidth = false;
            flipUvs = false;
            uvXMin = 0;
            uvXMax = 1;
            ignoreForWidthCalculation = false;
        }

        public Lane(Lane lane)
        {
            wholeRoad = lane.wholeRoad;
            startIndex = lane.startIndex;
            startPercentageOffset = lane.startPercentageOffset;
            endIndex = lane.endIndex;
            endPercentageOffset = lane.endPercentageOffset;
            mainRoadPart = lane.mainRoadPart;
            width = lane.width;
            yOffset = lane.yOffset;
            materials = lane.materials;
            physicMaterial = lane.physicMaterial;
            textureTilingMultiplier = lane.textureTilingMultiplier;
            constantUvWidth = lane.constantUvWidth;
            flipUvs = lane.flipUvs;
            uvXMin = lane.uvXMin;
            uvXMax = lane.uvXMax;
            ignoreForWidthCalculation = lane.ignoreForWidthCalculation;
        }
    }
}
#endif