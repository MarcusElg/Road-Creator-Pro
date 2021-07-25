using UnityEngine;

namespace RoadCreatorPro
{
    public class Point : MonoBehaviour
    {
        public Vector3 leftLocalControlPointPosition;
        public Vector3 rightLocalControlPointPosition;

        public Vector3 GetLeftLocalControlPoint()
        {
            return transform.position + leftLocalControlPointPosition;
        }

        public Vector3 GetRightLocalControlPoint()
        {
            return transform.position + rightLocalControlPointPosition;
        }
    }
}
