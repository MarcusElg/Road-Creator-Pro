using UnityEngine;

namespace RoadCreatorPro
{
    [System.Serializable]
    public struct VertexShaderStruct
    {
        public Vector3 position;
        public Vector3 localPosition;

        public VertexShaderStruct(Vector3 position, Vector3 localPosition)
        {
            this.position = position;
            this.localPosition = localPosition;
        }
    }
}