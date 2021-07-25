#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace RoadCreatorPro
{
    [Overlay(typeof(SceneView), "roadcreator-point-inspector", "Point Inspector", false)]
    public class SplineInspectorOverlay : IMGUIOverlay, ITransientOverlay
    {
        public bool visible => ShouldShow();

        private bool ShouldShow()
        {
            // Only show if a road, prefabline or prohibitedarea is selected
            GameObject selectedObject = Selection.activeGameObject;
            return selectedObject != null && (selectedObject.GetComponent<PrefabLineCreator>() != null || selectedObject.GetComponent<ProhibitedArea>() != null
             || (selectedObject.GetComponent<RoadSystem>() != null && selectedObject.GetComponent<RoadSystem>().selectedObject != null));
        }

        public override void OnGUI()
        {
            // Get current point from selected object
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null || (selectedObject.GetComponent<PrefabLineCreator>() == null && selectedObject.GetComponent<ProhibitedArea>() == null && (selectedObject.GetComponent<RoadSystem>() == null || selectedObject.GetComponent<RoadSystem>().selectedObject == null)))
            {
                return;
            }

            Transform currentPoint = null;
            int currentPointTypeIndex = 0;

            PrefabLineCreator prefabLine = selectedObject.GetComponent<PrefabLineCreator>();
            ProhibitedArea prohibitedArea = selectedObject.GetComponent<ProhibitedArea>();

            if (prefabLine != null)
            {
                currentPoint = prefabLine.currentPoint;
                currentPointTypeIndex = prefabLine.currentPointTypeIndex;
            }
            else if (prohibitedArea != null)
            {
                currentPoint = prohibitedArea.currentPoint;
                currentPointTypeIndex = 0;
            }
            else
            {
                RoadCreator road = selectedObject.GetComponent<RoadSystem>().lastPointRoad;
                if (road != null)
                {
                    currentPoint = road.currentPoint;
                    currentPointTypeIndex = road.currentPointTypeIndex;
                }
            }

            // Render
            if (currentPoint == null)
            {
                GUILayout.Label("Hover over a road, prefab line or prohibited area point to be able to edit it", EditorStyles.wordWrappedLabel);
            }
            else
            {
                // Update position
                float x, y, z = 0;
                if (currentPointTypeIndex == 0)
                {
                    x = currentPoint.transform.position.x;
                    y = currentPoint.transform.position.y;
                    z = currentPoint.transform.position.z;
                }
                else if (currentPointTypeIndex == 1)
                {
                    x = currentPoint.GetComponent<Point>().leftLocalControlPointPosition.x;
                    y = currentPoint.GetComponent<Point>().leftLocalControlPointPosition.y;
                    z = currentPoint.GetComponent<Point>().leftLocalControlPointPosition.z;
                }
                else
                {
                    x = currentPoint.GetComponent<Point>().rightLocalControlPointPosition.x;
                    y = currentPoint.GetComponent<Point>().rightLocalControlPointPosition.y;
                    z = currentPoint.GetComponent<Point>().rightLocalControlPointPosition.z;
                }

                EditorGUI.BeginChangeCheck();
                ShowButtons(ref x, "X");
                ShowButtons(ref y, "Y");
                ShowButtons(ref z, "Z");

                if (EditorGUI.EndChangeCheck())
                {
                    if (currentPointTypeIndex == 0)
                    {
                        Undo.RecordObject(currentPoint.transform, "Move Point");
                        currentPoint.transform.position = new Vector3(x, y, z);
                    }
                    else if (currentPointTypeIndex == 1)
                    {
                        Undo.RegisterCompleteObjectUndo(currentPoint, "Move Point");
                        currentPoint.GetComponent<Point>().leftLocalControlPointPosition = new Vector3(x, y, z);
                    }
                    else
                    {
                        Undo.RegisterCompleteObjectUndo(currentPoint, "Move Point");
                        currentPoint.GetComponent<Point>().rightLocalControlPointPosition = new Vector3(x, y, z);
                    }

                    // Update road/prefab line
                    Transform parentObject = currentPoint.transform.parent.parent;
                    if (parentObject.GetComponent<PointSystemCreator>() != null)
                    {
                        parentObject.GetComponent<PointSystemCreator>().Regenerate(false);
                    }
                    else
                    {
                        // Update prohibited area
                        parentObject.GetComponent<ProhibitedArea>().Regenerate();
                    }
                }
            }
        }

        public void ShowButtons(ref float axis, string label)
        {
            GUILayout.BeginHorizontal();

            // Move negative
            AddChangeButton(-1f, ref axis);
            AddChangeButton(-0.1f, ref axis);
            AddChangeButton(-0.01f, ref axis);

            // Move custom
            EditorGUIUtility.labelWidth = 15;
            axis = EditorGUILayout.FloatField(label + ":", axis);

            // Move positive
            AddChangeButton(1f, ref axis);
            AddChangeButton(0.1f, ref axis);
            AddChangeButton(0.01f, ref axis);

            // Copy and paste
            if (GUILayout.Button("Copy"))
            {
                EditorGUIUtility.systemCopyBuffer = "POINTPOSITION" + axis;
            }

            if (GUILayout.Button("Paste"))
            {
                if (EditorGUIUtility.systemCopyBuffer.StartsWith("POINTPOSITION"))
                {
                    axis = float.Parse(EditorGUIUtility.systemCopyBuffer.Replace("POINTPOSITION", ""));
                }
            }

            GUILayout.EndHorizontal();
        }

        private void AddChangeButton(float change, ref float axis)
        {
            if (GUILayout.Button((change >= 0 ? "+" : "") + change))
            {
                axis += change;
            }
        }
    }
}

#endif
