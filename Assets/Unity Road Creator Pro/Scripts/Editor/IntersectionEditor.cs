using UnityEditor;
using UnityEngine;


namespace RoadCreatorPro
{
    [CustomEditor(typeof(Intersection))]
    public class IntersectionEditor : Editor
    {
        public override void OnInspectorGUI()
        {

        }

        #region Inspector
        public static void ShowInspector(SerializedObject serializedObject, Intersection intersection)
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(intersection.gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(intersection.gameObject) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            if (intersection.settings == null)
            {
                intersection.settings = RoadCreatorSettings.GetSerializedSettings();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Intersection", EditorStyles.boldLabel);
            serializedObject.FindProperty("tab").intValue = GUILayout.Toolbar(serializedObject.FindProperty("tab").intValue, new string[] { "General", "Connections", "Terrain", "Main Roads", "Crosswalks" });

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorGUI.BeginChangeCheck();

            if (serializedObject.FindProperty("tab").intValue == 0)
            {
                InspectGeneral(serializedObject, intersection);
            }
            else if (serializedObject.FindProperty("tab").intValue == 1)
            {
                InspectConnections(serializedObject);
            }
            else if (serializedObject.FindProperty("tab").intValue == 2)
            {
                InspectTerrain(serializedObject, intersection);
            }
            else if (serializedObject.FindProperty("tab").intValue == 3)
            {
                InspectMainRoads(serializedObject);
            }
            else
            {
                InspectCrosswalks(serializedObject);
            }

            GUILayout.Space(20);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                intersection.Regenerate(true, false);
            }

            if (GUILayout.Button("Flatten Intersection"))
            {
                intersection.Flatten();
            }

            if (GUILayout.Button("Update Intersection"))
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                intersection.Regenerate(false, false);
            }
        }

