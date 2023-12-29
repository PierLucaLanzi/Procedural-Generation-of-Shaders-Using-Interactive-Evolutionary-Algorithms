using System;
using UnityEditor.ShaderGraph;
using UnityEditor.Rendering.Universal.ShaderGraph;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    static class CreateLitShaderGraph
    {
        [MenuItem("Assets/Create/Shader/Universal Render Pipeline/Lit Shader Graph", false, 300)]
        public static void CreateLitGraph()
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(UniversalLitSubTarget));

            var blockDescriptors = new [] 
            { 
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS, 
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Smoothness, 
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Occlusion,
            };

            GraphUtil.CreateNewGraphWithOutputs(new [] {target}, blockDescriptors);
        }

        public static void CreateLitGraph(string path)
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(UniversalLitSubTarget));

            var blockDescriptors = new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Occlusion,
            };

            GraphUtil.CreateNewGraphWithOutputs(new[] { target }, blockDescriptors,path);
        }
    }
}

namespace AutoGen
{
    public static class LitGraphGenerator
    {
        public static void Generate(string path)
        {
            CreateLitShaderGraph.CreateLitGraph(path);
        }
        public static void Generate(string path, int ID)
        {
            CreateLitShaderGraph.CreateLitGraph(string.Format("{0}/graph_{1}", path, ID));
        }
        public static void Generate(string path, int ID,string baseName)
        {
            CreateLitShaderGraph.CreateLitGraph(string.Format("{0}/{2}_{1}", path, ID,baseName));
        }
    }

}