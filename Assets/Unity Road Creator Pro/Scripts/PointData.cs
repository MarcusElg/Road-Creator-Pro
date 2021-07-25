using System.Collections.Generic;
using UnityEngine;

namespace RoadCreatorPro
{
    public class PointData
    {
        public List<Vector3> positions;
        public List<int> segmentStartIndexes;

        public PointData(List<Vector3> positions, List<int> segmentStartIndexes)
        {
            this.positions = positions;
            this.segmentStartIndexes = segmentStartIndexes;
        }
    }
}