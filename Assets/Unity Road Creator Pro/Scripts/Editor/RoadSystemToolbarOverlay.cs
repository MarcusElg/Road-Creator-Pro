#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace RoadCreatorPro
{
    [Overlay(typeof(SceneView), "roadcreator-toolbar-overlay", "Road System Toolbar", false)]
    public class RoadSystemToolbarOverlay : IMGUIOverlay, ITransientOverlay
    {
        // Action textures
        public static Dictionary<string, Texture> textures = new Dictionary<string, Texture>();

        private static SerializedObject settings;
        private int iconSize;

        public bool visible => ShouldShow();

        private bool ShouldShow()
        {
            // Only show if a roadsystem or prefabline is selected
            GameObject selectedObject = Selection.activeGameObject;
            return selectedObject != null && (selectedObject.GetComponent<PrefabLineCreator>() != null || selectedObject.GetComponent<RoadSystem>() != null);
        }

        public override void OnCreated()
        {
            settings = RoadCreatorSettings.GetSerializedSettings();
            iconSize = settings.FindProperty("roadOptionsIconSize").intValue;

            // Load textures
            // Road and intersection textures
            string[] textures = { "createintersection", "connectintersection", "disconnectintersection", "createroad", "addpoints", "deletepoints", "insertpoints", "splitroad" };
            foreach (string texture in textures)
            {
                LoadTexture(texture);
            }

            // Options
            textures = new string[] { "straightroad", "curvedroad", "ylockenabled", "ylockdisabled", "movepointsindividuallyenabled", "movepointsindividuallydisabled" };
            foreach (string texture in textures)
            {
                LoadSingleTexture(texture);
            }

            displayedChanged += OnDisplayChanged;
        }

        public override void OnWillBeDestroyed()
        {
            displayedChanged -= OnDisplayChanged;
        }

        private void OnDisplayChanged(bool visible)
        {
            ClearAction();
        }

        private void DrawActionButton(GUIContent content, GlobalRoadSystemSettings.Action action)
        {
            if (GUILayout.Button(content, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                ClearAction();
                GlobalRoadSystemSettings.currentAction = action;
            }
        }

        private static void ClearAction()
        {
            GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;

            GameObject activeGameObject = Selection.activeGameObject;
            if (activeGameObject != null)
            {
                if (activeGameObject.GetComponent<RoadSystem>() != null)
                {
                    activeGameObject.GetComponent<RoadSystem>().ClearAction();
                }
                else if (activeGameObject.GetComponent<PrefabLineCreator>() != null)
                {
                    activeGameObject.GetComponent<PrefabLineCreator>().ClearAction();
                }
            }
        }

        public override void OnGUI()
        {
            GUILayout.BeginHorizontal();

            bool drawButtons = !(Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<RoadSystem>() == null || Selection.activeGameObject.GetComponent<RoadSystem>().selectedObject == null);
            bool drawPrefabButtons = !(Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<PrefabLineCreator>() == null);
            if (!drawButtons && !drawPrefabButtons)
            {
                GUILayout.Label("Left click on a road or intersection to select it");

                if (GlobalRoadSystemSettings.currentAction != GlobalRoadSystemSettings.Action.CreateRoad)
                {
                    GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.None;
                }
            }
            else if (drawButtons)
            {
                // Intersection buttons
                DrawActionButton(new GUIContent(GetTexture("createintersection", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Create), "Create Intersection"), GlobalRoadSystemSettings.Action.Create);
                DrawActionButton(new GUIContent(GetTexture("connectintersection", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Connect), "Connect To Intersection"), GlobalRoadSystemSettings.Action.Connect);
                DrawActionButton(new GUIContent(GetTexture("disconnectintersection", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.Disconnect), "Disconnect From Intersection"), GlobalRoadSystemSettings.Action.Disconnect);

                GUILayout.Space(20);
            }

            // Road/prefab line buttons
            if (!drawPrefabButtons)
            {
                DrawActionButton(new GUIContent(GetTexture("createroad", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.CreateRoad), "Create Road"), GlobalRoadSystemSettings.Action.CreateRoad);
            }

            if (drawButtons || drawPrefabButtons)
            {
                DrawActionButton(new GUIContent(GetTexture("addpoints", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.AddPoints), "Add Points"), GlobalRoadSystemSettings.Action.AddPoints);
                DrawActionButton(new GUIContent(GetTexture("deletepoints", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.DeletePoints), "Delete Points"), GlobalRoadSystemSettings.Action.DeletePoints);
                DrawActionButton(new GUIContent(GetTexture("insertpoints", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.InsertPoints), "Insert Points"), GlobalRoadSystemSettings.Action.InsertPoints);
                DrawActionButton(new GUIContent(GetTexture("splitroad", GlobalRoadSystemSettings.currentAction == GlobalRoadSystemSettings.Action.SplitRoad), "Split"), GlobalRoadSystemSettings.Action.SplitRoad);

                if (drawButtons)
                {
                    // Render toggles
                    GUILayout.Space(20);
                    if (GUILayout.Button(new GUIContent(GlobalRoadSystemSettings.createStraightSegment ? textures["straightroad"] : textures["curvedroad"], "Straight/Curved Road"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                    {
                        GlobalRoadSystemSettings.createStraightSegment = !GlobalRoadSystemSettings.createStraightSegment;
                    }
                }
                else
                {
                    GUILayout.Space(20);
                }

                if (GUILayout.Button(new GUIContent(GlobalRoadSystemSettings.yLock ? textures["ylockenabled"] : textures["ylockdisabled"], "Y-Lock For Control Points (Enabled/Disabled)"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                {
                    GlobalRoadSystemSettings.yLock = !GlobalRoadSystemSettings.yLock;
                }

                if (GUILayout.Button(new GUIContent(GlobalRoadSystemSettings.movePointsIndividually ? textures["movepointsindividuallyenabled"] : textures["movepointsindividuallydisabled"], "Move Points Individually (Enabled/Disabled)"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                {
                    GlobalRoadSystemSettings.movePointsIndividually = !GlobalRoadSystemSettings.movePointsIndividually;
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void LoadTexture(string name)
        {
            LoadSingleTexture(name);
            LoadSingleTexture(name + "active");
        }

        private static void LoadSingleTexture(string name)
        {
            textures[name] = Resources.Load("Textures/Ui/" + name) as Texture;
        }

        private static Texture GetTexture(string name, bool active)
        {
            if (active)
            {
                return textures[name + "active"];
            }
            else
            {
                return textures[name];
            }
        }
    }
}
#endif
