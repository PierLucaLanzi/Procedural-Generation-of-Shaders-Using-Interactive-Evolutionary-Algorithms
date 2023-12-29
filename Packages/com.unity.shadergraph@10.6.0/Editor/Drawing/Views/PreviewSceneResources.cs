using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;

using System.Collections.Generic;

namespace UnityEditor.ShaderGraph.Drawing
{
    class PreviewSceneResources : IDisposable
    {
        readonly Scene m_Scene;
        Camera m_Camera;
        public Light light0 { get; private set; }
        public Light light1 { get; private set; }

        Material m_CheckerboardMaterial;
        Material m_BlitNoAlphaMaterial;

        static readonly Mesh[] s_Meshes = { null, null, null, null, null };
        static readonly GUIContent[] s_MeshIcons = { null, null, null, null, null };
        static readonly GUIContent[] s_LightIcons = { null, null };
        static readonly GUIContent[] s_TimeIcons = { null, null };

        static GameObject CreateLight()
        {
            GameObject lightGO = EditorUtility.CreateGameObjectWithHideFlags("PreRenderLight", HideFlags.HideAndDontSave, typeof(Light));
            var light = lightGO.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.enabled = false;
            return lightGO;
        }

        public PreviewSceneResources()
        {
            m_Scene = EditorSceneManager.NewPreviewScene();
            var camGO = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(camGO, m_Scene);

            m_Camera = camGO.GetComponent<Camera>();
            EditorUtility.SetCameraAnimateMaterials(camera, true);

            camera.cameraType = CameraType.Preview;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = 15;
            camera.farClipPlane = 10.0f;
            camera.nearClipPlane = 2.0f;
            camera.backgroundColor = new Color(49.0f / 255.0f, 49.0f / 255.0f, 49.0f / 255.0f, 1.0f);

            // Explicitly use forward rendering for all previews
            // (deferred fails when generating some static previews at editor launch; and we never want
            // vertex lit previews if that is chosen in the player settings)
            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.scene = m_Scene;

            var l0 = CreateLight();
            SceneManager.MoveGameObjectToScene(l0, m_Scene);

            //previewScene.AddGameObject(l0);
            light0 = l0.GetComponent<Light>();

            var l1 = CreateLight();
            SceneManager.MoveGameObjectToScene(l1, m_Scene);

            //previewScene.AddGameObject(l1);
            light1 = l1.GetComponent<Light>();

            light0.color = new Color(0.769f, 0.769f, 0.769f, 1); // SceneView.kSceneViewFrontLight
            light1.transform.rotation = Quaternion.Euler(340, 218, 177);
            light1.color = new Color(.4f, .4f, .45f, 0f) * .7f;

            m_CheckerboardMaterial = new Material(Shader.Find("Hidden/Checkerboard"));
            m_BlitNoAlphaMaterial = new Material(Shader.Find("Hidden/BlitNoAlpha"));
            checkerboardMaterial.hideFlags = HideFlags.HideAndDontSave;
            blitNoAlphaMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (s_Meshes[0] == null)
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");

                // @TODO: temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    var meshFilter = t.GetComponent<MeshFilter>();
                    switch (t.name)
                    {
                        case "sphere":
                            s_Meshes[0] = meshFilter.sharedMesh;
                            break;
                        case "cube":
                            s_Meshes[1] = meshFilter.sharedMesh;
                            break;
                        case "cylinder":
                            s_Meshes[2] = meshFilter.sharedMesh;
                            break;
                        case "torus":
                            s_Meshes[3] = meshFilter.sharedMesh;
                            break;
                        default:
                            Debug.LogWarning("Something is wrong, weird object found: " + t.name);
                            break;
                    }
                }

                s_MeshIcons[0] = EditorGUIUtility.IconContent("PreMatSphere");
                s_MeshIcons[1] = EditorGUIUtility.IconContent("PreMatCube");
                s_MeshIcons[2] = EditorGUIUtility.IconContent("PreMatCylinder");
                s_MeshIcons[3] = EditorGUIUtility.IconContent("PreMatTorus");
                s_MeshIcons[4] = EditorGUIUtility.IconContent("PreMatQuad");

                s_LightIcons[0] = EditorGUIUtility.IconContent("PreMatLight0");
                s_LightIcons[1] = EditorGUIUtility.IconContent("PreMatLight1");

