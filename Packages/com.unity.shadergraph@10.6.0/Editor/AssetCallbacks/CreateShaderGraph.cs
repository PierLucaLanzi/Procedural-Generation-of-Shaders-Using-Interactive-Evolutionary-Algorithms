using UnityEngine;
using UnityEditor.ShaderGraph;
using System.IO;
using AutoGen; 

namespace UnityEditor.ShaderGraph
{
    static class CreateShaderGraph
    {
        [MenuItem("Assets/Create/Shader/Blank Shader Graph", false, 208)]
        public static void CreateBlankShaderGraph()
        {
            GraphUtil.CreateNewGraph();
        }
        public static void CreateBlankShaderGraph(string path)
        {
            GraphUtil.CreateNewGraph(path);
        }
    }
}
namespace AutoGen
{
    

    public static class BlankGraphGenerator
    {
        public static void Generate(string path)
        {
            CreateShaderGraph.CreateBlankShaderGraph(path);
        }
        public static void Generate(string path, int ID)
        {
            CreateShaderGraph.CreateBlankShaderGraph(string.Format("{0}/graph_{1}", path, ID));
        }
        public static void Generate(string path, int ID, string baseName)
        {
            CreateShaderGraph.CreateBlankShaderGraph(string.Format("{0}/{1}_{2}", path,baseName, ID));
        }
    }

    public static class GraphInfoReader
    {
        public static void openGraphJson(string path)
        {
            GraphUtil.OpenFile(path);
        }
    }
}