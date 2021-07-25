using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadCreatorPro
{
    public class GenerateSplatmapWindow : EditorWindow
    {
        public int[] textureSizes = { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        public Terrain terrain;
        public int textureSize;
        public string location;
        public bool invertTexture = false;
        public float raycastRadius = 2;

        [MenuItem("Window/Road Creator/Generate Splatmap", false, 2500)]
        public static void ShowWindow()
        {
            GenerateSplatmapWindow window = (GenerateSplatmapWindow)EditorWindow.GetWindow(typeof(GenerateSplatmapWindow));
            window.minSize = new Vector2(400, 235);
            window.maxSize = window.minSize;
            window.titleContent = new GUIContent("Generate Splatmap");
            window.textureSize = 2;
            window.Validate();
        }

        private void Validate()
        {
            if (terrain == null)
            {
                terrain = GameObject.FindObjectOfType<Terrain>();
            }

            if (location == null || location.Length == 0)
            {
                location = "splatmap";
            }

            location = location.Replace(".png", "").Replace(".jpg", ""); // File extension is added automatically
        }

        private void OnGUI()
        {
            terrain = (Terrain)EditorGUILayout.ObjectField(new GUIContent("Terrain", "The terrain that determines location and size"), terrain, typeof(Terrain), true);
            location = EditorGUILayout.TextField("File Location", location);
            textureSize = EditorGUILayout.IntSlider("Texture Size", textureSize, 0, textureSizes.Length - 1);
            GUILayout.Label("Size: " + textureSizes[textureSize] + "x" + textureSizes[textureSize]);
            raycastRadius = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Raycast Radius", "Higher radius creates wider lines on the map"), raycastRadius), 0, 10);
            invertTexture = EditorGUILayout.Toggle("Invert Texture", invertTexture);

            if (textureSize > 5)
            {
                GUILayout.Label("NOTE! When creating an splatmap with larger resolution than \n" +
                    "2048x2048 the target texture has to have the max size\n" +
                    "property increased. This can only be changed manually so export\n" +
                    "once, change the property and export again. Alternatively create\n" +
                    "a texture beforehand and the let the tool override that texture.");
            }

            // Check that values are valid before generating image
            Validate();

            GUILayout.Space(20);
            if (GUILayout.Button(new GUIContent("Generate", "WARNING! Can take some time depending on texture size")))
            {
                Generate();
            }
        }

        private void Generate()
        {
            if (terrain == null)
            {
                return;
            }

            // Create image
            Texture2D texture = new Texture2D(textureSizes[textureSize], textureSizes[textureSize], TextureFormat.RGB24, false);

            float terrainSize = terrain.terrainData.size.x;
            float startX = terrain.transform.position.x;
            float startY = terrain.transform.position.y + terrain.terrainData.size.y + 1;
            float startZ = terrain.transform.position.z;
            float distancePerUnit = terrainSize / textureSizes[textureSize];

            // Cast ray for every pixel
            for (int x = 0; x < textureSizes[textureSize]; x++)
            {
                for (int y = 0; y < textureSizes[textureSize]; y++)
                {
                    // Found object
                    if (Physics.BoxCast(new Vector3(startX + x * distancePerUnit, startY, startZ + y * distancePerUnit), new Vector3(raycastRadius, 1, raycastRadius), Vector3.down, Quaternion.identity, terrain.terrainData.size.y * 1.5f, 1 << LayerMask.NameToLayer("Road") | 1 << LayerMask.NameToLayer("Intersection")))
                    {
                        if (invertTexture)
                        {
                            texture.SetPixel(x, y, Color.black);
                        }
                        else
                        {
                            texture.SetPixel(x, y, Color.white);
                        }
                    }
                    else
                    {
                        // Did not find object
                        if (invertTexture)
                        {
                            texture.SetPixel(x, y, Color.white);
                        }
                        else
                        {
                            texture.SetPixel(x, y, Color.black);
                        }
                    }
                }
            }

            texture.Apply();

            byte[] bytes = texture.EncodeToPNG();
            string path = Application.dataPath + "/" + location + ".png";

            // Replace old texture with new texture
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
        }
    }
}