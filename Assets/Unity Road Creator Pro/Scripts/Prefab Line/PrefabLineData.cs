#if UNITY_EDITOR
using UnityEngine;

namespace RoadCreatorPro
{
    [System.Serializable]
    public class PrefabLineData
    {
        // General
        public bool randomizeSpacing = false;
        public float spacing = 1;
        public float maxSpacing = 1;
        public bool fillGap = true;
        public bool deformPrefabsToCurve = true;
        public bool deformPrefabsToTerrain = true;
        public float yOffset = 0;
        public PrefabLineCreator.RotationDirection rotationDirection = PrefabLineCreator.RotationDirection.Left;
        public PrefabLineCreator.EndMode endMode = PrefabLineCreator.EndMode.Round;
        public float rotationRandomization = 0;
        public bool centralYModification = false;
        public bool bridgePillarMode = false;
        public bool onlyYModifyBottomVertices = false;
        public bool useCenterDistance = true;

        // Prefabs
        public GameObject startPrefab;
        public GameObject mainPrefab;
        public GameObject endPrefab;
        public float xScale = 1;
        public AnimationCurve yScale = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve zScale = AnimationCurve.Constant(0, 1, 1);

        // Road prefab lines
        public bool controlled = false;
        public AnimationCurve offsetCurve = AnimationCurve.Constant(0, 1, 0);
        public bool wholeRoad = true;
        public int startIndex = 0;
        public float startOffsetPercentage = 0;
        public int endIndex = 0;
        public float endOffsetPercentage = 1;
    }
}
#endif