                s_TimeIcons[0] = EditorGUIUtility.IconContent("PlayButton");
                s_TimeIcons[1] = EditorGUIUtility.IconContent("PauseButton");

                Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
                s_Meshes[4] = quadMesh;
            }
        }

        public Mesh sphere
        {
            get { return s_Meshes[0]; }
        }

        public Mesh quad
        {
            get { return s_Meshes[4]; }
        }

        public Material checkerboardMaterial
        {
            get { return m_CheckerboardMaterial; }
        }

        public Material blitNoAlphaMaterial
        {
            get { return m_BlitNoAlphaMaterial; }
        }

        public Camera camera
        {
            get { return m_Camera; }
        }

        public void Dispose()
        {
            if (light0 != null)
            {
                UnityEngine.Object.DestroyImmediate(light0.gameObject);
                light0 = null;
            }

            if (light1 != null)
            {
                UnityEngine.Object.DestroyImmediate(light1.gameObject);
                light1 = null;
            }

            if (camera != null)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                m_Camera = null;
            }

            if (checkerboardMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(checkerboardMaterial, true);
                m_CheckerboardMaterial = null;
            }
            if (blitNoAlphaMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(blitNoAlphaMaterial, true);
                m_BlitNoAlphaMaterial = null;
            }

            EditorSceneManager.ClosePreviewScene(m_Scene);
        }
    }
}


namespace AutoGen
{
    public static class StageSelector
    {
        private static List<Type> stages = new List<Type>()
        {
            typeof(LegacySceneResources),
            typeof(CornellBoxSceneResources),
            typeof(CheckerBoardSceneResources),
            typeof(DarkSceneResources)
        };

        public static int stagesNumber
        {
            get { return stages.Count; }
            private set { }
        }

        public static int getUsableStageIndex(int index)
        {
            return Mathf.Clamp(index, 0, stagesNumber-1);
        }

        public static AbstractPreviewSceneResources getStage(int index)
        {
            return (AbstractPreviewSceneResources)Activator.CreateInstance(stages[Mathf.Clamp(index, 0, stagesNumber-1)]);
        }

    }
    public abstract class AbstractPreviewSceneResources : IDisposable
    {
        protected static Color defaultUnityLightColor = new Color(1.0f, 244.0f / 255.0f, 214.0f / 255.0f);
        protected Scene previewScene;

        protected ulong cullingMask;

        public string name
        {
            get;
            protected set;
        }

        public Light light0 {
            get;
            protected set;
        }
        public Light light1
        {
            get;
            protected set;
        }
        public Camera camera
        {
            get;
            protected set;
        }
        public Mesh quad
        {
            get;
            protected set;
        }
        public Mesh sphere
        {
            get;
            protected set;
        }
        public Material checkerboardMaterial
        {
            get;
            protected set;
        }
        public Material blitNoAlphaMaterial
        {
            get;
            protected set;
        }
        
        protected GameObject shadowMesh;

        public abstract void Dispose();

        public abstract void addMeshForShadows(Mesh mesh,Matrix4x4 transform);

        public abstract void updateScene();

        protected abstract void setupRenderSettings();

        public void closeScene() {

            if (!EditorSceneManager.ClosePreviewScene(previewScene)) Debug.LogError("Scene was not closed!");
            try
            {
                AsyncOperation closeOp = SceneManager.UnloadSceneAsync(previewScene);
            }
            catch { }
        }

       

    }

    class CornellBoxSceneResources : AbstractPreviewSceneResources, IDisposable
    {
        Camera previewCamera;

        Material errorMaterial;
        Material m_BlitNoAlphaMaterial;
        
        static readonly Mesh[] s_Meshes = { null, null, null, null, null };
        static readonly GUIContent[] s_MeshIcons = { null, null, null, null, null };
        static readonly GUIContent[] s_LightIcons = { null, null };
        static readonly GUIContent[] s_TimeIcons = { null, null };


        // SIMPLIFICATIONS
        #region Simplifications


        #endregion


        static GameObject CreateLight(LightType type)
        {
            GameObject lightGO = EditorUtility.CreateGameObjectWithHideFlags("PreRenderLight", HideFlags.HideAndDontSave, typeof(Light));
            var light = lightGO.GetComponent<Light>();
            light.color = defaultUnityLightColor;
            light.type = type;
           
            light.shadows = LightShadows.Soft;
            light.intensity = 0.5f;
            light.range = 10f;
            light.enabled = false;
            return lightGO;
        }

