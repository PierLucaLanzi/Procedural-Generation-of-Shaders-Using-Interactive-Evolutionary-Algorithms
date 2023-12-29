using System;
using UnityEditor.ShaderGraph;
using UnityEditor.Rendering.Universal.ShaderGraph;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    static class CreateUnlitShaderGraph
    {
        [MenuItem("Assets/Create/Shader/Universal Render Pipeline/Unlit Shader Graph", false, 300)]
        public static void CreateUnlitGraph()
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(UniversalUnlitSubTarget));

            var blockDescriptors = new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
            };

            GraphUtil.CreateNewGraphWithOutputs(new[] { target }, blockDescriptors);
        }
        public static void CreateUnlitGraph(string path)
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(UniversalUnlitSubTarget));

            var blockDescriptors = new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor,
            };

            GraphUtil.CreateNewGraphWithOutputs(new[] { target }, blockDescriptors,path);
        }
    }
}

namespace AutoGen
{
    public static class UnlitGraphGenerator
    {
        public static void Generate(string path)
        {
            CreateUnlitShaderGraph.CreateUnlitGraph(path);
        }
        public static void Generate(string path, int ID)
        {
            CreateUnlitShaderGraph.CreateUnlitGraph(string.Format("{0}/graph_{1}", path, ID));
        }
        public static void Generate(string path, int ID,string baseName)
        {
            CreateUnlitShaderGraph.CreateUnlitGraph(string.Format("{0}/{2}_{1}", path, ID,baseName));
        }
    }

}