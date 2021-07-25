using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RoadCreatorPro
{
    public class RoadCreatorProjectSettings
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            SettingsProvider settingsProvider = new SettingsProvider("Project/RoadCreator", SettingsScope.Project)
            {
                label = "Road Creator",

                guiHandler = (searchContext) =>
                {
                    SerializedObject settings = RoadCreatorSettings.GetSerializedSettings();
                    EditorGUI.BeginChangeCheck();

                    // General
                    GUILayout.Label("General", EditorStyles.boldLabel);
                    settings.FindProperty("anchorPointSize").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Anchor Point Size", settings.FindProperty("anchorPointSize").floatValue), 0.001f, 30f);
                    settings.FindProperty("controlPointSize").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Control Point Size", settings.FindProperty("controlPointSize").floatValue), 0.001f, 30f);
                    settings.FindProperty("selectedObjectArrowSize").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Selected Object Arrow Size", settings.FindProperty("selectedObjectArrowSize").floatValue), 0.001f, 30f);
                    EditorGUILayout.PropertyField(settings.FindProperty("pointShape"));
                    settings.FindProperty("scalePointsWhenZoomed").boolValue = EditorGUILayout.Toggle(new GUIContent("Scale Points When Zoomed", "Determines if points should change size when zooming in/out"), settings.FindProperty("scalePointsWhenZoomed").boolValue);
                    settings.FindProperty("exposeCurveKeysToEditor").boolValue = EditorGUILayout.Toggle("Expose Curve Keys To Editor", settings.FindProperty("exposeCurveKeysToEditor").boolValue);
                    settings.FindProperty("prefabExportLocation").stringValue = EditorGUILayout.TextField("Prefab Export Location", settings.FindProperty("prefabExportLocation").stringValue);
                    settings.FindProperty("roadOptionsIconSize").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Road Options Icon Size", "The size of the icons for creating intersections etc"), settings.FindProperty("roadOptionsIconSize").intValue), 10, 100);

                    if (GUILayout.Button("Reset"))
                    {
                        settings.FindProperty("anchorPointSize").floatValue = 1.5f;
                        settings.FindProperty("controlPointSize").floatValue = 1.5f;
                        settings.FindProperty("selectedObjectArrowSize").floatValue = 4.5f;
                        settings.FindProperty("pointShape").enumValueIndex = (int)RoadCreatorSettings.PointShape.Cylinder;
                        settings.FindProperty("scalePointsWhenZoomed").boolValue = false;
                        settings.FindProperty("exposeCurveKeysToEditor").boolValue = true;
                        settings.FindProperty("prefabExportLocation").stringValue = "Assets/";
                        settings.FindProperty("roadOptionsIconSize").intValue = 40;
                    }

                    // Road guidelines
                    GUILayout.Space(20);
                    GUILayout.Label("Road Guidelines", EditorStyles.boldLabel);
                    settings.FindProperty("roadGuidelinesLength").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Length", settings.FindProperty("roadGuidelinesLength").floatValue), 0, 30);
                    if (settings.FindProperty("roadGuidelinesLength").floatValue > 0)
                    {
                        settings.FindProperty("roadGuidelinesRenderDistance").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Render Distance", settings.FindProperty("roadGuidelinesRenderDistance").floatValue), settings.FindProperty("roadGuidelinesLength").floatValue * 1.5f, 150);
                        settings.FindProperty("roadGuidelinesSnapDistance").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Snap Distance", settings.FindProperty("roadGuidelinesSnapDistance").floatValue), 0, 2);
                    }

                    if (GUILayout.Button("Reset"))
                    {
                        settings.FindProperty("roadGuidelinesLength").floatValue = 10;
                        settings.FindProperty("roadGuidelinesRenderDistance").floatValue = 60;
                        settings.FindProperty("roadGuidelinesSnapDistance").floatValue = 0.5f;
                    }

                    // Defaults
                    GUILayout.Space(20);
                    GUILayout.Label("Defaults", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("defaultLaneMaterials"));
                    EditorGUILayout.PropertyField(settings.FindProperty("defaultIntersectionMaterials"));
                    settings.FindProperty("defaultIntersectionMainRoadMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Intersection Main Road Material", settings.FindProperty("defaultIntersectionMainRoadMaterial").objectReferenceValue, typeof(Material), false);
                    settings.FindProperty("defaultIntersectionCrosswalkMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Intersection Crosswalk Material", settings.FindProperty("defaultIntersectionCrosswalkMaterial").objectReferenceValue, typeof(Material), false);
                    settings.FindProperty("defaultStartPrefab").objectReferenceValue = EditorGUILayout.ObjectField("Start Prefab", settings.FindProperty("defaultStartPrefab").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("defaultMainPrefab").objectReferenceValue = EditorGUILayout.ObjectField("Main Prefab", settings.FindProperty("defaultMainPrefab").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("defaultEndPrefab").objectReferenceValue = EditorGUILayout.ObjectField("End Prefab", settings.FindProperty("defaultEndPrefab").objectReferenceValue, typeof(GameObject), false);

                    if (GUILayout.Button("Reset"))
                    {
                        settings.FindProperty("defaultLaneMaterials").ClearArray();
                        settings.FindProperty("defaultIntersectionMaterials").ClearArray();
                        settings.FindProperty("defaultIntersectionMainRoadMaterial").objectReferenceValue = null;
                        settings.FindProperty("defaultIntersectionCrosswalkMaterial").objectReferenceValue = null;
                        settings.FindProperty("defaultStartPrefab").objectReferenceValue = null;
                        settings.FindProperty("defaultMainPrefab").objectReferenceValue = null;
                        settings.FindProperty("defaultEndPrefab").objectReferenceValue = null;
                    }

                    // Turn markings
                    GUILayout.Space(20);
                    GUILayout.Label("Turn Markings", EditorStyles.boldLabel);
                    settings.FindProperty("leftTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Left Turn Marking", settings.FindProperty("leftTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("forwardTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Forward Turn Marking", settings.FindProperty("forwardTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("rightTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Right Turn Marking", settings.FindProperty("rightTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("leftForwardTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Left And Forward Turn Marking", settings.FindProperty("leftForwardTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("rightForwardTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Right And Forward Turn Marking", settings.FindProperty("rightForwardTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("leftRightTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Left And Right Turn Marking", settings.FindProperty("leftRightTurnMarking").objectReferenceValue, typeof(GameObject), false);
                    settings.FindProperty("leftRightForwardTurnMarking").objectReferenceValue = (GameObject)EditorGUILayout.ObjectField("Left, Right And Forward Turn Marking", settings.FindProperty("leftRightForwardTurnMarking").objectReferenceValue, typeof(GameObject), false);

                    if (GUILayout.Button("Reset Turn Markings"))
                    {
                        settings.FindProperty("leftTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("forwardTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("rightTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("leftForwardTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("rightForwardTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("leftRightTurnMarking").objectReferenceValue = null;
                        settings.FindProperty("leftRightForwardTurnMarking").objectReferenceValue = null;
                    }

                    // Colours
                    GUILayout.Space(20);
                    GUILayout.Label("Colours", EditorStyles.boldLabel);
                    settings.FindProperty("anchorPointColour").colorValue = EditorGUILayout.ColorField("Anchor Point", settings.FindProperty("anchorPointColour").colorValue);
                    settings.FindProperty("selectedAnchorPointColour").colorValue = EditorGUILayout.ColorField("Selected Anchor Point", settings.FindProperty("selectedAnchorPointColour").colorValue);
                    settings.FindProperty("controlPointColour").colorValue = EditorGUILayout.ColorField("Control Point", settings.FindProperty("controlPointColour").colorValue);
                    settings.FindProperty("roadGuidelinesColour").colorValue = EditorGUILayout.ColorField("Road Guidelines", settings.FindProperty("roadGuidelinesColour").colorValue);
                    settings.FindProperty("selectedObjectColour").colorValue = EditorGUILayout.ColorField("Selected Object", settings.FindProperty("selectedObjectColour").colorValue);
                    settings.FindProperty("selectedPointColour").colorValue = EditorGUILayout.ColorField("Selected Point", settings.FindProperty("selectedPointColour").colorValue);

                    if (GUILayout.Button("Reset"))
                    {
                        settings.FindProperty("anchorPointColour").colorValue = new Color(1, 0, 0, 0.5f);
                        settings.FindProperty("selectedAnchorPointColour").colorValue = new Color(0.75f, 0, 0, 0.5f);
                        settings.FindProperty("controlPointColour").colorValue = new Color(1, 1, 0, 0.5f);
                        settings.FindProperty("roadGuidelinesColour").colorValue = new Color(0, 0.4f, 0, 1);
                        settings.FindProperty("selectedObjectColour").colorValue = new Color(0, 0, 1, 0.5f);
                        settings.FindProperty("selectedPointColour").colorValue = new Color(0, 1, 1, 0.5f);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        RoadCreatorSettings.GetOrCreateSettings().CheckDefaults();
                        UpdateSettings();
                    }
                }
            };

            return settingsProvider;
        }

        public static void UpdateSettings()
        {
            PointSystemCreator[] pointSystems = GameObject.FindObjectsOfType<PointSystemCreator>();
            for (int i = 0; i < pointSystems.Length; i++)
            {
                if (pointSystems[i].settings == null)
                {
                    pointSystems[i].settings = RoadCreatorSettings.GetSerializedSettings();
                }
                else
                {
                    pointSystems[i].settings.Update();
                }
            }

            Intersection[] intersections = GameObject.FindObjectsOfType<Intersection>();
            for (int i = 0; i < intersections.Length; i++)
            {
                if (intersections[i].settings == null)
                {
                    intersections[i].settings = RoadCreatorSettings.GetSerializedSettings();
                }
                else
                {
                    intersections[i].settings.Update();
                }
            }
        }
    }
}
#endif
