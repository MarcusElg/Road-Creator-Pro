using System.Collections.Generic;
using UnityEngine;

namespace RoadCreatorPro
{
    public class PrefabData
    {
        public List<float> percentages;
        public List<Vector3> startPoints;
        public List<Vector3> endPoints;
        public List<float> startTimes;
        public List<float> endTimes;
        public List<int> segment;

        // Offsets for road prefab lines
        public List<float> startOffsets;
        public List<float> endOffsets;

        public PrefabData(List<float> percentages, List<Vector3> startPoints, List<Vector3> endPoints, List<float> startTimes, List<float> endTimes, List<int> segment, List<float> startOffsets, List<float> endOffsets)
        {
            this.percentages = percentages;
            this.startPoints = startPoints;
            this.endPoints = endPoints;
            this.startTimes = startTimes;
            this.endTimes = endTimes;
            this.segment = segment;
            this.startOffsets = startOffsets;
            this.endOffsets = endOffsets;
        }
    }
}