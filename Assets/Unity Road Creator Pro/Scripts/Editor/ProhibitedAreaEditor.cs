using UnityEditor;

using UnityEngine;

namespace RoadCreatorPro
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ProhibitedArea))]
    public class ProhibitedAreaEditor : Editor
    {
        public int lastPointIndex = -1;
        public int currentMovingPointIndex = -1;

        private void OnEnable()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                ProhibitedArea area = (ProhibitedArea)targets[i];
                area.settings = RoadCreatorSettings.GetSerializedSettings();
                area.InitializeSystem();
                area.currentPoint = null;
                area.Regenerate();
            }

            Tools.current = Tool.None;
            Undo.undoRedoPerformed += UndoProhibitedArea;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoProhibitedArea;
            Tools.current = Tool.Move;
        }

        private void UndoProhibitedArea()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                ((ProhibitedArea)targets[i]).Regenerate();
            }
        }

        public void OnSceneGUI()
        {
            Event currentEvent = Event.current;
            ProhibitedArea area = (ProhibitedArea)target;

            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(area.gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(area.gameObject) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            if (area != null)
            {
                GetCurrentPointIndex(area);

                if (currentEvent.type == EventType.Layout)
                {
                    if (currentEvent.shift)
                    {
                        HandleUtility.AddDefaultControl(0);
                    }

                    for (int i = 0; i < area.transform.GetChild(0).childCount; i++)
                    {
                        float distance = HandleUtility.DistanceToCircle(area.transform.GetChild(0).GetChild(i).position, area.settings.FindProperty("anchorPointSize").floatValue / 2);
                        HandleUtility.AddControl(GetIdForIndex(area, i, 0), distance);
                    }
                }
                else if (currentEvent.type == EventType.MouseDown)
                {
                    if (currentEvent.button == 0)
                    {
                        // Move Points
                        if (currentMovingPointIndex == -1 && area.handleIds.Contains(HandleUtility.nearestControl))
                        {
                            currentMovingPointIndex = area.handleIds.IndexOf(HandleUtility.nearestControl);
                        }
                        else if (currentEvent.shift && currentEvent.alt)
                        {
                            InsertPoint(currentEvent, area);
                        }
                    }
                    else if (currentEvent.button == 1 && currentEvent.shift)
                    {
                        RemovePoint(currentEvent, area);
                    }
                }
                else if (currentEvent.type == EventType.MouseDrag)
                {
                    MovePoint(currentEvent, area);
                }
                else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    currentMovingPointIndex = -1;
                    area.Regenerate();
                }

                // Needs both mouse events, repaint event etc
                Draw(currentEvent, area);

                // Prevent scaling and rotation
                if (area.transform.hasChanged)
                {
                    area.transform.hasChanged = false;
                    area.transform.localRotation = Quaternion.identity;
                    area.transform.localScale = Vector3.one;
                }
            }
        }

        private void GetCurrentPointIndex(ProhibitedArea area)
        {
            // Prevent focus switching to over points when moving
            if (currentMovingPointIndex == -1 && GUIUtility.hotControl == 0 && area.handleIds.Contains(HandleUtility.nearestControl))
            {
                lastPointIndex = area.handleIds.IndexOf(HandleUtility.nearestControl);

                area.currentPoint = area.transform.GetChild(0).GetChild(lastPointIndex);
            }
        }

        public override void OnInspectorGUI()
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(((ProhibitedArea)target).gameObject) != null)
            {
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(((ProhibitedArea)target).gameObject) != PrefabAssetType.NotAPrefab)
            {
                return;
            }

            ProhibitedArea prohibitedArea = (ProhibitedArea)target;
            prohibitedArea.controlsFolded = EditorGUILayout.Foldout(prohibitedArea.controlsFolded, "Show Controls");

            if (prohibitedArea.controlsFolded)
            {
                GUILayout.Label("Insert Point: Shift + Alt + Left click");
                GUILayout.Label("Move point: Left click + Drag alternatively use the movement handles");
                GUILayout.Label("Remove point: Shift + Right click");

                GUILayout.Label("");
            }

            EditorGUI.BeginChangeCheck();

            serializedObject.FindProperty("centerMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Center Material", serializedObject.FindProperty("centerMaterial").objectReferenceValue, typeof(Material), false);
            serializedObject.FindProperty("outerMaterial").objectReferenceValue = EditorGUILayout.ObjectField("Outer Material", serializedObject.FindProperty("outerMaterial").objectReferenceValue, typeof(Material), false);
            serializedObject.FindProperty("uvScale").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Uv Scale", serializedObject.FindProperty("uvScale").floatValue), 0.01f, 10f);
            serializedObject.FindProperty("outerLineWidth").floatValue = Mathf.Clamp(EditorGUILayout.FloatField("Outer Line Width", serializedObject.FindProperty("outerLineWidth").floatValue), 0.05f, 1f);
            serializedObject.FindProperty("centerRotation").floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Center Rotation", "Rotation of the center material in degrees"), serializedObject.FindProperty("centerRotation").floatValue), 0, 360);

            GUILayout.Space(20);

            if (EditorGUI.EndChangeCheck() || GUILayout.Button("Update Prohibited Area"))
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                for (int i = 0; i < targets.Length; i++)
                {
                    ((ProhibitedArea)targets[i]).Regenerate();
                }
            }
        }

        private void Draw(Event currentEvent, ProhibitedArea area)
        {
            Vector3 screenMousePosition = currentEvent.mousePosition;
            screenMousePosition.z = 0;

            for (int i = 0; i < area.transform.GetChild(0).childCount; i++)
            {
                #region Draw Points

                // Main points
                Vector3 screenPosition = HandleUtility.WorldToGUIPoint(area.transform.GetChild(0).GetChild(i).position);
                screenPosition.z = 0;

                if (HandleUtility.nearestControl == GetIdForIndex(area, i, 0))
                {
                    Handles.color = area.settings.FindProperty("selectedAnchorPointColour").colorValue;
                }
                else
                {
                    Handles.color = area.settings.FindProperty("anchorPointColour").colorValue;
                }

                Transform point = area.transform.GetChild(0).GetChild(i);

                Handles.CapFunction shape;
                int shapeIndex = area.settings.FindProperty("pointShape").enumValueIndex;
                if (shapeIndex == 0)
                {
                    shape = Handles.CylinderHandleCap;
                }
                else if (shapeIndex == 1)
                {
                    shape = Handles.SphereHandleCap;
                }
                else if (shapeIndex == 2)
                {
                    shape = Handles.CubeHandleCap;
                }
                else
                {
                    shape = Handles.ConeHandleCap;
                }

                float handleSize = area.settings.FindProperty("anchorPointSize").floatValue;
                handleSize = Mathf.Min(handleSize, HandleUtility.GetHandleSize(point.position) * handleSize);
                shape(GetIdForIndex(area, i, 0), point.position, Quaternion.Euler(270, 0, 0), handleSize, EventType.Repaint);

                // Calculate handle rotation
                Vector3 lookDirection = (area.transform.GetChild(0).GetChild((i + 1) % area.transform.GetChild(0).childCount).transform.position - point.position).normalized;
                lookDirection.y = 0;
                Quaternion handleRotation = Quaternion.LookRotation(lookDirection);

                if (Tools.pivotRotation == PivotRotation.Global)
                {
                    handleRotation = Quaternion.identity;
                }

                if (lastPointIndex == i && currentMovingPointIndex == -1)
                {
                    Undo.RecordObject(point, "Move Point");
                    EditorGUI.BeginChangeCheck();
                    point.position = Utility.DrawPositionHandle(area.settings.FindProperty("scalePointsWhenZoomed").boolValue, area.settings.FindProperty("anchorPointSize").floatValue, point.position + Vector3.up * area.settings.FindProperty("anchorPointSize").floatValue, handleRotation) - Vector3.up * area.settings.FindProperty("anchorPointSize").floatValue;

                    if (EditorGUI.EndChangeCheck())
                    {
                        area.Regenerate();
                    }
                }

                #endregion

                #region Draw Lines
                Handles.color = Color.white;
                Handles.DrawLine(point.transform.position, area.transform.GetChild(0).GetChild((i + 1) % (area.transform.GetChild(0).childCount)).position);
                #endregion
            }

            SceneView.lastActiveSceneView.Repaint();
        }


        private void MovePoint(Event currentEvent, ProhibitedArea area)
        {
            if (currentMovingPointIndex != -1)
            {
                Undo.RecordObject(area.transform.GetChild(0).GetChild(currentMovingPointIndex), "Move Point");
                area.transform.GetChild(0).GetChild(currentMovingPointIndex).position = Utility.GetMousePosition(false, true);
            }
        }

        private void InsertPoint(Event currentEvent, ProhibitedArea area)
        {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            Vector3 mousePosition = Utility.GetMousePosition(false, true);

            for (int i = 0; i < area.transform.GetChild(0).childCount; i++)
            {
                float distance = HandleUtility.DistancePointLine(mousePosition, area.transform.GetChild(0).GetChild(i).position, area.transform.GetChild(0).GetChild((i + 1) % area.transform.GetChild(0).childCount).position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            Vector3 position = Utility.ClosestPointOnLine(mousePosition, area.transform.GetChild(0).GetChild(closestIndex).position, area.transform.GetChild(0).GetChild((closestIndex + 1) % area.transform.GetChild(0).childCount).position);
            if (position != area.transform.GetChild(0).GetChild(closestIndex).position && position != area.transform.GetChild(0).GetChild((closestIndex + 1) % area.transform.GetChild(0).childCount).position)
            {
                int newIndex = closestIndex + 1;
                GameObject point = new GameObject("Point");
                Undo.RegisterCreatedObjectUndo(point, "Insert Point");
                point.transform.SetParent(area.transform.GetChild(0));
                point.transform.SetSiblingIndex(newIndex);
                point.transform.position = position;
                point.hideFlags = HideFlags.NotEditable;

                area.Regenerate();
            }
        }

        private void RemovePoint(Event currentEvent, ProhibitedArea area)
        {
            // Always make sure that the area has atleast 3 points
            if (area.transform.GetChild(0).childCount <= 3)
            {
                Debug.Log("A prohibited area needs to contain atleast 3 points");
                return;
            }

            if (lastPointIndex != -1)
            {
                Vector3 screenMousePosition = currentEvent.mousePosition;
                screenMousePosition.z = 0;

                Vector3 screenPosition = HandleUtility.WorldToGUIPoint(area.transform.GetChild(0).GetChild(lastPointIndex).position);
                screenPosition.z = 0;

                if (HandleUtility.nearestControl == GetIdForIndex(area, lastPointIndex, 0))
                {
                    Undo.DestroyObjectImmediate(area.transform.GetChild(0).GetChild(lastPointIndex).gameObject);
                    RemoveIdForIndex(area, lastPointIndex);

                    lastPointIndex = -1;
                    area.Regenerate();
                    area.Regenerate();
                }
            }
        }

        #region Handle Ids

        private void RemoveIdForIndex(ProhibitedArea area, int index)
        {
            area.handleHashes.RemoveAt(index);
            area.handleIds.RemoveAt(index);
        }

        private int GetIdForIndex(ProhibitedArea area, int index, int point)
        {
            if (area.handleIds.Count <= index)
            {
                AddId(area);
            }

            return area.handleIds[index];
        }

        private void AddId(ProhibitedArea area)
        {
            // Main points
            int hash = ("PointHandle" + area.lastHashIndex).GetHashCode();
            area.handleHashes.Add(hash);
            int id = GUIUtility.GetControlID(hash, FocusType.Passive);
            area.handleIds.Add(id);

            area.lastHashIndex += 1;
        }

        #endregion
    }
}
