using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Prefab Line Preset", menuName = "ScriptableObjects/PrefabLinePreset", order = 1)]
    public class PrefabLinePreset : ScriptableObject
    {
        public PrefabLineData prefabLineData;
    }
}
#endif