        private static void InspectGeneral(SerializedObject serializedObject, Intersection intersection)
        {
            serializedObject.FindProperty("detailLevel").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Detail Level", serializedObject.FindProperty("detailLevel").floatValue), 0.01f, 20);
            Utility.ShowListInspector("Main Materials", ref intersection.mainMaterials);
            serializedObject.FindProperty("mainPhysicMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Physic Material", serializedObject.FindProperty("mainPhysicMaterial").objectReferenceValue, typeof(PhysicMaterial), false);
            serializedObject.FindProperty("cornerSharpnessPerCorner").boolValue = EditorGUILayout.Toggle(new GUIContent("Corner Sharpness Factor Per Corner", "If true then this value can be different for each corner"), serializedObject.FindProperty("cornerSharpnessPerCorner").boolValue);

            if (!serializedObject.FindProperty("cornerSharpnessPerCorner").boolValue)
            {
                serializedObject.FindProperty("cornerSharpnessFactor").floatValue = EditorGUILayout.FloatField(new GUIContent("Corner Sharpness Factor", "Determines the sharpness of the corners"), Mathf.Clamp(serializedObject.FindProperty("cornerSharpnessFactor").floatValue, 0.66f, 1));
            }

            if (GUILayout.Button("Recalculate Curve Points"))
            {
                intersection.RecalculateTangents();
            }

            GUILayout.Space(20);
            serializedObject.FindProperty("flipUvs").boolValue = EditorGUILayout.Toggle("Flip Uvs", serializedObject.FindProperty("flipUvs").boolValue);
            serializedObject.FindProperty("uvXScale").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Uv X Scale", serializedObject.FindProperty("uvXScale").floatValue), 0.01f, 10f);
            serializedObject.FindProperty("textureTilingMultiplier").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Texture Tiling Multiplier", serializedObject.FindProperty("textureTilingMultiplier").floatValue), 0.01f, 10f);
            serializedObject.FindProperty("generateColliders").boolValue = EditorGUILayout.Toggle("Generate Colliders", serializedObject.FindProperty("generateColliders").boolValue);

            GUILayout.Space(20);
            GUILayout.Label("LOD", EditorStyles.boldLabel);
            serializedObject.FindProperty("lodLevels").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Lod Levels", "Excludes original mesh"), serializedObject.FindProperty("lodLevels").intValue), 0, 3);

            if (serializedObject.FindProperty("lodLevels").intValue > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("(Distance/Vertex Divison)", "Distance: The distance in percent from the camera that the lod level stops showing, Vertex Division: Vertex Count/Vertex Division is the amount of vertices used for that lod level"));
                GUILayout.EndHorizontal();

                for (int i = 0; i < serializedObject.FindProperty("lodLevels").intValue; i++)
                {
                    if (i < serializedObject.FindProperty("lodDistances").arraySize)
                    {
                        float minDistance = 0;
                        float maxDistance = 1 - (serializedObject.FindProperty("lodLevels").intValue - i) * 0.01f;
                        if (i > 0)
                        {
                            minDistance = serializedObject.FindProperty("lodDistances").GetArrayElementAtIndex(i - 1).floatValue;
                        }

                        int minDivision = 2;
                        if (i > 0)
                        {
                            minDivision = serializedObject.FindProperty("lodVertexDivisions").GetArrayElementAtIndex(i - 1).intValue + 1;
                        }

                        GUILayout.BeginHorizontal();
                        serializedObject.FindProperty("lodDistances").GetArrayElementAtIndex(i).floatValue = Mathf.Clamp(EditorGUILayout.FloatField(serializedObject.FindProperty("lodDistances").GetArrayElementAtIndex(i).floatValue), minDistance + 0.01f, maxDistance);
                        serializedObject.FindProperty("lodVertexDivisions").GetArrayElementAtIndex(i).intValue = Mathf.Max(EditorGUILayout.IntField(serializedObject.FindProperty("lodVertexDivisions").GetArrayElementAtIndex(i).intValue), minDivision);
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        private static void InspectConnections(SerializedObject serializedObject)
        {
            // Generate options
            string[] options = new string[serializedObject.FindProperty("connections").arraySize];
            for (int i = 0; i < serializedObject.FindProperty("connections").arraySize; i++)
            {
                options[i] = (i + 1).ToString();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Connection");
            if (serializedObject.FindProperty("connectionTab").intValue > 0 && GUILayout.Button("←"))
            {
                serializedObject.FindProperty("connectionTab").intValue -= 1;
            }

            serializedObject.FindProperty("connectionTab").intValue = EditorGUILayout.Popup(serializedObject.FindProperty("connectionTab").intValue, options);

            if (serializedObject.FindProperty("connectionTab").intValue < serializedObject.FindProperty("connections").arraySize - 1 && GUILayout.Button("→"))
            {
                serializedObject.FindProperty("connectionTab").intValue += 1;
            }
            EditorGUILayout.EndHorizontal();

            SerializedProperty connection = serializedObject.FindProperty("connections").GetArrayElementAtIndex(serializedObject.FindProperty("connectionTab").intValue);
            connection.FindPropertyRelative("leftCurveOffset").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Left Curve Offset", connection.FindPropertyRelative("leftCurveOffset").floatValue), 0, 5);
            connection.FindPropertyRelative("rightCurveOffset").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Right Curve Offset", connection.FindPropertyRelative("rightCurveOffset").floatValue), 0, 5);

            if (serializedObject.FindProperty("cornerSharpnessPerCorner").boolValue)
            {
                connection.FindPropertyRelative("leftCornerSharpness").floatValue = EditorGUILayout.FloatField(new GUIContent("Left Corner Sharpness Factor", "Determines the sharpness of the corners"), Mathf.Clamp(connection.FindPropertyRelative("leftCornerSharpness").floatValue, 0.66f, 1));
            }

            GUILayout.Space(20);
            GUILayout.Label("Lane Turn Markings", EditorStyles.boldLabel);
            connection.FindPropertyRelative("turnMarkingsRepetitions").intValue = Mathf.Clamp(EditorGUILayout.IntField("Repetitions", connection.FindPropertyRelative("turnMarkingsRepetitions").intValue), 0, 5);

            if (connection.FindPropertyRelative("turnMarkingsRepetitions").intValue > 0)
            {
                connection.FindPropertyRelative("turnMarkingsAmount").intValue = Mathf.Clamp(EditorGUILayout.IntField("Amount (Per Repetition)", connection.FindPropertyRelative("turnMarkingsAmount").intValue), 1, 20);
                connection.FindPropertyRelative("turnMarkingsStartOffset").floatValue = Mathf.Max(EditorGUILayout.FloatField("Start Offset", connection.FindPropertyRelative("turnMarkingsStartOffset").floatValue), 0);
                connection.FindPropertyRelative("turnMarkingsContiniusOffset").floatValue = Mathf.Max(EditorGUILayout.FloatField("Continius Offset", connection.FindPropertyRelative("turnMarkingsContiniusOffset").floatValue), 1);
                connection.FindPropertyRelative("turnMarkingsYOffset").floatValue = Mathf.Clamp01(EditorGUILayout.FloatField("Y Offset", connection.FindPropertyRelative("turnMarkingsYOffset").floatValue));

                GUILayout.Space(20);
                GUILayout.Label("(Left/Forward/Right)");
                // Display checkboxes
                for (int i = 0; i < connection.FindPropertyRelative("turnMarkings").arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("#" + (i + 1));
                    connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("one").boolValue = EditorGUILayout.Toggle(connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("one").boolValue);
                    connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("two").boolValue = EditorGUILayout.Toggle(connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("two").boolValue);
                    connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("three").boolValue = EditorGUILayout.Toggle(connection.FindPropertyRelative("turnMarkings").GetArrayElementAtIndex(i).FindPropertyRelative("three").boolValue);
                    EditorGUILayout.EndHorizontal();
                }

                // Display X-offsets
                GUILayout.Space(20);
                GUILayout.Label("X-Offets");
                connection.FindPropertyRelative("sameXOffsetsForAllRepetitions").boolValue = EditorGUILayout.Toggle("Same X-Offsets For All Repetitions", connection.FindPropertyRelative("sameXOffsetsForAllRepetitions").boolValue);

                if (!connection.FindPropertyRelative("sameXOffsetsForAllRepetitions").boolValue)
                {
                    GUILayout.Label("Vertical: Repeations, Horizontal: Amount");
                }
                else
                {
                    GUILayout.Label("Horizontal: Amount");
                }

                int max = connection.FindPropertyRelative("turnMarkingsXOffsets").arraySize;
                if (connection.FindPropertyRelative("sameXOffsetsForAllRepetitions").boolValue)
                {
                    max = 1;
                }

                for (int i = 0; i < max; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (connection.FindPropertyRelative("turnMarkingsXOffsets").arraySize > i)
                    {
                        for (int j = 0; j < connection.FindPropertyRelative("turnMarkingsXOffsets").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize; j++)
                        {
                            connection.FindPropertyRelative("turnMarkingsXOffsets").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(j).floatValue = EditorGUILayout.FloatField(connection.FindPropertyRelative("turnMarkingsXOffsets").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(j).floatValue, GUILayout.MinWidth(15));
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                connection.FindPropertyRelative("turnMarkings").ClearArray();
            }
        }

        private static void InspectTerrain(SerializedObject serializedObject, Intersection intersection)
        {
            GUILayout.Label("Terrain Deformation", EditorStyles.boldLabel);
            serializedObject.FindProperty("modifyTerrainHeight").boolValue = EditorGUILayout.Toggle("Modify Terrain Height", serializedObject.FindProperty("modifyTerrainHeight").boolValue);

            if (serializedObject.FindProperty("modifyTerrainHeight").boolValue)
            {
                serializedObject.FindProperty("terrainRadius").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Radius", serializedObject.FindProperty("terrainRadius").floatValue), 0, 100);
                serializedObject.FindProperty("terrainSmoothingRadius").intValue = Mathf.Clamp(EditorGUILayout.IntField("Smoothing Radius", serializedObject.FindProperty("terrainSmoothingRadius").intValue), 0, 7);

                if (serializedObject.FindProperty("terrainSmoothingRadius").intValue > 0)
                {
                    serializedObject.FindProperty("terrainSmoothingAmount").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Smoothing Amount", serializedObject.FindProperty("terrainSmoothingAmount").floatValue), 0, 1);
                }

                serializedObject.FindProperty("terrainAngle").floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Angle", "Angle in degrees"), serializedObject.FindProperty("terrainAngle").floatValue), 0, 89);
                serializedObject.FindProperty("terrainExtraMaxHeight").floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Extra Max Height", "The width of the section next to the intersection that has the same height"), serializedObject.FindProperty("terrainExtraMaxHeight").floatValue), 1, 10);
                serializedObject.FindProperty("terrainModificationYOffset").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Y Offset", serializedObject.FindProperty("terrainModificationYOffset").floatValue), 0.01f, 2f);
                serializedObject.FindProperty("modifyTerrainOnUpdate").boolValue = EditorGUILayout.Toggle("Modify Terrain Height On Update", serializedObject.FindProperty("modifyTerrainOnUpdate").boolValue);

                GUILayout.Space(20);
                if (GUILayout.Button("Modify Terrain Height"))
                {
                    intersection.Regenerate(true, true);
                }
            }

            GUILayout.Space(20);
            GUILayout.Label("Detail Removal", EditorStyles.boldLabel);
            serializedObject.FindProperty("terrainRemoveDetails").boolValue = EditorGUILayout.Toggle("Remove Details", serializedObject.FindProperty("terrainRemoveDetails").boolValue);
            if (serializedObject.FindProperty("terrainRemoveDetails").boolValue)
            {
                serializedObject.FindProperty("terrainDetailsRadius").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Remove Detail Radius", serializedObject.FindProperty("terrainDetailsRadius").floatValue), 0, 100);
                serializedObject.FindProperty("terrainRemoveDetailsOnUpdate").boolValue = EditorGUILayout.Toggle("Remove Details On Update", serializedObject.FindProperty("terrainRemoveDetailsOnUpdate").boolValue);

                if (GUILayout.Button("Remove Details"))
                {
                    intersection.Regenerate(true, false, true);
                }
            }

            GUILayout.Space(20);
            GUILayout.Label("Tree Removal", EditorStyles.boldLabel);
            serializedObject.FindProperty("terrainRemoveTrees").boolValue = EditorGUILayout.Toggle("Remove Trees", serializedObject.FindProperty("terrainRemoveTrees").boolValue);
            if (serializedObject.FindProperty("terrainRemoveTrees").boolValue)
            {
                serializedObject.FindProperty("terrainTreesRadius").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Remove Trees Radius", serializedObject.FindProperty("terrainTreesRadius").floatValue), 0, 100);
                serializedObject.FindProperty("terrainRemoveTreesOnUpdate").boolValue = EditorGUILayout.Toggle("Remove Trees On Update", serializedObject.FindProperty("terrainRemoveTreesOnUpdate").boolValue);

                if (GUILayout.Button("Remove Trees"))
                {
                    intersection.Regenerate(true, false, false, true);
                }
            }

            if (serializedObject.FindProperty("modifyTerrainHeight").boolValue && serializedObject.FindProperty("terrainRemoveDetails").boolValue && serializedObject.FindProperty("terrainRemoveTrees").boolValue)
            {
                if (GUILayout.Button("Update Terrain And Remove Details/Trees"))
                {
                    intersection.Regenerate(true, true, true, true);
                }
            }
        }

        private static void InspectMainRoads(SerializedObject serializedObject)
        {
            serializedObject.FindProperty("automaticallyGenerateMainRoads").boolValue = EditorGUILayout.Toggle(new GUIContent("Automatically Generate Main Roads", "Uses a algorithm to connect connected roads that have the same amount of lanes that arn't part of the main intersection. For example if you have two roads that both have a sidewalk consisting of 2 lanes, then the sidewalk will continue through the intersection."), serializedObject.FindProperty("automaticallyGenerateMainRoads").boolValue);

            if (serializedObject.FindProperty("mainRoads").arraySize > 0)
            {
                // Generate options
                string[] options = new string[serializedObject.FindProperty("mainRoads").arraySize];
                for (int i = 0; i < serializedObject.FindProperty("mainRoads").arraySize; i++)
                {
                    options[i] = (i + 1).ToString();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Main Road");
                if (serializedObject.FindProperty("mainRoadTab").intValue > 0 && GUILayout.Button("←"))
                {
                    serializedObject.FindProperty("mainRoadTab").intValue -= 1;
                }

                serializedObject.FindProperty("mainRoadTab").intValue = EditorGUILayout.Popup(serializedObject.FindProperty("mainRoadTab").intValue, options);

                if (serializedObject.FindProperty("mainRoadTab").intValue < serializedObject.FindProperty("mainRoads").arraySize - 1 && GUILayout.Button("→"))
                {
                    serializedObject.FindProperty("mainRoadTab").intValue += 1;
                }
                EditorGUILayout.EndHorizontal();

                // Prevent selecting a main road that doesn't exist
                if (serializedObject.FindProperty("mainRoadTab").intValue > serializedObject.FindProperty("mainRoads").arraySize - 1)
                {
                    serializedObject.FindProperty("mainRoadTab").intValue = 0;
                }

                SerializedProperty mainRoad = serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue);
                // Only show settings for non-generated main roads
                if (mainRoad.FindPropertyRelative("generated").boolValue)
                {
                    GUILayout.Label("You cannot edit a generated main road");
                }
                else
                {
                    // Settings                  
                    mainRoad.FindPropertyRelative("material").objectReferenceValue = EditorGUILayout.ObjectField("Material", mainRoad.FindPropertyRelative("material").objectReferenceValue, typeof(Material), false);
                    mainRoad.FindPropertyRelative("physicMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Physic Material", mainRoad.FindPropertyRelative("physicMaterial").objectReferenceValue, typeof(PhysicMaterial), false);
                    mainRoad.FindPropertyRelative("startIndex").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Start Index", "The index of the connection where the main road starts"), mainRoad.FindPropertyRelative("startIndex").intValue), 0, serializedObject.FindProperty("connections").arraySize - 1);
                    mainRoad.FindPropertyRelative("endIndex").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("End Index", "The index of the connection where the main road ends"), mainRoad.FindPropertyRelative("endIndex").intValue), 0, serializedObject.FindProperty("connections").arraySize - 1);

                    // Prevent the main road starting and ending at the same connection
                    if (mainRoad.FindPropertyRelative("startIndex").intValue == mainRoad.FindPropertyRelative("endIndex").intValue)
                    {
                        mainRoad.FindPropertyRelative("endIndex").intValue += 1;
                        if (mainRoad.FindPropertyRelative("endIndex").intValue > serializedObject.FindProperty("connections").arraySize - 1)
                        {
                            mainRoad.FindPropertyRelative("endIndex").intValue = 0;
                        }
                    }

                    mainRoad.FindPropertyRelative("wholeLeftRoad").boolValue = EditorGUILayout.Toggle(new GUIContent("Whole Start Road", "Should the main road connect to the entire start road?"), mainRoad.FindPropertyRelative("wholeLeftRoad").boolValue);
                    mainRoad.FindPropertyRelative("wholeRightRoad").boolValue = EditorGUILayout.Toggle(new GUIContent("Whole End Road", "Should the main road connect to the entire end road?"), mainRoad.FindPropertyRelative("wholeRightRoad").boolValue);

                    if (!mainRoad.FindPropertyRelative("wholeLeftRoad").boolValue)
                    {
                        int startIndex = mainRoad.FindPropertyRelative("startIndex").intValue;
                        mainRoad.FindPropertyRelative("startIndexLeftRoad").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Start Index Start Road", "The index of the lane that the main road should start the connection to the start road"), mainRoad.FindPropertyRelative("startIndexLeftRoad").intValue), 0, serializedObject.FindProperty("connections").GetArrayElementAtIndex(startIndex).FindPropertyRelative("connectedLanes").arraySize - 1);
                        mainRoad.FindPropertyRelative("endIndexLeftRoad").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("End Index Start Road", "The index of the lane that the main road should end the connection to the start road"), mainRoad.FindPropertyRelative("endIndexLeftRoad").intValue), mainRoad.FindPropertyRelative("startIndexLeftRoad").intValue, serializedObject.FindProperty("connections").GetArrayElementAtIndex(startIndex).FindPropertyRelative("connectedLanes").arraySize - 1);
                    }

                    if (!mainRoad.FindPropertyRelative("wholeRightRoad").boolValue)
                    {
                        int endIndex = mainRoad.FindPropertyRelative("endIndex").intValue;
                        mainRoad.FindPropertyRelative("startIndexRightRoad").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Start Index End Road", "The index of the lane that the main road should start the connection to the end road"), mainRoad.FindPropertyRelative("startIndexRightRoad").intValue), 0, serializedObject.FindProperty("connections").GetArrayElementAtIndex(endIndex).FindPropertyRelative("connectedLanes").arraySize - 1);
                        mainRoad.FindPropertyRelative("endIndexRightRoad").intValue = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("End Index End Road", "The index of the lane that the main road should end the connection to the end road"), mainRoad.FindPropertyRelative("endIndexRightRoad").intValue), mainRoad.FindPropertyRelative("startIndexRightRoad").intValue, serializedObject.FindProperty("connections").GetArrayElementAtIndex(endIndex).FindPropertyRelative("connectedLanes").arraySize - 1);
                    }

                    mainRoad.FindPropertyRelative("yOffset").floatValue = EditorGUILayout.FloatField("Y Offset", mainRoad.FindPropertyRelative("yOffset").floatValue);
                    mainRoad.FindPropertyRelative("flipUvs").boolValue = EditorGUILayout.Toggle("Flip Uvs", mainRoad.FindPropertyRelative("flipUvs").boolValue);
                    mainRoad.FindPropertyRelative("textureTilingMultiplier").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Texture Tiling Multiplier", mainRoad.FindPropertyRelative("textureTilingMultiplier").floatValue), 0.01f, 10f);
                    mainRoad.FindPropertyRelative("textureTilingOffset").floatValue = Mathf.Clamp01(EditorGUILayout.FloatField("Texture Tiling Offset", mainRoad.FindPropertyRelative("textureTilingOffset").floatValue));

                    if (GUILayout.Button("Duplicate"))
                    {
                        serializedObject.FindProperty("mainRoads").InsertArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1);
                        Utility.CopyMainRoadsData(mainRoad, serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1));
                        serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1).FindPropertyRelative("generated").boolValue = false;
                        serializedObject.FindProperty("mainRoadTab").intValue += 1;
                    }
                }
            }

            if (GUILayout.Button("Add"))
            {
                if (serializedObject.FindProperty("mainRoads").arraySize == 0 || serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue).FindPropertyRelative("generated").boolValue)
                {
                    serializedObject.FindProperty("mainRoads").InsertArrayElementAtIndex(0);
                    Utility.CopyMainRoadsData(serializedObject.FindProperty("defaultMainRoad"), serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(0));
                    serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(0).FindPropertyRelative("generated").boolValue = false;
                    serializedObject.FindProperty("mainRoadTab").intValue = 0;
                }
                else
                {
                    serializedObject.FindProperty("mainRoads").InsertArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1);
                    serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1).FindPropertyRelative("generated").boolValue = false;
                    Utility.CopyMainRoadsData(serializedObject.FindProperty("defaultMainRoad"), serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue + 1));
                    serializedObject.FindProperty("mainRoadTab").intValue += 1;
                }
            }

            if ((serializedObject.FindProperty("mainRoads").arraySize == 0 || !serializedObject.FindProperty("mainRoads").GetArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue).FindPropertyRelative("generated").boolValue) && GUILayout.Button("Remove"))
            {
                serializedObject.FindProperty("mainRoads").DeleteArrayElementAtIndex(serializedObject.FindProperty("mainRoadTab").intValue);

                if (serializedObject.FindProperty("mainRoadTab").intValue > 0)
                {
                    serializedObject.FindProperty("mainRoadTab").intValue -= 1;
                }
            }
        }

        private static void InspectCrosswalks(SerializedObject serializedObject)
        {
            serializedObject.FindProperty("generateCrosswalks").boolValue = EditorGUILayout.Toggle(new GUIContent("Generate Crosswalks"), serializedObject.FindProperty("generateCrosswalks").boolValue);

            if (serializedObject.FindProperty("generateCrosswalks").boolValue)
            {
                serializedObject.FindProperty("generateSameCrosswalkForAllConnections").boolValue = EditorGUILayout.Toggle(new GUIContent("Generate Same Crosswalk For All Connections"), serializedObject.FindProperty("generateSameCrosswalkForAllConnections").boolValue);

                if (serializedObject.FindProperty("crosswalks").arraySize > 0)
                {
                    if (serializedObject.FindProperty("generateSameCrosswalkForAllConnections").boolValue)
                    {
                        serializedObject.FindProperty("crosswalkTab").intValue = 0;
                    }
                    else
                    {
                        // Only show option to select crosswalk if they are unique
                        // Generate options
                        string[] options = new string[serializedObject.FindProperty("crosswalks").arraySize];
                        for (int i = 0; i < serializedObject.FindProperty("crosswalks").arraySize; i++)
                        {
                            options[i] = (i + 1).ToString();
                        }

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Crosswalk");
                        if (serializedObject.FindProperty("crosswalkTab").intValue > 0 && GUILayout.Button("←"))
                        {
                            serializedObject.FindProperty("crosswalkTab").intValue -= 1;
                        }

                        serializedObject.FindProperty("crosswalkTab").intValue = EditorGUILayout.Popup(serializedObject.FindProperty("crosswalkTab").intValue, options);

                        if (serializedObject.FindProperty("crosswalkTab").intValue < serializedObject.FindProperty("crosswalks").arraySize - 1 && GUILayout.Button("→"))
                        {
                            serializedObject.FindProperty("crosswalkTab").intValue += 1;
                        }
                        EditorGUILayout.EndHorizontal();

                        // Prevent selecting a crosswalk that doesn't exist
                        if (serializedObject.FindProperty("crosswalkTab").intValue > serializedObject.FindProperty("crosswalks").arraySize - 1)
                        {
                            serializedObject.FindProperty("crosswalkTab").intValue = 0;
                        }
                    }

                    // Settings
                    SerializedProperty crosswalk = serializedObject.FindProperty("crosswalks").GetArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue);
                    if (!serializedObject.FindProperty("generateSameCrosswalkForAllConnections").boolValue)
                    {
                        crosswalk.FindPropertyRelative("connectionIndex").intValue = EditorGUILayout.IntField(new GUIContent("Connection Index", "Which road connection the crosswalk should generate on"), crosswalk.FindPropertyRelative("connectionIndex").intValue); // Is clamped in CheckVariables method
                    }

                    crosswalk.FindPropertyRelative("width").floatValue = EditorGUILayout.FloatField("Width", Mathf.Clamp(crosswalk.FindPropertyRelative("width").floatValue, 0.1f, 5f));
                    crosswalk.FindPropertyRelative("insetDistance").floatValue = EditorGUILayout.FloatField(new GUIContent("Inset Distance", "Distance to inset crosswalk from sidewalks"), Mathf.Clamp(crosswalk.FindPropertyRelative("insetDistance").floatValue, 0f, 0.5f));
                    crosswalk.FindPropertyRelative("anchorAtConnection").boolValue = EditorGUILayout.Toggle(new GUIContent("Anchor At Connection", "If true anchors at connection start, if false anchors at the corners."), crosswalk.FindPropertyRelative("anchorAtConnection").boolValue);
                    crosswalk.FindPropertyRelative("material").objectReferenceValue = EditorGUILayout.ObjectField("Material", crosswalk.FindPropertyRelative("material").objectReferenceValue, typeof(Material), false);
                    crosswalk.FindPropertyRelative("yOffset").floatValue = EditorGUILayout.FloatField("Y Offset", crosswalk.FindPropertyRelative("yOffset").floatValue);
                    crosswalk.FindPropertyRelative("textureTilingMultiplier").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Texture Tiling Multiplier", crosswalk.FindPropertyRelative("textureTilingMultiplier").floatValue), 0.01f, 10f);
                    crosswalk.FindPropertyRelative("textureTilingOffset").floatValue = Mathf.Clamp01(EditorGUILayout.FloatField("Texture Tiling Offset", crosswalk.FindPropertyRelative("textureTilingOffset").floatValue));

                    if (!serializedObject.FindProperty("generateSameCrosswalkForAllConnections").boolValue)
                    {
                        if (GUILayout.Button("Duplicate"))
                        {
                            serializedObject.FindProperty("crosswalks").InsertArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue + 1);
                            Utility.CopyCrosswalkData(crosswalk, serializedObject.FindProperty("crosswalks").GetArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue + 1));
                            serializedObject.FindProperty("crosswalkTab").intValue += 1;
                        }

                        if (GUILayout.Button("Add"))
                        {
                            serializedObject.FindProperty("crosswalks").InsertArrayElementAtIndex(0);
                            Utility.CopyCrosswalkData(serializedObject.FindProperty("defaultCrosswalk"), serializedObject.FindProperty("crosswalks").GetArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue + 1));
                            serializedObject.FindProperty("crosswalkTab").intValue += 1;
                        }

                        if (GUILayout.Button("Remove"))
                        {
                            serializedObject.FindProperty("crosswalks").DeleteArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue);

                            if (serializedObject.FindProperty("crosswalkTab").intValue > 0)
                            {
                                serializedObject.FindProperty("crosswalkTab").intValue -= 1;
                            }
                        }
                    }
                }
                else
                {
                    // Show add even when there aren't any crosswalks
                    if (GUILayout.Button("Add"))
                    {
                        serializedObject.FindProperty("crosswalks").InsertArrayElementAtIndex(0);
                        Utility.CopyCrosswalkData(serializedObject.FindProperty("defaultCrosswalk"), serializedObject.FindProperty("crosswalks").GetArrayElementAtIndex(serializedObject.FindProperty("crosswalkTab").intValue));
                        serializedObject.FindProperty("crosswalkTab").intValue = 0;
                    }
                }
            }
        }

        #endregion
    }
}