        public CornellBoxSceneResources()
        {
            /*
                * Basic Setup FOr all Scene Rseources
                */

            name = "Cornell Box";

            try
            {
                cullingMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
            }
            catch
            {
                Debug.LogError("Error while trying to fetch an available culling mask. Please check the number of active previews");
            }
            previewScene = EditorSceneManager.NewPreviewScene();
            cullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
            var cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            previewCamera = cameraObject.GetComponent<Camera>();


            errorMaterial = new Material(Shader.Find("Hidden/Checkerboard"));
            errorMaterial.hideFlags = HideFlags.HideAndDontSave;
            m_BlitNoAlphaMaterial = new Material(Shader.Find("Hidden/BlitNoAlpha"));
            m_BlitNoAlphaMaterial.hideFlags = HideFlags.HideAndDontSave;
            checkerboardMaterial = errorMaterial;
            blitNoAlphaMaterial = m_BlitNoAlphaMaterial;


            // Adding the Camera
            EditorUtility.SetCameraAnimateMaterials(previewCamera, true);

            previewCamera.cameraType = CameraType.Preview;
            camera = previewCamera;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = 19;
            camera.farClipPlane = 40.001f;
            camera.nearClipPlane = 0.1f;
            camera.backgroundColor = new Color(10.0f / 255.0f, 100.0f / 255.0f, 100.0f / 255.0f, 1.0f);

            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.orthographic = false;
            camera.scene = previewScene;
            camera.clearFlags = CameraClearFlags.Skybox;

            // TODO on scene active
            /*
                * RenderSettings.skybox = skyboxMaterial;
                */


            Vector3 sharedOffset = new Vector3(0f, 0f, 2.75f);
            camera.transform.position = new Vector3(0f, 0f, -5f) - sharedOffset;

            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");

            Material skyboxMaterial = Resources.Load<Material>("SGA_CornellSkybox");
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
            else
            {
                Debug.LogError("Material is null");
            }

            Material leftMaterial = new Material(standardShader);
            leftMaterial.color = Color.red;
            Material rightMaterial = new Material(standardShader);
            rightMaterial.color = Color.green;
            Material boxMaterial = new Material(standardShader);
            boxMaterial.color = Color.white;

            #region Creating Meshes in Scene
            var backQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var leftQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var rightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var topQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var bottomQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);

            leftQuad.GetComponent<MeshRenderer>().material = leftMaterial;
            rightQuad.GetComponent<MeshRenderer>().material = rightMaterial;
            bottomQuad.GetComponent<MeshRenderer>().material = boxMaterial;
            topQuad.GetComponent<MeshRenderer>().material = boxMaterial;
            backQuad.GetComponent<MeshRenderer>().material = boxMaterial;

            SceneManager.MoveGameObjectToScene(backQuad, previewScene);
            SceneManager.MoveGameObjectToScene(leftQuad, previewScene);
            SceneManager.MoveGameObjectToScene(rightQuad, previewScene);
            SceneManager.MoveGameObjectToScene(bottomQuad, previewScene);
            SceneManager.MoveGameObjectToScene(topQuad, previewScene);

            backQuad.transform.localScale = new Vector3(2f, 2f, 1f);
            leftQuad.transform.localScale = new Vector3(4f, 2f, 1f);
            rightQuad.transform.localScale = new Vector3(4f, 2f, 1f);
            topQuad.transform.localScale = new Vector3(2f, 4f, 1f);
            bottomQuad.transform.localScale = new Vector3(2f, 4f, 1f);

