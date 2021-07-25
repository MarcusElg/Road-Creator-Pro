#if UNITY_EDITOR

namespace RoadCreatorPro
{
    [System.Serializable]
    public class TerrainModificationInterval
    {
        public bool wholeRoad = true;
        public int startIndex = 0;
        public float startPercentageOffset = 0;
        public int endIndex = 0;
        public float endPercentageOffset = 1;
    }
}
#endif