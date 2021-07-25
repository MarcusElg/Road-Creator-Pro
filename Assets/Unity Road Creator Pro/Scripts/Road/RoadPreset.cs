using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    [CreateAssetMenu(fileName = "Road Preset", menuName = "ScriptableObjects/RoadPreset", order = 1)]
    public class RoadPreset : ScriptableObject
    {
        // General
        public float baseYOffset = 0.02f;
        public bool connectToIntersections = true;
        public bool generateColliders = true;

        // LOD
        public int lodLevels = 3;
        public List<float> lodDistances = new List<float>();
        public List<int> lodVertexDivisions = new List<int>();

        // Terrain Modification
        public bool deformMeshToTerrain = false;
        public bool modifyTerrainHeight = true;
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

        // Lanes
        public List<Lane> lanes;

        // Prefabs
        // General
        public List<PrefabLineData> prefabLines = new List<PrefabLineData>();
    }
}
#endif