            backQuad.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f),Quaternion.identity);  
            leftQuad.transform.SetPositionAndRotation( new Vector3(-1f, 0f,0f), Quaternion.AngleAxis(-90f,Vector3.up));
            rightQuad.transform.SetPositionAndRotation( new Vector3(1f, 0f, 0f),Quaternion.AngleAxis(90f,Vector3.up));
            bottomQuad.transform.SetPositionAndRotation( new Vector3(0f, -1f, 0f),Quaternion.AngleAxis(90f,Vector3.right));
            topQuad.transform.SetPositionAndRotation( new Vector3(0f, 1f, 0f), Quaternion.AngleAxis(-90f, Vector3.right));

            topQuad.GetComponent<MeshRenderer>().receiveShadows = true;
            bottomQuad.GetComponent<MeshRenderer>().receiveShadows = true;
            leftQuad.GetComponent<MeshRenderer>().receiveShadows = true;
            rightQuad.GetComponent<MeshRenderer>().receiveShadows = true;
            backQuad.GetComponent<MeshRenderer>().receiveShadows = true;

            topQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            backQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            leftQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            rightQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            bottomQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            #endregion

            var light0Object = CreateLight(LightType.Directional);
            var light1Object = CreateLight(LightType.Point);


            SceneManager.MoveGameObjectToScene(light0Object, previewScene);
            SceneManager.MoveGameObjectToScene(light1Object, previewScene);

            light0 = light0Object.GetComponent<Light>();
            light1 = light1Object.GetComponent<Light>();
            light0.transform.SetPositionAndRotation(new Vector3(0f, 0f, -5f), Quaternion.AngleAxis(-25f, Vector3.right));
            light1.transform.position = new Vector3(0f, 0.9f, 0f);
            light0.range = 1f;
            light0.enabled = true;
            light1.enabled = true;

            // Generating preview meshes
            if (s_Meshes[0] == null)
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");

                // @TODO: temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    var meshFilter = t.GetComponent<MeshFilter>();
                    switch (t.name)
                    {
                        case "sphere":
                            s_Meshes[0] = meshFilter.sharedMesh;
                            break;
                        case "cube":
                            s_Meshes[1] = meshFilter.sharedMesh;
                            break;
                        case "cylinder":
                            s_Meshes[2] = meshFilter.sharedMesh;
                            break;
                        case "torus":
                            s_Meshes[3] = meshFilter.sharedMesh;
                            break;
                        default:
                            Debug.LogWarning("Something is wrong, weird object found: " + t.name);
                            break;
                    }
                }

                s_MeshIcons[0] = EditorGUIUtility.IconContent("PreMatSphere");
                s_MeshIcons[1] = EditorGUIUtility.IconContent("PreMatCube");
                s_MeshIcons[2] = EditorGUIUtility.IconContent("PreMatCylinder");
                s_MeshIcons[3] = EditorGUIUtility.IconContent("PreMatTorus");
                s_MeshIcons[4] = EditorGUIUtility.IconContent("PreMatQuad");

                s_LightIcons[0] = EditorGUIUtility.IconContent("PreMatLight0");
                s_LightIcons[1] = EditorGUIUtility.IconContent("PreMatLight1");

                s_TimeIcons[0] = EditorGUIUtility.IconContent("PlayButton");
                s_TimeIcons[1] = EditorGUIUtility.IconContent("PauseButton");

                Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
                s_Meshes[4] = quadMesh;
                
            }
            sphere = s_Meshes[0];
            quad = s_Meshes[4];

        }


        protected override void setupRenderSettings() { }

        public override void Dispose()
        {

            if (light0 != null)
            {
                UnityEngine.Object.DestroyImmediate(light0.gameObject);
                light0 = null;
            }

            if (light1 != null)
            {
                UnityEngine.Object.DestroyImmediate(light1.gameObject);
                light1 = null;
            }

            if (camera != null)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                previewCamera = null;
            }

            if (errorMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(errorMaterial, true);
                errorMaterial = null;
            }
            if (blitNoAlphaMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(blitNoAlphaMaterial, true);
                m_BlitNoAlphaMaterial = null;
            }

        }

        public override void addMeshForShadows(Mesh mesh, Matrix4x4 transform)
        {
            if(shadowMesh == null)
            {
                shadowMesh = new GameObject();
                shadowMesh.AddComponent<MeshFilter>();
                shadowMesh.AddComponent<MeshRenderer>();
                Material basicMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shadowMesh.GetComponent<MeshRenderer>().material = basicMat;
                shadowMesh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                shadowMesh.GetComponent<MeshRenderer>().receiveShadows = true;
                SceneManager.MoveGameObjectToScene(shadowMesh, previewScene);

            }
            if(shadowMesh.GetComponent<MeshFilter>().sharedMesh != mesh) shadowMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadowMesh.transform.rotation = transform.rotation ;
            shadowMesh.transform.localScale = transform.lossyScale;
            shadowMesh.transform.position = transform.GetColumn(3);
        }

        public override void updateScene() { }


    }


    class CheckerBoardSceneResources : AbstractPreviewSceneResources, IDisposable
    {
        
        Camera previewCamera;

        Material errorMaterial;
        Material m_BlitNoAlphaMaterial;

        static readonly Mesh[] s_Meshes = { null, null, null, null, null };
        static readonly GUIContent[] s_MeshIcons = { null, null, null, null, null };
        static readonly GUIContent[] s_LightIcons = { null, null };
        static readonly GUIContent[] s_TimeIcons = { null, null };


        // SIMPLIFICATIONS
        #region Simplifications


        #endregion


        static GameObject CreateLight(LightType type)
        {
            GameObject lightGO = EditorUtility.CreateGameObjectWithHideFlags("PreRenderLight", HideFlags.HideAndDontSave, typeof(Light));
            var light = lightGO.GetComponent<Light>();
            light.color = defaultUnityLightColor;
            light.type = type;
           
            light.shadows = LightShadows.Soft;
            light.lightShadowCasterMode = LightShadowCasterMode.Default;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.Auto;
            light.shadowNearPlane = 0.01f;
            light.intensity = 5f;
            light.range = 10f;
            light.enabled = false;
            return lightGO;
        }

        public CheckerBoardSceneResources()
        {
            /*
                * Basic Setup FOr all Scene Rseources
                */
            name = "Checkerboard Pavement";

            try
            {
                cullingMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
            }
            catch
            {
                Debug.LogError("Error while trying to fetch an available culling mask. Please check the number of active previews");
            }
            previewScene = EditorSceneManager.NewPreviewScene();
            cullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
            var cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            previewCamera = cameraObject.GetComponent<Camera>();


            errorMaterial = new Material(Shader.Find("Hidden/Checkerboard"));
            errorMaterial.hideFlags = HideFlags.HideAndDontSave;
            m_BlitNoAlphaMaterial = new Material(Shader.Find("Hidden/BlitNoAlpha"));
            m_BlitNoAlphaMaterial.hideFlags = HideFlags.HideAndDontSave;
            checkerboardMaterial = errorMaterial;
            blitNoAlphaMaterial = m_BlitNoAlphaMaterial;


            // Adding the Camera
            EditorUtility.SetCameraAnimateMaterials(previewCamera, true);

            previewCamera.cameraType = CameraType.Preview;
            camera = previewCamera;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = 19;
            camera.farClipPlane = 40.001f;
            camera.nearClipPlane = 0.1f;
            camera.backgroundColor = new Color(10.0f / 255.0f, 100.0f / 255.0f, 100.0f / 255.0f, 1.0f);

            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.orthographic = false;
            camera.scene = previewScene;
            camera.clearFlags = CameraClearFlags.Skybox;

            // TODO on scene active
            /*
                * RenderSettings.skybox = skyboxMaterial;
                */


            Vector3 sharedOffset = new Vector3(0f, 0f, 2.75f);
            camera.transform.position = new Vector3(0f, 0f, -5f) - sharedOffset;

            //Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");

            Material skyboxMaterial = Resources.Load<Material>("CheckerboardSkybox");
            Material pavementMaterial = Resources.Load<Material>("Pavement");
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
            else
            {
                Debug.LogError("Material is null");
            }




            var checkerBoardPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);

            checkerBoardPlane.GetComponent<MeshRenderer>().material = pavementMaterial;

            SceneManager.MoveGameObjectToScene(checkerBoardPlane, previewScene);

            checkerBoardPlane.transform.localScale = Vector3.one;

            checkerBoardPlane.transform.SetPositionAndRotation(new Vector3(0f, -0.85f, 0f), Quaternion.identity);

            checkerBoardPlane.GetComponent<MeshRenderer>().receiveShadows = true;

            checkerBoardPlane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            

            var light0Object = CreateLight(LightType.Spot);
            //var light1Object = CreateLight(LightType.Point);


            SceneManager.MoveGameObjectToScene(light0Object, previewScene);
            //SceneManager.MoveGameObjectToScene(light1Object, previewScene);

            light0 = light0Object.GetComponent<Light>();
            //light1 = light1Object.GetComponent<Light>();
            light0.transform.SetPositionAndRotation(new Vector3(0f, 2.75f, 0f), Quaternion.AngleAxis(90, Vector3.right));
            //light1.transform.position = new Vector3(0f, 0.9f, 0f);
            light0.range = 10f;
            light0.innerSpotAngle = 0f;
            light0.spotAngle = 120f;
            light0.enabled = true;
            //light1.enabled = true;

            // Generating preview meshes
            if (s_Meshes[0] == null)
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");

                // @TODO: temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    var meshFilter = t.GetComponent<MeshFilter>();
                    switch (t.name)
                    {
                        case "sphere":
                            s_Meshes[0] = meshFilter.sharedMesh;
                            break;
                        case "cube":
                            s_Meshes[1] = meshFilter.sharedMesh;
                            break;
                        case "cylinder":
                            s_Meshes[2] = meshFilter.sharedMesh;
                            break;
                        case "torus":
                            s_Meshes[3] = meshFilter.sharedMesh;
                            break;
                        default:
                            Debug.LogWarning("Something is wrong, weird object found: " + t.name);
                            break;
                    }
                }

                s_MeshIcons[0] = EditorGUIUtility.IconContent("PreMatSphere");
                s_MeshIcons[1] = EditorGUIUtility.IconContent("PreMatCube");
                s_MeshIcons[2] = EditorGUIUtility.IconContent("PreMatCylinder");
                s_MeshIcons[3] = EditorGUIUtility.IconContent("PreMatTorus");
                s_MeshIcons[4] = EditorGUIUtility.IconContent("PreMatQuad");

                s_LightIcons[0] = EditorGUIUtility.IconContent("PreMatLight0");
                s_LightIcons[1] = EditorGUIUtility.IconContent("PreMatLight1");

                s_TimeIcons[0] = EditorGUIUtility.IconContent("PlayButton");
                s_TimeIcons[1] = EditorGUIUtility.IconContent("PauseButton");

                Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
                s_Meshes[4] = quadMesh;

            }
            sphere = s_Meshes[0];
            quad = s_Meshes[4];

        }

        protected override void setupRenderSettings() {}
        
        public override void Dispose()
        {

            if (light0 != null)
            {
                UnityEngine.Object.DestroyImmediate(light0.gameObject);
                light0 = null;
            }

            if (light1 != null)
            {
                UnityEngine.Object.DestroyImmediate(light1.gameObject);
                light1 = null;
            }

            if (camera != null)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                previewCamera = null;
            }

            if (errorMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(errorMaterial, true);
                errorMaterial = null;
            }
            if (blitNoAlphaMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(blitNoAlphaMaterial, true);
                m_BlitNoAlphaMaterial = null;
            }

        }

        public override void addMeshForShadows(Mesh mesh, Matrix4x4 transform)
        {
        }

        public override void updateScene() { }
    }


    class LegacySceneResources : AbstractPreviewSceneResources, IDisposable
    {
        
        /*
        Camera m_Camera;
        Material m_CheckerboardMaterial;
        Material m_BlitNoAlphaMaterial;
        */
        static readonly Mesh[] s_Meshes = { null, null, null, null, null };
        static readonly GUIContent[] s_MeshIcons = { null, null, null, null, null };
        static readonly GUIContent[] s_LightIcons = { null, null };
        static readonly GUIContent[] s_TimeIcons = { null, null };

        static GameObject CreateLight()
        {
            GameObject lightGO = EditorUtility.CreateGameObjectWithHideFlags("PreRenderLight", HideFlags.HideAndDontSave, typeof(Light));
            var light = lightGO.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.enabled = false;
            return lightGO;
        }

        public LegacySceneResources()
        {

            name = "Legacy";

            try
            {
                cullingMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
            }
            catch
            {
                Debug.LogError("Error while trying to fetch an available culling mask. Please check the number of active previews");
            }
            previewScene = EditorSceneManager.NewPreviewScene();
            cullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);


            var cameraGameObject = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
            
            SceneManager.MoveGameObjectToScene(cameraGameObject, previewScene);
            camera = cameraGameObject.GetComponent<Camera>();
            EditorUtility.SetCameraAnimateMaterials(camera, true);

            camera.cameraType = CameraType.Preview;
            camera.enabled = true;
            camera.fieldOfView = 15;
            camera.farClipPlane = 10.0f;
            camera.nearClipPlane = 2.0f;
            camera.backgroundColor = new Color(49.0f / 255.0f, 49.0f / 255.0f, 49.0f / 255.0f, 1.0f);
            camera.clearFlags = CameraClearFlags.Color;

            // Explicitly use forward rendering for all previews
            // (deferred fails when generating some static previews at editor launch; and we never want
            // vertex lit previews if that is chosen in the player settings)
            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.scene = previewScene;
            camera.transform.position = -Vector3.forward * 5f;
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = false;



            var light0GameObject = CreateLight();
            var light1GameObject = CreateLight();

            SceneManager.MoveGameObjectToScene(light0GameObject, previewScene);
            SceneManager.MoveGameObjectToScene(light1GameObject, previewScene);

            light0 = light0GameObject.GetComponent<Light>();
            light1 = light1GameObject.GetComponent<Light>();

            light0.transform.rotation = Quaternion.Euler(50f, 50f, 0f);
            light0.color = new Color(0.769f, 0.769f, 0.769f, 1); // SceneView.kSceneViewFrontLight
            light1.transform.rotation = Quaternion.Euler(340, 218, 177);
            light1.color = new Color(.4f, .4f, .45f, 0f) * .7f;

            light0.enabled = true;
            light1.enabled = true;



            checkerboardMaterial = new Material(Shader.Find("Hidden/Checkerboard"));
            blitNoAlphaMaterial = new Material(Shader.Find("Hidden/BlitNoAlpha"));
            checkerboardMaterial.hideFlags = HideFlags.HideAndDontSave;
            blitNoAlphaMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (s_Meshes[0] == null)
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");

                // @TODO: temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    var meshFilter = t.GetComponent<MeshFilter>();
                    switch (t.name)
                    {
                        case "sphere":
                            s_Meshes[0] = meshFilter.sharedMesh;
                            sphere = s_Meshes[0];
                            break;
                        case "cube":
                            s_Meshes[1] = meshFilter.sharedMesh;
                            break;
                        case "cylinder":
                            s_Meshes[2] = meshFilter.sharedMesh;
                            break;
                        case "torus":
                            s_Meshes[3] = meshFilter.sharedMesh;
                            break;
                        default:
                            Debug.LogWarning("Something is wrong, weird object found: " + t.name);
                            break;
                    }
                }

                s_MeshIcons[0] = EditorGUIUtility.IconContent("PreMatSphere");
                s_MeshIcons[1] = EditorGUIUtility.IconContent("PreMatCube");
                s_MeshIcons[2] = EditorGUIUtility.IconContent("PreMatCylinder");
                s_MeshIcons[3] = EditorGUIUtility.IconContent("PreMatTorus");
                s_MeshIcons[4] = EditorGUIUtility.IconContent("PreMatQuad");

                s_LightIcons[0] = EditorGUIUtility.IconContent("PreMatLight0");
                s_LightIcons[1] = EditorGUIUtility.IconContent("PreMatLight1");

                s_TimeIcons[0] = EditorGUIUtility.IconContent("PlayButton");
                s_TimeIcons[1] = EditorGUIUtility.IconContent("PauseButton");

                Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
                s_Meshes[4] = quadMesh;
            }
            quad = s_Meshes[4];
            sphere = s_Meshes[0];
        }


        protected override void setupRenderSettings() {}
        public override void Dispose()
        {
            if (light0 != null)
            {
                UnityEngine.Object.DestroyImmediate(light0.gameObject);
                light0 = null;
            }

            if (light1 != null)
            {
                UnityEngine.Object.DestroyImmediate(light1.gameObject);
                light1 = null;
            }

            if (camera != null)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                camera = null;
            }

            if (checkerboardMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(checkerboardMaterial, true);
                checkerboardMaterial = null;
            }
            if (blitNoAlphaMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(blitNoAlphaMaterial, true);
                blitNoAlphaMaterial = null;
            }

            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        public override void addMeshForShadows(Mesh mesh, Matrix4x4 transform)
        {
            if (shadowMesh == null)
            {
                shadowMesh = new GameObject();
                shadowMesh.AddComponent<MeshFilter>();
                shadowMesh.AddComponent<MeshRenderer>();
                Material basicMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shadowMesh.GetComponent<MeshRenderer>().material = basicMat;
                shadowMesh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                shadowMesh.GetComponent<MeshRenderer>().receiveShadows = true;
                SceneManager.MoveGameObjectToScene(shadowMesh, previewScene);

            }
            if (shadowMesh.GetComponent<MeshFilter>().sharedMesh != mesh) shadowMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadowMesh.transform.rotation = transform.rotation;
            shadowMesh.transform.localScale = transform.lossyScale;
            shadowMesh.transform.position = transform.GetColumn(3);
        }

        public override void updateScene() { }
    }



    class DarkSceneResources : AbstractPreviewSceneResources, IDisposable
    {
        
        Camera previewCamera;
        GameObject centerOfRotation;

        Material errorMaterial;
        Material m_BlitNoAlphaMaterial;

        static readonly Mesh[] s_Meshes = { null, null, null, null, null };
        static readonly GUIContent[] s_MeshIcons = { null, null, null, null, null };
        static readonly GUIContent[] s_LightIcons = { null, null };
        static readonly GUIContent[] s_TimeIcons = { null, null };


        // SIMPLIFICATIONS
        #region Simplifications


        #endregion


        static GameObject CreateLight(LightType type)
        {
            GameObject lightGO = EditorUtility.CreateGameObjectWithHideFlags("PreRenderLight", HideFlags.HideAndDontSave, typeof(Light));
            var light = lightGO.GetComponent<Light>();
            light.color = defaultUnityLightColor;
            light.type = type;
           
            light.shadows = LightShadows.Soft;
            light.intensity = 5f;
            light.range = 10f;
            light.enabled = false;
            return lightGO;
        }

        public DarkSceneResources()
        {
            /*
                * Basic Setup FOr all Scene Rseources
                */
            name = "Dark With Moving Lights";


            try
            {
                cullingMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
            }
            catch
            {
                Debug.LogError("Error while trying to fetch an available culling mask. Please check the number of active previews");
            }
            previewScene = EditorSceneManager.NewPreviewScene();
            cullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
            var cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            previewCamera = cameraObject.GetComponent<Camera>();


            errorMaterial = new Material(Shader.Find("Hidden/Checkerboard"));
            errorMaterial.hideFlags = HideFlags.HideAndDontSave;
            m_BlitNoAlphaMaterial = new Material(Shader.Find("Hidden/BlitNoAlpha"));
            m_BlitNoAlphaMaterial.hideFlags = HideFlags.HideAndDontSave;
            checkerboardMaterial = errorMaterial;
            blitNoAlphaMaterial = m_BlitNoAlphaMaterial;


            // Adding the Camera
            EditorUtility.SetCameraAnimateMaterials(previewCamera, true);

            previewCamera.cameraType = CameraType.Preview;
            camera = previewCamera;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = 19;
            camera.farClipPlane = 40.001f;
            camera.nearClipPlane = 0.1f;
            camera.backgroundColor = new Color(10.0f / 255.0f, 100.0f / 255.0f, 100.0f / 255.0f, 1.0f);

            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.orthographic = false;
            camera.scene = previewScene;
            camera.clearFlags = CameraClearFlags.Skybox;

            Vector3 sharedOffset = new Vector3(0f, 0f, 2.75f);
            camera.transform.position = new Vector3(0f, 0f, -5f) - sharedOffset;

            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");

            Material skyboxMaterial = Resources.Load<Material>("BlackSkybox");
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
            else
            {
                Debug.LogError("Material is null");
            }

            Material pavementMaterial = new Material(standardShader);
            pavementMaterial.color = Color.white;

            var pavement = GameObject.CreatePrimitive(PrimitiveType.Plane);

            pavement.GetComponent<MeshRenderer>().material = pavementMaterial;
            SceneManager.MoveGameObjectToScene(pavement, previewScene);
            pavement.transform.SetPositionAndRotation(new Vector3(0f, -0.85f, 0f), Quaternion.identity);
            pavement.GetComponent<MeshRenderer>().receiveShadows = true;
            pavement.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            

            var light0Object = CreateLight(LightType.Directional);
            var light1Object = CreateLight(LightType.Spot);

            centerOfRotation = new GameObject();
            centerOfRotation.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);


            SceneManager.MoveGameObjectToScene(light0Object, previewScene);
            SceneManager.MoveGameObjectToScene(light1Object, previewScene);
            SceneManager.MoveGameObjectToScene(centerOfRotation, previewScene);

            light0 = light0Object.GetComponent<Light>();
            light1 = light1Object.GetComponent<Light>();

            light0.intensity = 0.05f;
            light0Object.transform.parent = centerOfRotation.transform;
            light0Object.transform.SetPositionAndRotation(new Vector3(0f, 0f, 3f), Quaternion.AngleAxis(180f,Vector3.up));


            light1Object.transform.parent = centerOfRotation.transform;
            light1Object.transform.SetPositionAndRotation(new Vector3(0f, 0f, -3f), Quaternion.identity);
            light1.innerSpotAngle = 0f;
            light1.spotAngle = 55f;

            light0.enabled = true;
            light1.enabled = true;

            // Generating preview meshes
            if (s_Meshes[0] == null)
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");

                // @TODO: temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    var meshFilter = t.GetComponent<MeshFilter>();
                    switch (t.name)
                    {
                        case "sphere":
                            s_Meshes[0] = meshFilter.sharedMesh;
                            break;
                        case "cube":
                            s_Meshes[1] = meshFilter.sharedMesh;
                            break;
                        case "cylinder":
                            s_Meshes[2] = meshFilter.sharedMesh;
                            break;
                        case "torus":
                            s_Meshes[3] = meshFilter.sharedMesh;
                            break;
                        default:
                            Debug.LogWarning("Something is wrong, weird object found: " + t.name);
                            break;
                    }
                }

                s_MeshIcons[0] = EditorGUIUtility.IconContent("PreMatSphere");
                s_MeshIcons[1] = EditorGUIUtility.IconContent("PreMatCube");
                s_MeshIcons[2] = EditorGUIUtility.IconContent("PreMatCylinder");
                s_MeshIcons[3] = EditorGUIUtility.IconContent("PreMatTorus");
                s_MeshIcons[4] = EditorGUIUtility.IconContent("PreMatQuad");

                s_LightIcons[0] = EditorGUIUtility.IconContent("PreMatLight0");
                s_LightIcons[1] = EditorGUIUtility.IconContent("PreMatLight1");

                s_TimeIcons[0] = EditorGUIUtility.IconContent("PlayButton");
                s_TimeIcons[1] = EditorGUIUtility.IconContent("PauseButton");

                Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
                s_Meshes[4] = quadMesh;

            }
            sphere = s_Meshes[0];
            quad = s_Meshes[4];

        }

        public override void Dispose()
        {

            if (light0 != null)
            {
                UnityEngine.Object.DestroyImmediate(light0.gameObject);
                light0 = null;
            }

            if (light1 != null)
            {
                UnityEngine.Object.DestroyImmediate(light1.gameObject);
                light1 = null;
            }

            if (camera != null)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                previewCamera = null;
            }

            if (errorMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(errorMaterial, true);
                errorMaterial = null;
            }
            if (blitNoAlphaMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(blitNoAlphaMaterial, true);
                m_BlitNoAlphaMaterial = null;
            }

        }



        protected override void setupRenderSettings() {}

        public override void addMeshForShadows(Mesh mesh, Matrix4x4 transform)
        {
            if (shadowMesh == null)
            {
                shadowMesh = new GameObject();
                shadowMesh.AddComponent<MeshFilter>();
                shadowMesh.AddComponent<MeshRenderer>();
                Material basicMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shadowMesh.GetComponent<MeshRenderer>().material = basicMat;
                shadowMesh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                shadowMesh.GetComponent<MeshRenderer>().receiveShadows = true;
                SceneManager.MoveGameObjectToScene(shadowMesh, previewScene);

            }
            if (shadowMesh.GetComponent<MeshFilter>().sharedMesh != mesh) shadowMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadowMesh.transform.rotation = transform.rotation;
            shadowMesh.transform.localScale = transform.lossyScale;
            shadowMesh.transform.position = transform.GetColumn(3);
        }

        public override void updateScene() {

            centerOfRotation.transform.Rotate(Vector3.up, Time.deltaTime * 20f);

        }
    }


}