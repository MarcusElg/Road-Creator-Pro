using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    public class RoadCreatorSettings : ScriptableObject
    {
        // General
        public float anchorPointSize = 1.5f;
        public float controlPointSize = 1.5f;
        public float selectedObjectArrowSize = 4.5f;
        public enum PointShape { Cylinder, Sphere, Cube, Cone };
        public PointShape pointShape;
        public bool scalePointsWhenZoomed = false;
        public bool exposeCurveKeysToEditor = true;
        public string prefabExportLocation = "Assets/";
        public int roadOptionsIconSize = 40;

        // Road guidelines
        public float roadGuidelinesLength = 10f;
        public float roadGuidelinesRenderDistance = 60f;
        public float roadGuidelinesSnapDistance = 0.5f;

        // Defaults
        public List<Material> defaultLaneMaterials;
        public List<Material> defaultIntersectionMaterials;
        public Material defaultIntersectionMainRoadMaterial;
        public Material defaultIntersectionCrosswalkMaterial;
        public GameObject defaultStartPrefab;
        public GameObject defaultMainPrefab;
        public GameObject defaultEndPrefab;

        // Turn markings
        public GameObject leftTurnMarking;
        public GameObject forwardTurnMarking;
        public GameObject rightTurnMarking;
        public GameObject leftForwardTurnMarking;
        public GameObject rightForwardTurnMarking;
        public GameObject leftRightTurnMarking;
        public GameObject leftRightForwardTurnMarking;

        // Colours
        public Color anchorPointColour = new Color(1, 0, 0, 0.5f);
        public Color selectedAnchorPointColour = new Color(0.75f, 0, 0, 0.5f);
        public Color controlPointColour = new Color(1, 1, 0, 0.5f);
        public Color roadGuidelinesColour = new Color(0, 0.4f, 0, 1);
        public Color selectedObjectColour = new Color(0, 0, 1, 0.5f);
        public Color selectedPointColour = new Color(0, 1, 1, 0.5f);

        private void OnEnable()
        {
            CheckDefaults();
        }

        public static RoadCreatorSettings GetOrCreateSettings()
        {
            RoadCreatorSettings settings = AssetDatabase.LoadAssetAtPath<RoadCreatorSettings>("Assets/Editor/RoadCreatorSettings.asset");
            if (settings == null)
            {
                if (Directory.Exists("Assets/Editor") == false)
                {
                    Directory.CreateDirectory("Assets/Editor");
                }

                settings = ScriptableObject.CreateInstance<RoadCreatorSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Editor/RoadCreatorSettings.asset");
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        public void CheckDefaults()
        {
            if (defaultLaneMaterials == null || defaultLaneMaterials.Count == 0)
            {
                defaultLaneMaterials = new List<Material>();
                defaultLaneMaterials.Add(Resources.Load("Materials/Asphalt") as Material);
                defaultLaneMaterials.Add(Resources.Load("Materials/Road/Lane Center") as Material);
            }

            if (defaultIntersectionMaterials == null || defaultIntersectionMaterials.Count == 0)
            {
                defaultIntersectionMaterials = new List<Material>();
                defaultIntersectionMaterials.Add(Resources.Load("Materials/Asphalt") as Material);
                defaultIntersectionMaterials.Add(Resources.Load("Materials/Intersections/Intersection") as Material);
            }

            if (defaultIntersectionMainRoadMaterial == null)
            {
                defaultIntersectionMainRoadMaterial = Resources.Load("Materials/Intersections/2L 2 Sides Intersection Main Road") as Material;
            }

            if (defaultIntersectionCrosswalkMaterial == null)
            {
                defaultIntersectionCrosswalkMaterial = Resources.Load("Materials/Intersections/Crosswalk") as Material;
            }

            if (defaultMainPrefab == null)
            {
                defaultMainPrefab = Resources.Load("Prefabs/Concrete Barrier") as GameObject;
            }

            // Turn markings
            if (leftTurnMarking == null)
            {
                leftTurnMarking = Resources.Load("Prefabs/Turn Markings/Left Arrow") as GameObject;
            }

            if (forwardTurnMarking == null)
            {
                forwardTurnMarking = Resources.Load("Prefabs/Turn Markings/Forward Arrow") as GameObject;
            }

            if (rightTurnMarking == null)
            {
                rightTurnMarking = Resources.Load("Prefabs/Turn Markings/Right Arrow") as GameObject;
            }

            if (leftForwardTurnMarking == null)
            {
                leftForwardTurnMarking = Resources.Load("Prefabs/Turn Markings/Forward And Left Arrow") as GameObject;
            }

            if (rightForwardTurnMarking == null)
            {
                rightForwardTurnMarking = Resources.Load("Prefabs/Turn Markings/Forward And Right Arrow") as GameObject;
            }

            if (leftRightTurnMarking == null)
            {
                leftRightTurnMarking = Resources.Load("Prefabs/Turn Markings/Left And Right Arrow") as GameObject;
            }

            if (leftRightForwardTurnMarking == null)
            {
                leftRightForwardTurnMarking = Resources.Load("Prefabs/Turn Markings/Forward, Left And Right Arrow") as GameObject;
            }
        }
    }
}
#endif
