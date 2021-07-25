using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoadCreatorPro
{
    public class GameObjectMenuButtons : MonoBehaviour
    {
        [MenuItem("GameObject/3D Object/Road System", false, -10)]
        public static void CreateRoadSystem(MenuCommand menuCommand)
        {
            GameObject roadSystem = new GameObject("Road System");
            roadSystem.AddComponent<RoadSystem>();
            GameObjectUtility.SetParentAndAlign(roadSystem, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(roadSystem, "Create Road System");
            Selection.activeGameObject = roadSystem;
            GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.CreateRoad;
        }

        // Shortcut: CTRL + ALT + L
        [MenuItem("GameObject/3D Object/Prefab Line (CTRL + ALT + L) %&l", false, -10)]
        public static void CreatePrefabLine(MenuCommand menuCommand)
        {
            GameObject prefabLine = new GameObject("Prefab Line");
            prefabLine.AddComponent<PrefabLineCreator>();
            GameObjectUtility.SetParentAndAlign(prefabLine, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(prefabLine, "Create Prefab Line");
            SetPosition(prefabLine);
            GlobalRoadSystemSettings.currentAction = GlobalRoadSystemSettings.Action.AddPoints;
        }

        [MenuItem("GameObject/3D Object/Traffic Light", false, -10)]
        public static void CreateTrafficLight(MenuCommand menuCommand)
        {
            GameObject trafficLight = Instantiate(Resources.Load("Prefabs/Traffic Light") as GameObject);
            GameObjectUtility.SetParentAndAlign(trafficLight, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(trafficLight, "Create Traffic Light");
            SetPosition(trafficLight);
        }

        [MenuItem("GameObject/3D Object/Prohibited Area", false, -10)]
        public static void CreateProhibitedArea(MenuCommand menuCommand)
        {
            GameObject area = new GameObject("Prohibited Area");
            area.AddComponent<ProhibitedArea>();
            GameObjectUtility.SetParentAndAlign(area, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(area, "Create Prohibited Area");
            SetPosition(area);
            area.transform.rotation = Quaternion.identity;
        }

        private static void SetPosition(GameObject gameObject)
        {
            RaycastHit raycastHit;
            Camera camera = SceneView.lastActiveSceneView.camera;
            Ray ray = camera.ScreenPointToRay(new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2, 0));

            if (Physics.Raycast(ray, out raycastHit, 1000))
            {
                gameObject.transform.position = raycastHit.point;
                gameObject.transform.rotation = Quaternion.Euler(0, camera.transform.rotation.eulerAngles.y + 180, 0);
                Selection.activeGameObject = gameObject;
            }
        }

        [MenuItem("Window/Road Creator/Save As Prefab", false, 2500)]
        public static void CreatePrefab()
        {
            if (Selection.activeObject == null)
            {
                return;
            }

            SerializedObject settings = RoadCreatorSettings.GetSerializedSettings();

            // Find all meshes
            Component[] meshFilters = Selection.activeGameObject.GetComponentsInChildren(typeof(MeshFilter), true);
            Mesh[] meshes = new Mesh[meshFilters.Length];

            if (meshFilters.Length == 0)
            {
                return;
            }

            // Save meshes to disk
            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh mesh = ((MeshFilter)meshFilters[i]).sharedMesh;
                string path = "Assets/temp" + i;
                AssetDatabase.DeleteAsset(path);

                // Copy mesh to not affect mesh that currently exists in scene
                meshes[i] = Instantiate(mesh);
            }

            // Create prefab
            string assetPath = settings.FindProperty("prefabExportLocation").stringValue + Selection.activeGameObject.name + ".prefab";
            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(Selection.activeGameObject, assetPath);

            // Save meshes to prefab
            for (int i = 0; i < meshFilters.Length; i++)
            {
                AssetDatabase.AddObjectToAsset(meshes[i], prefab);
            }

            AssetDatabase.SaveAssets();

            // Update references
            meshFilters = prefab.GetComponentsInChildren(typeof(MeshFilter), true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                ((MeshFilter)meshFilters[i]).sharedMesh = meshes[i];

                if (meshFilters[i].gameObject.GetComponent<MeshCollider>() != null)
                {
                    meshFilters[i].gameObject.GetComponent<MeshCollider>().sharedMesh = meshes[i];
                }
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Window/Road Creator/Convert To Single Mesh", false, 2500)]
        public static void ConvertToSingleMesh()
        {
            GameObject gameObject = Selection.activeGameObject;
            RoadCreator[] roads = gameObject.GetComponentsInChildren<RoadCreator>();
            PrefabLineCreator[] prefabLines = gameObject.GetComponentsInChildren<PrefabLineCreator>();
            Intersection[] intersections = gameObject.GetComponentsInChildren<Intersection>();
            ProhibitedArea[] prohibitedAreas = gameObject.GetComponentsInChildren<ProhibitedArea>();

            List<MeshFilter> meshFilters = new List<MeshFilter>();
            List<Material> materials = new List<Material>();

            // Get highest level lod mesh and materials
            foreach (RoadCreator road in roads)
            {
                AddMaterialsAndMeshFromMesh(ref materials, ref meshFilters, road.transform.GetChild(1));
            }

            foreach (PrefabLineCreator prefabLine in prefabLines)
            {
                AddMaterialsAndMeshFromMesh(ref materials, ref meshFilters, prefabLine.transform.GetChild(1));
            }

            foreach (Intersection intersection in intersections)
            {
                AddMaterialsAndMeshFromMesh(ref materials, ref meshFilters, intersection.transform.GetChild(0)); // Intersection mesh and main roads
                AddMaterialsAndMeshFromMesh(ref materials, ref meshFilters, intersection.transform.GetChild(1)); // Turn markings
            }

            foreach (ProhibitedArea prohibitedArea in prohibitedAreas)
            {
                AddMaterialsAndMeshFromMesh(ref materials, ref meshFilters, prohibitedArea.transform.GetChild(1));
            }

            // Add meshes
            List<Mesh> subMeshes = new List<Mesh>();
            foreach (Material material in materials)
            {
                // Combine meshes with same material
                List<CombineInstance> combineInstances = new List<CombineInstance>();
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                    Material[] localMaterials = meshRenderer.sharedMaterials;

                    for (int materialIndex = 0; materialIndex < localMaterials.Length; materialIndex++)
                    {
                        if (localMaterials[materialIndex] != material)
                        {
                            continue;
                        }

                        CombineInstance combineInstance = new CombineInstance();
                        combineInstance.mesh = meshFilter.sharedMesh;
                        combineInstance.subMeshIndex = materialIndex;
                        Matrix4x4 matrix = Matrix4x4.identity;
                        matrix.SetTRS(meshFilter.transform.position, meshFilter.transform.rotation, meshFilter.transform.lossyScale);
                        combineInstance.transform = matrix;
                        combineInstances.Add(combineInstance);
                    }
                }

                Mesh mesh = new Mesh();
                mesh.CombineMeshes(combineInstances.ToArray(), true);
                subMeshes.Add(mesh);
            }

            List<CombineInstance> finalCombiners = new List<CombineInstance>();
            foreach (Mesh mesh in subMeshes)
            {
                CombineInstance combineInstance = new CombineInstance();
                combineInstance.mesh = mesh;
                combineInstance.subMeshIndex = 0;
                combineInstance.transform = Matrix4x4.identity;
                finalCombiners.Add(combineInstance);
            }

            Mesh finalMesh = new Mesh();
            finalMesh.CombineMeshes(finalCombiners.ToArray(), false);

            GameObject newMesh = new GameObject("Road System");
            Undo.RegisterCreatedObjectUndo(newMesh, "Create Combined Mesh");
            Utility.AddCollidableMeshAndOtherComponents(ref newMesh, new List<System.Type>());
            newMesh.GetComponent<MeshFilter>().sharedMesh = finalMesh;
            newMesh.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            newMesh.GetComponent<MeshCollider>().sharedMesh = finalMesh;
            Selection.activeGameObject = newMesh;
            Undo.DestroyObjectImmediate(gameObject.gameObject);
        }

        private static void AddMaterialsAndMeshFromMesh(ref List<Material> materials, ref List<MeshFilter> meshFilters, Transform children)
        {
            for (int i = 0; i < children.childCount; i++)
            {
                MeshRenderer meshRenderer = children.GetChild(i).GetComponent<MeshRenderer>();
                meshFilters.Add(children.GetChild(i).GetComponent<MeshFilter>());
                Material[] localMaterials = meshRenderer.sharedMaterials;

                foreach (Material localMaterial in localMaterials)
                {
                    if (!materials.Contains(localMaterial))
                    {
                        materials.Add(localMaterial);
                    }
                }
            }
        }
    }
}
