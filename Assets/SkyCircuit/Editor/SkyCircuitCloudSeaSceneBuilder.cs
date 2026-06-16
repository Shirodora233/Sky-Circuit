using System.IO;
using SkyCircuit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class SkyCircuitCloudSeaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V0_7_CloudSea.unity";
        private const string MaterialsFolder = "Assets/SkyCircuit/Art/Materials";
        private const string TexturesFolder = "Assets/SkyCircuit/Art/Textures";
        private const string CloudTexturePath = TexturesFolder + "/SC_CloudSeaNoise.png";
        private const string CloudSeaMeshPath = "Assets/SkyCircuit/Art/SC_CloudSeaSurface.asset";
        private const string FogRingMeshPath = "Assets/SkyCircuit/Art/SC_HeightFogRing.asset";
        private const string SkyCloudDomeMeshPath = "Assets/SkyCircuit/Art/SC_SkyCloudDome.asset";
        private const string SkyboxMaterialPath = MaterialsFolder + "/SC_CloudSeaSkybox.mat";
        private const string SceneRevisionMarker = "Cloud Sea Scene Revision 13";
        private const string AutoBuildSessionKey = "SkyCircuit.V07.CloudSea.AutoBuildQueued.v13";

        static SkyCircuitCloudSeaSceneBuilder()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoBuildSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoBuildCloudSeaScene;
        }

        [MenuItem("Sky Circuit/Build V0.7 Cloud Sea Scene")]
        public static void BuildCloudSeaScene()
        {
            EnsureFolders();

            Texture2D cloudTexture = EnsureCloudNoiseTexture();
            Material cloudSeaMaterial = CreateCloudSeaMaterial(cloudTexture);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "V0_7_CloudSea";

            GameObject environmentRoot = new GameObject("Environment");
            GameObject previewRoot = new GameObject("Preview");

            CreateRevisionMarker(environmentRoot.transform);
            ConfigureLightingAndFog();
            CreateCloudSeaSurface(environmentRoot.transform, cloudSeaMaterial);
            CreatePreviewCamera(previewRoot.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.7 cloud sea scene built at {ScenePath}");
        }

        private static void TryAutoBuildCloudSeaScene()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoBuildCloudSeaScene;
                return;
            }

            if (SessionState.GetBool(AutoBuildSessionKey, false))
            {
                return;
            }

            if (IsCurrentCloudSeaSceneBuilt())
            {
                SessionState.SetBool(AutoBuildSessionKey, true);
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, true);
            try
            {
                BuildCloudSeaScene();
            }
            catch
            {
                SessionState.SetBool(AutoBuildSessionKey, false);
                throw;
            }
        }

        private static bool IsCurrentCloudSeaSceneBuilt()
        {
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            string sceneText = File.ReadAllText(ScenePath);
            return sceneText.Contains(SceneRevisionMarker)
                && sceneText.Contains("Cloud Sea Surface")
                && !sceneText.Contains("Low Height Fog Sheet");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
            CreateFolder("Assets/SkyCircuit/Art", "Textures");
            CreateFolder("Assets/SkyCircuit/Art", "Shaders");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Texture2D EnsureCloudNoiseTexture()
        {
            if (File.Exists(CloudTexturePath))
            {
                AssetDatabase.ImportAsset(CloudTexturePath);
                ConfigureCloudTextureImporter();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(CloudTexturePath);
            }

            const int textureSize = 512;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true, false)
            {
                name = "SC_CloudSeaNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            Color[] pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float u = x / (float)textureSize;
                    float v = y / (float)textureSize;

                    float broad = TileableFractalNoise(u, v, 2, 5, 0.55f);
                    float detail = TileableFractalNoise(u + 0.37f, v + 0.19f, 5, 4, 0.48f);
                    float wisps = TileableFractalNoise(u - 0.23f, v + 0.41f, 11, 3, 0.46f);

                    pixels[y * textureSize + x] = new Color(
                        Mathf.SmoothStep(0.22f, 0.88f, broad),
                        Mathf.SmoothStep(0.26f, 0.82f, detail),
                        Mathf.SmoothStep(0.34f, 0.76f, wisps),
                        1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);

            File.WriteAllBytes(CloudTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CloudTexturePath);
            ConfigureCloudTextureImporter();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(CloudTexturePath);
        }

        private static void ConfigureCloudTextureImporter()
        {
            TextureImporter importer = AssetImporter.GetAtPath(CloudTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static float FractalNoise(float u, float v, float scale, int octaves, float persistence)
        {
            float amplitude = 1f;
            float frequency = scale;
            float total = 0f;
            float normalization = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(u * frequency, v * frequency) * amplitude;
                normalization += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return total / normalization;
        }

        private static float TileableFractalNoise(float u, float v, int baseFrequency, int octaves, float persistence)
        {
            float amplitude = 1f;
            int frequency = baseFrequency;
            float total = 0f;
            float normalization = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += TileableNoise(u, v, frequency) * amplitude;
                normalization += amplitude;
                amplitude *= persistence;
                frequency *= 2;
            }

            return total / normalization;
        }

        private static float TileableNoise(float u, float v, int frequency)
        {
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);

            float n00 = Mathf.PerlinNoise(u * frequency, v * frequency);
            float n10 = Mathf.PerlinNoise((u - 1f) * frequency, v * frequency);
            float n01 = Mathf.PerlinNoise(u * frequency, (v - 1f) * frequency);
            float n11 = Mathf.PerlinNoise((u - 1f) * frequency, (v - 1f) * frequency);

            float nx0 = Mathf.Lerp(n00, n10, u);
            float nx1 = Mathf.Lerp(n01, n11, u);
            return Mathf.Lerp(nx0, nx1, v);
        }

        private static Material CreateCloudSeaMaterial(Texture2D cloudTexture)
        {
            Material material = CreateMaterial("SC_CloudSea.mat", "SkyCircuit/Cloud Sea");
            material.SetTexture("_CloudTex", cloudTexture);
            material.SetColor("_VoidColor", new Color(0.33f, 0.58f, 0.82f, 1f));
            material.SetColor("_ThinCloudColor", new Color(0.72f, 0.83f, 0.9f, 1f));
            material.SetColor("_BaseColor", new Color(0.93f, 0.96f, 0.96f, 1f));
            material.SetColor("_HighlightColor", new Color(1f, 1f, 0.98f, 1f));
            material.SetColor("_FogColor", new Color(0.58f, 0.72f, 0.86f, 1f));
            material.SetFloat("_WorldTiling", 0.00016f);
            material.SetFloat("_BandStretch", 1f);
            material.SetFloat("_BandSlant", 0.18f);
            material.SetFloat("_DetailScale", 0.85f);
            material.SetFloat("_CloudCoverage", 0.5f);
            material.SetFloat("_CloudFeather", 0.24f);
            material.SetFloat("_DistanceFogStart", 1600f);
            material.SetFloat("_DistanceFogEnd", 5200f);
            material.SetFloat("_RadialFadeStart", 2400f);
            material.SetFloat("_RadialFadeEnd", 3450f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSkyCloudMaterial(Texture2D cloudTexture)
        {
            Material material = CreateMaterial("SC_SkyCloudLayer.mat", "SkyCircuit/Sky Cloud Layer");
            material.SetTexture("_CloudTex", cloudTexture);
            material.SetColor("_CloudColor", new Color(0.82f, 0.9f, 0.98f, 1f));
            material.SetColor("_HighlightColor", Color.white);
            material.SetFloat("_Alpha", 0.52f);
            material.SetFloat("_Coverage", 0.5f);
            material.SetFloat("_Feather", 0.22f);
            material.SetFloat("_HorizonFadeStart", 0.04f);
            material.SetFloat("_HorizonFadeEnd", 0.24f);
            material.SetFloat("_TopFadeStart", 0.86f);
            material.SetFloat("_TopFadeEnd", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateHeightFogMaterial(Texture2D cloudTexture)
        {
            Material material = CreateMaterial("SC_HeightFog.mat", "SkyCircuit/Height Fog");
            material.SetTexture("_NoiseTex", cloudTexture);
            material.SetColor("_FogColor", new Color(0.58f, 0.72f, 0.86f, 1f));
            material.SetFloat("_Alpha", 0.12f);
            material.SetFloat("_NoiseStrength", 0.18f);
            material.SetFloat("_FogBottom", -60f);
            material.SetFloat("_FogTop", 170f);
            material.SetFloat("_DistanceFadeStart", 1500f);
            material.SetFloat("_DistanceFadeEnd", 4800f);
            material.SetFloat("_WorldTiling", 0.0025f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateMaterial(string fileName, string shaderName)
        {
            string path = $"{MaterialsFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"Could not find shader {shaderName}. Falling back to Universal Render Pipeline/Unlit.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(fileName);
            return material;
        }

        private static void CreateRevisionMarker(Transform parent)
        {
            GameObject marker = new GameObject(SceneRevisionMarker);
            marker.transform.SetParent(parent);
        }

        private static void ConfigureLightingAndFog()
        {
            RenderSettings.skybox = CreateSkyboxMaterial();

            GameObject sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(72f, -28f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.color = new Color(1f, 0.98f, 0.93f);
            RenderSettings.sun = light;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.92f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.86f, 0.92f);
            RenderSettings.ambientGroundColor = new Color(0.72f, 0.8f, 0.86f);
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.72f, 0.86f);
            RenderSettings.fogStartDistance = 3200f;
            RenderSettings.fogEndDistance = 7000f;
        }

        private static Material CreateSkyboxMaterial()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("Could not find Skybox/Procedural shader. Keeping the current skybox.");
                return RenderSettings.skybox;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", new Color(0.58f, 0.74f, 0.9f));
            }

            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", new Color(0.48f, 0.66f, 0.82f));
            }

            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", 0.45f);
            }

            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.018f);
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", 1.04f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateSkyCloudDome(Transform parent, Material material)
        {
            GameObject skyClouds = new GameObject("Sky Cloud Dome");
            skyClouds.transform.SetParent(parent);
            skyClouds.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            MeshFilter filter = skyClouds.AddComponent<MeshFilter>();
            filter.sharedMesh = EnsureSkyCloudDomeMesh();

            MeshRenderer renderer = skyClouds.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            CloudSeaMaterialScroller scroller = skyClouds.AddComponent<CloudSeaMaterialScroller>();
            scroller.Configure(renderer, new Vector2(0.0012f, 0.00035f), Vector2.zero);
        }

        private static Mesh EnsureSkyCloudDomeMesh()
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkyCloudDomeMeshPath);
            if (existingMesh != null && existingMesh.vertexCount > 1500)
            {
                return existingMesh;
            }

            if (existingMesh != null)
            {
                AssetDatabase.DeleteAsset(SkyCloudDomeMeshPath);
            }

            const int radialSegments = 128;
            const int verticalSegments = 24;
            const float radius = 6200f;
            const float centerY = -480f;
            const float minElevation = 4f * Mathf.Deg2Rad;
            const float maxElevation = 84f * Mathf.Deg2Rad;

            Vector3[] vertices = new Vector3[(radialSegments + 1) * (verticalSegments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[radialSegments * verticalSegments * 6];

            for (int y = 0; y <= verticalSegments; y++)
            {
                float v = y / (float)verticalSegments;
                float elevation = Mathf.Lerp(minElevation, maxElevation, v);
                float ringRadius = Mathf.Cos(elevation) * radius;
                float height = Mathf.Sin(elevation) * radius + centerY;

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = x / (float)radialSegments;
                    float angle = u * Mathf.PI * 2f;
                    int index = y * (radialSegments + 1) + x;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * ringRadius, height, Mathf.Sin(angle) * ringRadius);
                    uv[index] = new Vector2(u * 2.4f, v * 1.15f);
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < verticalSegments; y++)
            {
                for (int x = 0; x < radialSegments; x++)
                {
                    int vertexIndex = y * (radialSegments + 1) + x;
                    triangles[triangleIndex++] = vertexIndex;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + radialSegments + 1;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + radialSegments + 2;
                    triangles[triangleIndex++] = vertexIndex + radialSegments + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "SC_SkyCloudDome",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, SkyCloudDomeMeshPath);
            return mesh;
        }

        private static void CreateCloudSeaSurface(Transform parent, Material material)
        {
            GameObject surface = new GameObject("Cloud Sea Surface");
            surface.transform.SetParent(parent);
            surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            MeshFilter filter = surface.AddComponent<MeshFilter>();
            filter.sharedMesh = EnsureCloudSeaSurfaceMesh();

            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            CloudSeaMaterialScroller scroller = surface.AddComponent<CloudSeaMaterialScroller>();
            scroller.Configure(renderer, new Vector2(0.0032f, 0.0014f), new Vector2(-0.0011f, 0.0022f));
        }

        private static Mesh EnsureCloudSeaSurfaceMesh()
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CloudSeaMeshPath);
            if (existingMesh != null)
            {
                AssetDatabase.DeleteAsset(CloudSeaMeshPath);
            }

            const int segments = 220;
            const float size = 7200f;
            const float halfSize = size * 0.5f;
            Vector3[] vertices = new Vector3[(segments + 1) * (segments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * segments * 6];

            for (int z = 0; z <= segments; z++)
            {
                for (int x = 0; x <= segments; x++)
                {
                    float u = x / (float)segments;
                    float v = z / (float)segments;
                    float worldX = Mathf.Lerp(-halfSize, halfSize, u);
                    float worldZ = Mathf.Lerp(-halfSize, halfSize, v);
                    float broad = FractalNoise(u + 13.37f, v - 8.51f, 3.2f, 5, 0.52f);
                    float detail = FractalNoise(u - 2.4f, v + 5.8f, 10f, 4, 0.46f);
                    float height = Mathf.SmoothStep(0.1f, 0.9f, broad) * 32f + detail * 7f - 18f;

                    int index = z * (segments + 1) + x;
                    vertices[index] = new Vector3(worldX, height, worldZ);
                    uv[index] = new Vector2(u * 10f, v * 10f);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int vertexIndex = z * (segments + 1) + x;
                    triangles[triangleIndex++] = vertexIndex;
                    triangles[triangleIndex++] = vertexIndex + segments + 1;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + 1;
                    triangles[triangleIndex++] = vertexIndex + segments + 1;
                    triangles[triangleIndex++] = vertexIndex + segments + 2;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "SC_CloudSeaSurface",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, CloudSeaMeshPath);
            return mesh;
        }

        private static void CreateHeightFogCurtain(Transform parent, Material material)
        {
            Mesh mesh = EnsureFogRingMesh();
            GameObject curtain = new GameObject("Height Fog Curtain");
            curtain.transform.SetParent(parent);

            MeshFilter filter = curtain.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = curtain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            CloudSeaMaterialScroller scroller = curtain.AddComponent<CloudSeaMaterialScroller>();
            scroller.Configure(renderer, new Vector2(0.0015f, 0.0006f), new Vector2(-0.0008f, 0.0012f));
        }

        private static Mesh EnsureFogRingMesh()
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FogRingMeshPath);
            if (existingMesh != null && existingMesh.bounds.size.y > 190f && existingMesh.bounds.extents.x > 3200f)
            {
                return existingMesh;
            }

            if (existingMesh != null)
            {
                AssetDatabase.DeleteAsset(FogRingMeshPath);
            }

            const int segments = 160;
            const float radius = 3500f;
            const float height = 220f;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                int vertexIndex = i * 2;

                vertices[vertexIndex] = new Vector3(x, -50f, z);
                vertices[vertexIndex + 1] = new Vector3(x, height, z);
                uv[vertexIndex] = new Vector2(t, 0f);
                uv[vertexIndex + 1] = new Vector2(t, 1f);

                if (i >= segments)
                {
                    continue;
                }

                int triangleIndex = i * 6;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex + 2;
                triangles[triangleIndex + 4] = vertexIndex + 1;
                triangles[triangleIndex + 5] = vertexIndex + 3;
            }

            Mesh mesh = new Mesh
            {
                name = "SC_HeightFogRing",
                vertices = vertices,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, FogRingMeshPath);
            return mesh;
        }

        private static void CreatePreviewCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 260f, -880f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 20f, 900f) - cameraObject.transform.position, Vector3.up);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.82f, 0.88f, 0.9f);
            camera.fieldOfView = 66f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20000f;
            cameraObject.AddComponent<AudioListener>();
        }
    }
}
