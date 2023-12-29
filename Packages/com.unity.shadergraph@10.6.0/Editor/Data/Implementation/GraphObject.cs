using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Inspector;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

using UnityEditor.ShaderGraph.Drawing.Controls;
using System.IO;
using ColorMode = UnityEditor.ShaderGraph.Internal.ColorMode;

namespace UnityEditor.Graphing
{
    class GraphObject : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        SerializationHelper.JSONSerializedElement m_SerializedGraph;

        [SerializeField]
        int m_SerializedVersion;

        [SerializeField]
        bool m_IsDirty;

        [SerializeField]
        bool m_IsSubGraph;

        [SerializeField]
        string m_AssetGuid;

        internal string AssetGuid
        {
            get => m_AssetGuid;
        }

        [NonSerialized]
        GraphData m_Graph;

        [NonSerialized]
        int m_DeserializedVersion;

        public GraphData graph
        {
            get { return m_Graph; }
            set
            {
                if (m_Graph != null)
                    m_Graph.owner = null;
                m_Graph = value;
                if (m_Graph != null)
                    m_Graph.owner = this;
            }
        }

        // this value stores whether an undo operation has been registered (which indicates a change has been made to the graph)
        // and is used to trigger the MaterialGraphEditWindow to update it's title
        public bool isDirty
        {
            get { return m_IsDirty; }
            set { m_IsDirty = value; }
        }

        public virtual void RegisterCompleteObjectUndo(string actionName)
        {
            Undo.RegisterCompleteObjectUndo(this, actionName);
            m_SerializedVersion++;
            m_DeserializedVersion++;
            m_IsDirty = true;
        }

        public void OnBeforeSerialize()
        {
            if (graph != null)
            {
                var json = MultiJson.Serialize(graph);
                m_SerializedGraph = new SerializationHelper.JSONSerializedElement { JSONnodeData = json };
                m_IsSubGraph = graph.isSubGraph;
                m_AssetGuid = graph.assetGuid;
            }
        }

        public void OnAfterDeserialize()
        {
        }

        public bool wasUndoRedoPerformed => m_DeserializedVersion != m_SerializedVersion;

        public void HandleUndoRedo()
        {
            Debug.Assert(wasUndoRedoPerformed);
            var deserializedGraph = DeserializeGraph();
            m_Graph.ReplaceWith(deserializedGraph);
        }

        GraphData DeserializeGraph()
        {
            var json = m_SerializedGraph.JSONnodeData;
            var deserializedGraph = new GraphData {isSubGraph = m_IsSubGraph, assetGuid = m_AssetGuid};
            MultiJson.Deserialize(deserializedGraph, json);
            m_DeserializedVersion = m_SerializedVersion;
            m_SerializedGraph = default;
            return deserializedGraph;
        }

        public void Validate()
        {
            if (graph != null)
            {
                graph.OnEnable();
                graph.ValidateGraph();
            }
        }

        void OnEnable()
        {
            if (graph == null && m_SerializedGraph.JSONnodeData != null)
            {
                graph = DeserializeGraph();
            }
            Validate();
        }

        void OnDestroy()
        {
            graph?.OnDisable();
        }
    }
}

namespace AutoGen
{
    internal static class DebugFunctions
    {
        public static void debugPrintBlockNodes(GraphData graph)
        {
            foreach (var node in graph.GetNodes<BlockNode>())
            {
                Debug.Log(node.name);
            }
        }
    }
    public enum MutationStrength
    {
        Low,// Only modifies input values
        Medium, // Modifies input values + changes Metallic, Smoothness, Alpha and AO
        High, // Modifies input values and can change inputs to base color, emission, normal and vertex position
    }

    public enum RangeSize
    {
        One = -1, // This range only allows the value to go from 0 to 1
        Tiny = 0, // -2 to 2
        Small = 1, // Small Range, good for values that need to stay between -5 and +5
        Medium = 2, // -100 to +100
        Large  =3    // -1000 +1000
    }
    internal static class RangeSizeValues
    {
        internal static readonly int tinyRangeSize = 2;
        internal static readonly int smallRangeSize = 5;
        internal static readonly int mediumRangeSize = 100;
        internal static readonly int largeRangeSize = 1000;

        internal static int getRange(RangeSize size)
        {
            switch (size)
            {
                case RangeSize.One:
                    return 1;
                case RangeSize.Tiny:
                    return tinyRangeSize;
                case RangeSize.Small:
                    return smallRangeSize;
                case RangeSize.Medium:
                    return mediumRangeSize;
                case RangeSize.Large:
                    return largeRangeSize;
                default:
                    return tinyRangeSize;
            }
        }
    }

    public class GeneticHandler
    {


        private System.Random rand;
        public MutationStrength mutationStrength;
        public GraphType graphType;

        private float minFloat;
        private float maxFloat;
        private float boolChance;
        private Vector3 minGradientCol; // Collection of 3 parameters
        private Vector3 maxGradientCol; // Collection of 3 parameters
        private Vector4 minVec; // Collection of 4 parameters
        private Vector4 maxVec; // Collection of 4 parameters
        private Matrix4x4 minMatrix; // Collection of 16 parameters
        private Matrix4x4 maxMatrix; // Collection of 16 parameters

        internal Dictionary<AbstractMaterialNode, Tuple<float, float>> inputFloatRanges;

        public bool changeGraphType = false;
        public bool changeTypeAllowed= false;
        public bool expandAllowed = false;
        public bool expandGraph = false;
        public float expansionProbability = 0f;
        public float typeChangeProbability = 0f;

        private int generation = 0;


        private int numberOfParameters = 49;



        public GeneticHandler(bool allowExpansion,bool allowTypeChange, float expandProbability, float changeProbability)
        {
            rand = new System.Random();
            maxFloat = randFloat(rand);
            minFloat = randFloat(rand, maxFloat, true);
            boolChance = (float)rand.NextDouble();
            maxGradientCol = new Vector3(randFloat(rand) % 255f, randFloat(rand) % 255f, randFloat(rand) % 255f);
            minGradientCol = new Vector3(randFloat(rand, maxGradientCol.x, true) % 255f, randFloat(rand, maxGradientCol.y, true) % 255f, randFloat(rand, maxGradientCol.z, true) % 255f);
            maxVec = new Vector4(randFloat(rand), randFloat(rand), randFloat(rand), randFloat(rand));
            minVec = new Vector4(randFloat(rand, maxVec.x, true), randFloat(rand, maxVec.y, true), randFloat(rand, maxVec.z, true), randFloat(rand, maxVec.w, true));

            Vector4 col0 = new Vector4(randFloat(rand), randFloat(rand), randFloat(rand), randFloat(rand));
            Vector4 col1 = new Vector4(randFloat(rand), randFloat(rand), randFloat(rand), randFloat(rand));
            Vector4 col2 = new Vector4(randFloat(rand), randFloat(rand), randFloat(rand), randFloat(rand));
            Vector4 col3 = new Vector4(randFloat(rand), randFloat(rand), randFloat(rand), randFloat(rand));

            maxMatrix = new Matrix4x4(col0, col1, col2, col3);

            col0 = new Vector4(randFloat(rand, col0.x, true), randFloat(rand, col0.y, true), randFloat(rand, col0.z, true), randFloat(rand, col0.w, true));
            col1 = new Vector4(randFloat(rand, col1.x, true), randFloat(rand, col1.y, true), randFloat(rand, col1.z, true), randFloat(rand, col1.w, true));
            col2 = new Vector4(randFloat(rand, col2.x, true), randFloat(rand, col2.y, true), randFloat(rand, col2.z, true), randFloat(rand, col2.w, true));
            col3 = new Vector4(randFloat(rand, col3.x, true), randFloat(rand, col3.y, true), randFloat(rand, col3.z, true), randFloat(rand, col3.w, true));

            minMatrix = new Matrix4x4(col0, col1, col2, col3);

            float chosenStrength = (float)rand.NextDouble();

            if (chosenStrength > 0.66) mutationStrength = MutationStrength.High;
            else if (chosenStrength > 0.33) mutationStrength = MutationStrength.Medium;
            else mutationStrength = MutationStrength.Low;

            expansionProbability = expandProbability;
            expandAllowed = allowExpansion;
            if (expandAllowed)
            {
                expandGraph = (float)rand.NextDouble() > expansionProbability ;
            }

            typeChangeProbability = changeProbability;
            changeTypeAllowed = allowTypeChange;
            if (changeTypeAllowed)
            {
                changeGraphType = (float)rand.NextDouble() > typeChangeProbability;
            }

            inputFloatRanges = new Dictionary<AbstractMaterialNode, Tuple<float, float>>();
        }

        public GeneticHandler(GeneticHandler parent)
        {
            rand = new System.Random();
            // When only one parent, the ranges can move by at most 100% of the range up or down
            float floatRange = Mathf.Abs(maxFloat - minFloat);
            minFloat = parent.minFloat + (rand.NextDouble() > 0.5 ? 1 : -1) * (float)rand.NextDouble() * floatRange;
            maxFloat = parent.maxFloat + (rand.NextDouble() > 0.5 ? 1 : -1) * (float)rand.NextDouble() * floatRange;
            swapMinMaxFloat();

            boolChance = parent.boolChance + (float)((rand.NextDouble() > 0.5 ? 1 : -1) * rand.NextDouble());
            boolChance = Mathf.Clamp01(boolChance);

            minGradientCol.x = moveFrom(parent.minGradientCol.x, parent.maxGradientCol.x - parent.minGradientCol.x) % 255f;
            minGradientCol.y = moveFrom(parent.minGradientCol.y, parent.maxGradientCol.y - parent.minGradientCol.y) % 255f;
            minGradientCol.z = moveFrom(parent.minGradientCol.z, parent.maxGradientCol.z - parent.minGradientCol.z) % 255f;

            maxGradientCol.x = moveFrom(parent.maxGradientCol.x, parent.maxGradientCol.x - parent.minGradientCol.x) % 255f;
            maxGradientCol.y = moveFrom(parent.maxGradientCol.y, parent.maxGradientCol.y - parent.minGradientCol.y) % 255f;
            maxGradientCol.z = moveFrom(parent.maxGradientCol.z, parent.maxGradientCol.z - parent.minGradientCol.z) % 255f;
            swapMinMaxGrad();

            minVec.x = moveFrom(parent.minVec.x, parent.maxVec.x - parent.minVec.x);
            minVec.y = moveFrom(parent.minVec.y, parent.maxVec.y - parent.minVec.y);
            minVec.z = moveFrom(parent.minVec.z, parent.maxVec.z - parent.minVec.z);
            minVec.w = moveFrom(parent.minVec.w, parent.maxVec.w - parent.minVec.w);

            maxVec.x = moveFrom(parent.maxVec.x, parent.maxVec.x - parent.minVec.x);
            maxVec.y = moveFrom(parent.maxVec.y, parent.maxVec.y - parent.minVec.y);
            maxVec.z = moveFrom(parent.maxVec.z, parent.maxVec.z - parent.minVec.z);
            maxVec.w = moveFrom(parent.maxVec.w, parent.maxVec.w - parent.minVec.w);
            swapMinMaxVec();

            List<Vector4> pminColumns = new List<Vector4>() { parent.minMatrix.GetColumn(0), parent.minMatrix.GetColumn(1), parent.minMatrix.GetColumn(2), parent.minMatrix.GetColumn(3) };
            List<Vector4> pmaxColumns = new List<Vector4>() { parent.maxMatrix.GetColumn(0), parent.maxMatrix.GetColumn(1), parent.maxMatrix.GetColumn(2), parent.maxMatrix.GetColumn(3) };
            List<Vector4> minColumns = new List<Vector4>(4);
            List<Vector4> maxColumns = new List<Vector4>(4);
            for (int i = 0; i < 4; i++)
            {
                Vector4 col = new Vector4();
                col.x = moveFrom(pminColumns[i].x, pmaxColumns[i].x - pminColumns[i].x);
                col.y = moveFrom(pminColumns[i].y, pmaxColumns[i].y - pminColumns[i].y);
                col.z = moveFrom(pminColumns[i].z, pmaxColumns[i].z - pminColumns[i].z);
                col.w = moveFrom(pminColumns[i].w, pmaxColumns[i].w - pminColumns[i].w);
                minColumns.Add(col);
                col = new Vector4();
                col.x = moveFrom(pmaxColumns[i].x, pmaxColumns[i].x - pminColumns[i].x);
                col.y = moveFrom(pmaxColumns[i].y, pmaxColumns[i].y - pminColumns[i].y);
                col.z = moveFrom(pmaxColumns[i].z, pmaxColumns[i].z - pminColumns[i].z);
                col.w = moveFrom(pmaxColumns[i].w, pmaxColumns[i].w - pminColumns[i].w);
                maxColumns.Add(col);
            }
            minMatrix = new Matrix4x4(minColumns[0], minColumns[1], minColumns[2], minColumns[3]);
            maxMatrix = new Matrix4x4(maxColumns[0], maxColumns[1], maxColumns[2], maxColumns[3]);
            swapMinMaxMat();

            expansionProbability = parent.expansionProbability;
            typeChangeProbability = parent.typeChangeProbability;
            expandAllowed = parent.expandAllowed;
            changeTypeAllowed = parent.changeTypeAllowed;

            expandGraph = expandAllowed && (float)rand.NextDouble() > expansionProbability ;
            changeGraphType = changeTypeAllowed && (float)rand.NextDouble() > typeChangeProbability ;

            mutationStrength = parent.mutationStrength;

            if (changeGraphType) {
                switch (parent.graphType)
                {
                    case GraphType.Lit:
                        graphType = GraphType.Unlit;
                        break;
                    case GraphType.Unlit:
                        graphType = GraphType.Lit;
                        break;
                    default:
                        throw new ArgumentException("Cannot inherit types different from Lit and Unlit.");
                }
            }
            else
            {
                graphType = parent.graphType;
            }

            generation = parent.generation + 1;

            inputFloatRanges = new Dictionary<AbstractMaterialNode, Tuple<float, float>>();
        }

        /*
         * Right now inheriting genes from 2 parents performs uniform crossover among the two genes
         */
        public GeneticHandler(GeneticHandler p1, GeneticHandler p2)
        {
            rand = new System.Random();
            minFloat = (rand.NextDouble() > 0.5) ? p1.minFloat : p2.minFloat;
            maxFloat = (rand.NextDouble() > 0.5) ? p1.maxFloat : p2.maxFloat;
            swapMinMaxFloat();

            boolChance = (rand.NextDouble() > 0.5) ? p1.boolChance : p2.boolChance;

            minGradientCol = (rand.NextDouble() > 0.5) ? p1.minGradientCol : p2.minGradientCol;
            maxGradientCol = (rand.NextDouble() > 0.5) ? p1.maxGradientCol : p2.maxGradientCol;
            swapMinMaxGrad();

            maxVec = (rand.NextDouble() > 0.5) ? p1.maxVec : p2.maxVec;
            minVec = (rand.NextDouble() > 0.5) ? p1.minVec : p2.minVec;
            swapMinMaxVec();

            minMatrix = (rand.NextDouble() > 0.5) ? p1.minMatrix : p2.minMatrix;
            maxMatrix = (rand.NextDouble() > 0.5) ? p1.maxMatrix : p2.maxMatrix;
            swapMinMaxMat();

            mutationStrength = (rand.NextDouble() > 0.5) ? p1.mutationStrength : p2.mutationStrength;

            float choice = UnityEngine.Random.Range(0f, 1f);
            graphType = (choice > 0.5f) ? p1.graphType : p2.graphType;
            Debug.LogFormat("Between {0} and {1} i chose {2} with {3}", p1.graphType, p2.graphType, graphType,choice);

            expansionProbability = rand.NextDouble() >0.5? p1.expansionProbability : p2.expansionProbability;
            typeChangeProbability = rand.NextDouble() > 0.5 ? p1.typeChangeProbability:p2.typeChangeProbability;
            expandAllowed = p1.expandAllowed && p2.expandAllowed;
            changeTypeAllowed = p1.changeTypeAllowed && p2.changeTypeAllowed;
            

            expandGraph = expandAllowed && (float)rand.NextDouble() > expansionProbability;
            changeGraphType = changeTypeAllowed && (float)rand.NextDouble() > typeChangeProbability;
            if (changeGraphType) {
                Debug.Log("Triggering graph type change");
                graphType = graphType == GraphType.Unlit ? GraphType.Lit : GraphType.Unlit;
                }
            generation = Mathf.Max(p1.generation, p2.generation) + 1;

            inputFloatRanges = new Dictionary<AbstractMaterialNode, Tuple<float, float>>();
        }

        internal void inheritRanges(Dictionary<AbstractMaterialNode, AbstractMaterialNode> parentToChild, GeneticHandler parentGenes)
        {
            foreach (AbstractMaterialNode node in parentGenes.inputFloatRanges.Keys)
            {
                Tuple<float, float> parentFloatRange = parentGenes.inputFloatRanges[node];
                Tuple<float, float> floatRange = new Tuple<float, float>(parentFloatRange.Item1, parentFloatRange.Item2);
                try
                {
                    AbstractMaterialNode childNode = parentToChild[node];
                    inputFloatRanges.Add(childNode, floatRange);
                }
                catch(KeyNotFoundException e)
                {
                    GraphData childGraph = null ;
                    foreach (var anyChildNode in parentToChild.Values)
                    {
                        childGraph = anyChildNode.owner;
                        break;
                    }
                    Debug.LogErrorFormat("Child {0} was not found when inheriting from {1} to {2}", node.name, AssetDatabase.GUIDToAssetPath(node.owner.assetGuid), AssetDatabase.GUIDToAssetPath(childGraph?.assetGuid));
                }
            }
        }

        #region Random Generation Functions
        private float moveInRange(float value, float min, float max)
        {
            float range = Mathf.Abs(max - min);
            float result = value + (rand.NextDouble() > 0.5 ? -1 : 1) * range * (float)rand.NextDouble();
            return Mathf.Clamp(result, min, max);
        }

        private float moveIn01(float value)
        {
            return Mathf.Clamp01(value + (float)rand.NextDouble() * (rand.NextDouble() > 0.5 ? 1 : -1));
        }

        private float moveFrom(float value, float range)
        {
            return value + (rand.NextDouble() > 0.5 ? 1 : -1) * (float)rand.NextDouble() * range;
        }

        public bool getBool()
        {
            return rand.NextDouble() > boolChance;
        }

        public float getFloat01()
        {
            return (float)rand.NextDouble();
        }

        public Color getColor()
        {
            float x = Mathf.Abs(randFloat(rand, maxGradientCol.x, minGradientCol.x)% 255f / 255f);
            float y = Mathf.Abs(randFloat(rand, maxGradientCol.y, minGradientCol.y)% 255f / 255f);
            float z = Mathf.Abs(randFloat(rand, maxGradientCol.z, minGradientCol.z)% 255f / 255f);
            return new Color(x, y, z,0f);
        }

        public Matrix4x4 getMat2()
        {
            Vector4 col0 = new Vector4(randFloat(rand, maxMatrix.m00, minMatrix.m00), randFloat(rand, maxMatrix.m10, minMatrix.m10));
            Vector4 col1 = new Vector4(randFloat(rand, maxMatrix.m01, minMatrix.m01), randFloat(rand, maxMatrix.m11, minMatrix.m11));
            return new Matrix4x4(col0, col1, Vector4.zero, Vector4.zero);
        }
        public Matrix4x4 getMat3()
        {
            Vector4 col0 = new Vector4(randFloat(rand, maxMatrix.m00, minMatrix.m00), randFloat(rand, maxMatrix.m10, minMatrix.m10), randFloat(rand, maxMatrix.m20, minMatrix.m20));
            Vector4 col1 = new Vector4(randFloat(rand, maxMatrix.m01, minMatrix.m01), randFloat(rand, maxMatrix.m11, minMatrix.m11), randFloat(rand, maxMatrix.m21, minMatrix.m21));
            Vector4 col2 = new Vector4(randFloat(rand, maxMatrix.m02, minMatrix.m02), randFloat(rand, maxMatrix.m12, minMatrix.m12), randFloat(rand, maxMatrix.m22, minMatrix.m22));
            return new Matrix4x4(col0, col1, col2, Vector4.zero);
        }
        public Matrix4x4 getMat4()
        {
            Vector4 col0 = new Vector4(randFloat(rand, maxMatrix.m00, minMatrix.m00), randFloat(rand, maxMatrix.m10, minMatrix.m10), randFloat(rand, maxMatrix.m20, minMatrix.m20), randFloat(rand, maxMatrix.m30, minMatrix.m30));
            Vector4 col1 = new Vector4(randFloat(rand, maxMatrix.m01, minMatrix.m01), randFloat(rand, maxMatrix.m11, minMatrix.m11), randFloat(rand, maxMatrix.m21, minMatrix.m21), randFloat(rand, maxMatrix.m31, minMatrix.m31));
            Vector4 col2 = new Vector4(randFloat(rand, maxMatrix.m02, minMatrix.m02), randFloat(rand, maxMatrix.m12, minMatrix.m12), randFloat(rand, maxMatrix.m22, minMatrix.m22), randFloat(rand, maxMatrix.m32, minMatrix.m32));
            Vector4 col3 = new Vector4(randFloat(rand, maxMatrix.m03, minMatrix.m03), randFloat(rand, maxMatrix.m13, minMatrix.m13), randFloat(rand, maxMatrix.m23, minMatrix.m23), randFloat(rand, maxMatrix.m33, minMatrix.m33));
            return new Matrix4x4(col0, col1, col2, col3);
        }

        public float getFloat()
        {
            return randFloat(rand, minFloat, maxFloat);
        }

        public Vector2 getVec2()
        {
            float x = randFloat(rand, maxVec.x, minVec.x);
            float y = randFloat(rand, maxVec.y, minVec.y);
            return new Vector2(x, y);
        }

        public Vector3 getVec3()
        {
            float x = randFloat(rand, maxVec.x, minVec.x);
            float y = randFloat(rand, maxVec.y, minVec.y);
            float z = randFloat(rand, maxVec.z, minVec.z);
            return new Vector3(x, y, z);
        }

        public Vector4 getVec4()
        {
            float x = randFloat(rand, maxVec.x, minVec.x);
            float y = randFloat(rand, maxVec.y, minVec.y);
            float z = randFloat(rand, maxVec.z, minVec.z);
            float w = randFloat(rand, maxVec.w, minVec.w);
            return new Vector4(x, y, z, w);
        }

        private void swapMinMaxFloat()
        {
            if (minFloat <= maxFloat) return;
            float temp = minFloat;
            minFloat = maxFloat;
            maxFloat = minFloat;
        }

        private void swapMinMaxGrad()
        {
            float temp;
            if (minGradientCol.x > maxGradientCol.x)
            {
                temp = minGradientCol.x;
                minGradientCol.x = maxGradientCol.x;
                maxGradientCol.x = temp;
            }
            if (minGradientCol.y > maxGradientCol.y)
            {
                temp = minGradientCol.y;
                minGradientCol.y = maxGradientCol.y;
                maxGradientCol.y = temp;
            }
            if (minGradientCol.z > maxGradientCol.z)
            {
                temp = minGradientCol.z;
                minGradientCol.z = maxGradientCol.z;
                maxGradientCol.z = temp;
            }
        }

        private void swapMinMaxVec()
        {
            float temp;
            if (minVec.x > maxVec.x)
            {
                temp = minVec.x;
                minVec.x = maxVec.x;
                maxVec.x = temp;
            }
            if (minVec.y > maxVec.y)
            {
                temp = minVec.y;
                minVec.y = maxVec.y;
                maxVec.y = temp;
            }
            if (minVec.z > maxVec.z)
            {
                temp = minVec.z;
                minVec.z = maxVec.z;
                maxVec.z = temp;
            }
            if (minVec.w > maxVec.w)
            {
                temp = minVec.w;
                minVec.w = maxVec.w;
                maxVec.w = temp;
            }
        }

        private void swapMinMaxMat()
        {
            float temp;
            if (minMatrix.m00 > maxMatrix.m00)
            {
                temp = minMatrix.m00;
                minMatrix.m00 = maxMatrix.m00;
                maxMatrix.m00 = temp;
            }
            if (minMatrix.m01 > maxMatrix.m01)
            {
                temp = minMatrix.m01;
                minMatrix.m01 = maxMatrix.m01;
                maxMatrix.m01 = temp;
            }
            if (minMatrix.m02 > maxMatrix.m02)
            {
                temp = minMatrix.m02;
                minMatrix.m02 = maxMatrix.m02;
                maxMatrix.m02 = temp;
            }
            if (minMatrix.m03 > maxMatrix.m03)
            {
                temp = minMatrix.m03;
                minMatrix.m03 = maxMatrix.m03;
                maxMatrix.m03 = temp;
            }
            if (minMatrix.m10 > maxMatrix.m10)
            {
                temp = minMatrix.m10;
                minMatrix.m10 = maxMatrix.m10;
                maxMatrix.m10 = temp;
            }
            if (minMatrix.m11 > maxMatrix.m11)
            {
                temp = minMatrix.m11;
                minMatrix.m11 = maxMatrix.m11;
                maxMatrix.m11 = temp;
            }
            if (minMatrix.m12 > maxMatrix.m12)
            {
                temp = minMatrix.m12;
                minMatrix.m12 = maxMatrix.m12;
                maxMatrix.m12 = temp;
            }
            if (minMatrix.m13 > maxMatrix.m13)
            {
                temp = minMatrix.m13;
                minMatrix.m13 = maxMatrix.m13;
                maxMatrix.m13 = temp;
            }
            if (minMatrix.m20 > maxMatrix.m20)
            {
                temp = minMatrix.m20;
                minMatrix.m20 = maxMatrix.m20;
                maxMatrix.m20 = temp;
            }
            if (minMatrix.m21 > maxMatrix.m21)
            {
                temp = minMatrix.m21;
                minMatrix.m21 = maxMatrix.m21;
                maxMatrix.m21 = temp;
            }
            if (minMatrix.m22 > maxMatrix.m22)
            {
                temp = minMatrix.m22;
                minMatrix.m22 = maxMatrix.m22;
                maxMatrix.m22 = temp;
            }
            if (minMatrix.m23 > maxMatrix.m23)
            {
                temp = minMatrix.m23;
                minMatrix.m23 = maxMatrix.m23;
                maxMatrix.m23 = temp;
            }
            if (minMatrix.m30 > maxMatrix.m30)
            {
                temp = minMatrix.m30;
                minMatrix.m30 = maxMatrix.m30;
                maxMatrix.m30 = temp;
            }
            if (minMatrix.m31 > maxMatrix.m31)
            {
                temp = minMatrix.m31;
                minMatrix.m31 = maxMatrix.m31;
                maxMatrix.m31 = temp;
            }
            if (minMatrix.m32 > maxMatrix.m32)
            {
                temp = minMatrix.m32;
                minMatrix.m32 = maxMatrix.m32;
                maxMatrix.m32 = temp;
            }
            if (minMatrix.m33 > maxMatrix.m33)
            {
                temp = minMatrix.m33;
                minMatrix.m33 = maxMatrix.m33;
                maxMatrix.m33 = temp;
            }
        }

        private float randFloat(System.Random rand)
        {
            return (float)(rand.NextDouble() * (rand.NextDouble() > 0.5 ? -1.0f : 1.0f) * rand.Next());
        }

        private float randFloat(System.Random rand, float maxOrMin, bool isMax)
        {
            if (isMax) return maxOrMin - (float)(rand.NextDouble() * rand.Next());
            else return maxOrMin + (float)(rand.NextDouble() * rand.Next());
        }
        private float randFloat(System.Random rand, float max, float min)
        {
            if (min > max)
            {
                var temp = min;
                min = max;
                max = min;
            }
            return min + (float)(rand.NextDouble() * (max - min));
        }

        #endregion


        /*
         * private float minFloat;
        private float maxFloat;
        private float boolChance;
        private Vector3 minGradientCol; // Collection of 3 parameters
        private Vector3 maxGradientCol; // Collection of 3 parameters
        private Vector4 minVec; // Collection of 4 parameters
        private Vector4 maxVec; // Collection of 4 parameters
        private Matrix4x4 minMatrix; // Collection of 16 parameters
        private Matrix4x4 maxMatrix; // Collection of 16 parameters
         */

        #region New Random Generation Functions
        internal float newRandomFloat(AbstractMaterialNode node)
        { 
            if (!inputFloatRanges.ContainsKey(node))
            {
                float rangeMin;
                float rangeMax;
                Tuple<float,float> maximumRangeSize;
                maximumRangeSize = MutationHelper.evaluateFloatRangeSize(node.GetOutputSlots<MaterialSlot>());
                rangeMin = UnityEngine.Random.Range(maximumRangeSize.Item1, maximumRangeSize.Item2);
                rangeMax = UnityEngine.Random.Range(rangeMin, maximumRangeSize.Item2);
                Tuple<float, float> floatRange = Tuple.Create(rangeMin, rangeMax);
                inputFloatRanges.Add(node, floatRange);
                return rangeMin + ((float)rand.NextDouble() * (rangeMax - rangeMin));
            }
            else
            {
                return inputFloatRanges[node].Item1 + ((float)rand.NextDouble() * (inputFloatRanges[node].Item2 - inputFloatRanges[node].Item1));
            }
           
        } 
        #endregion
    }

    public static class MutationHelper
    {
        private static List<Type> noiseNodeTypes = new List<Type>()
        {
            typeof(VoronoiNode),
            typeof(GradientNoiseNode),
            typeof(NoiseNode)
        };

        private static Dictionary<MutationStrength,List<string>> mutationsPerStrength = new Dictionary<MutationStrength, List<string>>
        {
            {MutationStrength.High , new List<string>(){
                //"VertexDescription.Position",
                "VertexDescription.Normal",
                "SurfaceDescription.BaseColor",
                "SurfaceDescription.NormalTS"
            }},
            {MutationStrength.Medium, new List<string>(){
                "SurfaceDescription.Metallic",
                "SurfaceDescription.Smoothness",
                "SurfaceDescription.Emission"
            }},
            {MutationStrength.Low, new List<string>(){
                "SurfaceDescription.Occlusion"
            }}
        };

        private static Dictionary<string, List<Type>> blockNameToSlotType = new Dictionary<string, List<Type>>
        {
            {"VertexDescription.Position", new List<Type>(){
                typeof(PositionNode)
            }},
            {"VertexDescription.Normal", new List<Type>(){
                typeof(NormalVectorNode)
            }},
            {"SurfaceDescription.NormalTS", new List<Type>(){
                typeof(NormalVectorNode)
            }},
            {"SurfaceDescription.BaseColor", new List<Type>(){
                typeof(AssignableColorNode),
                typeof(ColorNode)
            }},
            {"SurfaceDescription.Emission", new List<Type>(){
                typeof(AssignableEmissionNode),
                typeof(ColorNode)
            }},
        };

        private static Dictionary<Type, int> scaleInputSlotIds = new Dictionary<Type, int>
        {
            { typeof(VoronoiNode), 2 },
            { typeof(GradientNoiseNode), 1 },
            {typeof(NoiseNode), 1 }
        };

        internal static void newMutation(GraphManager graphManager, MutationStrength strength)
        {
            bool isPopulated = false;
            bool mutationSuccessfull = false;
            foreach(AbstractMaterialNode node in graphManager.getNodes<AbstractMaterialNode>())
            {
                if (!node.GetType().IsEquivalentTo(typeof(BlockNode)))
                {
                    isPopulated = true;
                    break;
                }
            }
            if (!isPopulated) mutationSuccessfull = expandGraph(graphManager, strength);
            else
            {
                List<Func<GraphManager, MutationStrength, bool>> possibleMutations = new List<Func<GraphManager, MutationStrength, bool>>();

                if (graphManager.getGenes().expandAllowed)
                {
                    Debug.LogWarning("Adding Expansion as option");
                    possibleMutations.Add(expandGraph);
                }
                else
                {
                    Debug.Log("Expansion is not allowed");
                }

                foreach (AbstractMaterialNode node in graphManager.getNodes<AbstractMaterialNode>())
                {
                    if (strength < MutationStrength.High ) break;
                    if (node is VoronoiNode || node is GradientNoiseNode || node is NoiseNode)
                    { 
                        Debug.LogWarning("Adding Noise Change as option");
                        possibleMutations.Add(changeNoiseNodes);
                        break;
                    }
                }
                // Use other mutations
                if (strength >= MutationStrength.Medium &&
                    graphManager.getNodes<AssignableVector1Node>().Count > 0
                    )
                {
                    Debug.LogWarning("Added temporization as option");
                    possibleMutations.Add(temporizeFloatInput);
                }

                if (strength >= MutationStrength.Low)
                {
                    try
                    {
                        GeneticHandler genes = graphManager.getGenes();
                        if(genes.inputFloatRanges.Count > 0)
                        {
                            possibleMutations.Add(moveFloatRange);
                        }
                    }
                    catch (NullReferenceException) { }
                }
                UnityEngine.Random.InitState(DateTime.Now.Millisecond);
                Debug.Log("Choosing mutation");
                if (possibleMutations.Count == 0)
                {
                    Debug.LogFormat("No available mutations for {0}",AssetDatabase.GUIDToAssetPath(graphManager.getGraph().assetGuid));
                    graphManager.finalizeChanges();
                    return;
                }
                mutationSuccessfull = possibleMutations[UnityEngine.Random.Range(0, possibleMutations.Count)].Invoke(graphManager, strength);
            }
            //Debug.LogFormat("Mutating with {0} strength", strength);
            GraphData graph = graphManager.getGraph();
            
            if (!mutationSuccessfull) Debug.Log("Could not mutate");
            //randomizeGraphInputs();


        }
        #region Mutation Functions
        public static bool expandGraph(GraphManager graphManager, MutationStrength strength)
        {
            GraphData graph = graphManager.getGraph(); 
            List<string> candidateMutationBlockNames = null;
            try
            {
                candidateMutationBlockNames = new List<string>(mutationsPerStrength[strength]);
            }
            catch (KeyNotFoundException)
            {
                throw new ArgumentException(string.Format("Strength {0} is not an allowed mutation strength", strength.ToString()));
            }
            candidateMutationBlockNames.RemoveAll(candidate => ShaderGraphUtilities.tryGetBlockNode(graph, candidate) == null);
            if (candidateMutationBlockNames.Count == 0)
            {
                Debug.LogErrorFormat("No compatible mutations have {0} strength", strength);
                return false;
            }
            string chosenMutationBlockName = candidateMutationBlockNames[UnityEngine.Random.Range(0, candidateMutationBlockNames.Count)];
            BlockNode mutationBlockNode = ShaderGraphUtilities.tryGetBlockNode(graph, chosenMutationBlockName);
            if (mutationBlockNode == null) throw new Exception("Mutation should be compatible but block node is null");
            List<Type> mutationCompatibleSlots;
            List<AbstractMaterialNode> mutationCandidateNodes = new List<AbstractMaterialNode>();
            if (blockNameToSlotType.TryGetValue(chosenMutationBlockName, out mutationCompatibleSlots))
            {
                foreach (Type t in mutationCompatibleSlots)
                {
                    mutationCandidateNodes.AddRange(graphManager.getReachingTo(mutationBlockNode));
                    mutationCandidateNodes.RemoveAll(node => !node.GetType().IsEquivalentTo(t));
                    mutationCandidateNodes.RemoveAll(node => ShaderGraphUtilities.hasConnectedInputs(node));
                }
            }
            mutationCandidateNodes.Add(mutationBlockNode);
            AbstractMaterialNode chosenNode = mutationCandidateNodes[UnityEngine.Random.Range(0, mutationCandidateNodes.Count)];
            int chosenMutation = RandomizationHelper.chooseGraphMutation(mutationBlockNode.GetSlotReference(0).slot);
            if (chosenMutation != -1)
            {
                GraphManager mutationReferenceGraph = RandomizationHelper.referenceGraphs[chosenMutation];
                Dictionary<AbstractMaterialNode, AbstractMaterialNode> referenceToOwnGraph = new Dictionary<AbstractMaterialNode, AbstractMaterialNode>();
                AbstractMaterialNode referenceBlockNode = getCompatibleBlockNode(mutationReferenceGraph.getGraph(), chosenMutationBlockName);

                if (!(chosenNode is BlockNode))
                {
                    Debug.LogFormat("Not BlockNode, {0}", chosenNode.GetType());
                    SlotReference referenceOutputSlot = mutationReferenceGraph.getInputEdge(referenceBlockNode.GetSlotReference(0)).outputSlot;
                    AbstractMaterialNode referenceOutputNode = mutationReferenceGraph.getInputEdge(referenceBlockNode.GetSlotReference(0)).outputSlot.node;
                    AbstractMaterialNode replacingOutputNode = (AbstractMaterialNode)Activator.CreateInstance(referenceOutputNode.GetType());
                    replacingOutputNode.drawState = chosenNode.drawState;
                    graph.AddNode(replacingOutputNode);
                    SlotReference replacingOutputSlot = replacingOutputNode.GetSlotReference(referenceOutputSlot.slotId);
                    foreach (MaterialSlot outputSlot in chosenNode.GetOutputSlots<MaterialSlot>())
                    {
                        if (outputSlot.isConnected)
                        {
                            List<IEdge> outputEdgesToReplace = graphManager.getOutputEdges(outputSlot.slotReference);
                            if (outputEdgesToReplace == null)
                                throw new NullReferenceException(string.Format("Output Slot {0} in {1} at {2} is connected but no output edges were found. Reference is {3}", outputSlot.RawDisplayName(), outputSlot.owner, AssetDatabase.GUIDToAssetPath(outputSlot.owner.owner.assetGuid), AssetDatabase.GUIDToAssetPath(referenceBlockNode.owner.assetGuid)));
                            foreach (IEdge outputEdge in outputEdgesToReplace)
                            {
                                graph.Connect(replacingOutputSlot, outputEdge.inputSlot);
                            }
                        }
                    }
                    referenceToOwnGraph.Add(referenceOutputNode, replacingOutputNode);
                    graphManager.inheritNodes(mutationReferenceGraph.getGraph(), referenceOutputNode, replacingOutputNode, referenceToOwnGraph);
                }
                else
                {
                    List<AbstractMaterialNode> associatedNodes = graphManager.inheritInputNode(referenceBlockNode.GetSlotReference(0).slot, chosenNode.GetSlotReference(0).slot);
                    referenceToOwnGraph.Add(associatedNodes[0], associatedNodes[1]);
                    graphManager.inheritNodes(mutationReferenceGraph.getGraph(), associatedNodes[0], associatedNodes[1], referenceToOwnGraph);


                }
                graphManager.inheritInputs(mutationReferenceGraph.getGraph(), referenceToOwnGraph);
                return true;
            }
            else
            {
                Debug.LogError("No mutation was applicable");
            }
            return false;
        }

        public static bool changeNoiseNodes(GraphManager graphManager,MutationStrength strength)
        {
            Debug.LogWarningFormat("Changing Noise Nodes in {0}", AssetDatabase.GUIDToAssetPath(graphManager.getGraph().assetGuid));
            List<AbstractMaterialNode> noiseNodesInGraph = new List<AbstractMaterialNode>(graphManager.getNodes<AbstractMaterialNode>());
            noiseNodesInGraph.RemoveAll(node => !noiseNodeTypes.Contains(node.GetType()));
            if (noiseNodesInGraph.Count < 1) return true;
            AbstractMaterialNode targetNoiseNode = RandomizationHelper.chooseRandomNode(noiseNodesInGraph);
            List<AbstractMaterialNode> targetNoiseNodes = new List<AbstractMaterialNode>() { targetNoiseNode };

            foreach (MaterialSlot inputSlot in targetNoiseNode.GetInputSlots<MaterialSlot>())
            {
                if (inputSlot.isConnected)
                {
                    IEdge inputEdge = graphManager.getInputEdge(inputSlot.slotReference);

                    foreach (IEdge outputEdge in graphManager.getOutputEdges(inputEdge.outputSlot))
                    {
                        AbstractMaterialNode inputNode = outputEdge.inputSlot.node;
                        if (inputNode.GetType().IsEquivalentTo(targetNoiseNode.GetType()) && !targetNoiseNodes.Contains(inputNode))
                        {
                            targetNoiseNodes.Add(inputNode);
                        }
                    }
                }
            }

            Type newNoiseType = noiseNodeTypes.FindAll(type => !type.IsEquivalentTo(targetNoiseNode.GetType()))[UnityEngine.Random.Range(0, noiseNodeTypes.Count - 1)];
            List<AbstractMaterialNode> addedNodes;

            int chosenOutSlot = 0;
            bool newNoiseIsVoronoi = newNoiseType.IsEquivalentTo(typeof(VoronoiNode));
            if (newNoiseIsVoronoi)
            {
                addedNodes = new List<AbstractMaterialNode>();
                chosenOutSlot = UnityEngine.Random.Range(0, 2);
            }

            foreach (AbstractMaterialNode oldNoiseNode in targetNoiseNodes)
            {
                AbstractMaterialNode newNoiseNode = (AbstractMaterialNode)Activator.CreateInstance(newNoiseType);
                newNoiseNode.drawState = oldNoiseNode.drawState;
                graphManager.getGraph().AddNode(newNoiseNode);

                MaterialSlot newOutputSlot = new List<MaterialSlot>(newNoiseNode.GetOutputSlots<MaterialSlot>())[chosenOutSlot];
                foreach (MaterialSlot oldOutputSlot in oldNoiseNode.GetOutputSlots<MaterialSlot>())
                {
                    if (!oldOutputSlot.isConnected)
                    {
                        Debug.LogFormat("{0} in {1} at {2} is not connected", oldOutputSlot.displayName, oldOutputSlot.owner.name, AssetDatabase.GUIDToAssetPath(oldOutputSlot.owner.owner.assetGuid));
                        continue;
                    }
                    foreach (IEdge oldOutputEdge in graphManager.getOutputEdges(oldOutputSlot.slotReference))
                    {
                        graphManager.getGraph().Connect(newOutputSlot.slotReference, oldOutputEdge.inputSlot);
                    }
                }

                SlotReference oldInputUVSlot = oldNoiseNode.GetSlotReference(0);
                SlotReference newInputUVSlot = newNoiseNode.GetSlotReference(0);
                if (oldInputUVSlot.slot.isConnected)
                {
                    IEdge oldInputUVEdge = graphManager.getInputEdge(oldInputUVSlot);
                    graphManager.getGraph().Connect(oldInputUVEdge.outputSlot, newInputUVSlot);
                }

                SlotReference oldScaleInputSlot = oldNoiseNode.GetSlotReference(scaleInputSlotIds[oldNoiseNode.GetType()]);
                SlotReference newScaleInputSlot = newNoiseNode.GetSlotReference(scaleInputSlotIds[newNoiseNode.GetType()]);
                if (oldScaleInputSlot.slot.isConnected)
                {
                    IEdge oldScaleInputEdge = graphManager.getInputEdge(oldScaleInputSlot);
                    graphManager.getGraph().Connect(oldScaleInputEdge.outputSlot, newScaleInputSlot);
                }

                graphManager.getGraph().RemoveNode(oldNoiseNode);
            }
            return true;
        }

        public static bool temporizeFloatInput(GraphManager graphManager, MutationStrength strength)
        {
            Debug.LogWarningFormat("Temporizing Float node in {0}", AssetDatabase.GUIDToAssetPath(graphManager.getGraph().assetGuid));
            List<AbstractMaterialNode> floatNodesInGraph = new List<AbstractMaterialNode>(graphManager.getNodes<AssignableVector1Node>());
            floatNodesInGraph.RemoveAll(node => node.IsSlotConnected(1) || !node.IsSlotConnected(0));
            if (floatNodesInGraph.Count < 1) return true;
            floatNodesInGraph.TrimExcess();
            AbstractMaterialNode temporizedNode = RandomizationHelper.chooseRandomNode(floatNodesInGraph);
            AbstractMaterialNode timeNode = (AbstractMaterialNode)Activator.CreateInstance(typeof(TimeNode));
            AbstractMaterialNode multiplyNode = (AbstractMaterialNode)Activator.CreateInstance(typeof(MultiplyNode));
            AbstractMaterialNode multiplierNode = (AbstractMaterialNode)Activator.CreateInstance(typeof(Vector1Node));
            Tuple<float, float> valueRanges;
            try
            {
                valueRanges = graphManager.getGenes().inputFloatRanges[temporizedNode];
            }
            catch(KeyNotFoundException)
            {
                valueRanges = evaluateFloatRangeSize(temporizedNode.GetOutputSlots<MaterialSlot>());
            }
            #region arranging Nodes
            graphManager.getGraph().AddNode(multiplyNode);
            multiplyNode.previewExpanded = false;
            DrawState drawState = multiplyNode.drawState;
            drawState.expanded = false;
            drawState.position = temporizedNode.drawState.position;
            Rect newPosition = drawState.position;
            newPosition.position -= new Vector2(temporizedNode.drawState.position.width * 1.5f, 0f);
            drawState.position = newPosition;
            multiplyNode.drawState = drawState;

            graphManager.getGraph().AddNode(timeNode);
            drawState = timeNode.drawState;
            drawState.expanded = false;
            drawState.position = multiplyNode.drawState.position;
            newPosition = drawState.position;
            newPosition.position -= new Vector2(multiplyNode.drawState.position.width * 1.5f, 0f);
            drawState.position = newPosition;
            timeNode.drawState = drawState;

            graphManager.getGraph().AddNode(multiplierNode);
            drawState = multiplierNode.drawState;
            drawState.expanded = false;
            drawState.position = timeNode.drawState.position;
            newPosition = drawState.position;
            newPosition.position -= new Vector2(0f, timeNode.drawState.position.height * 1.5f);
            drawState.position = newPosition;
            multiplierNode.drawState = drawState;
            #endregion
            Vector1MaterialSlot multiplierSlot = (Vector1MaterialSlot)multiplierNode.GetSlotReference(1).slot;
            multiplierSlot.value = UnityEngine.Random.Range(valueRanges.Item1,valueRanges.Item2);
            graphManager.getGenes().inputFloatRanges.Add(multiplierNode, valueRanges);

            graphManager.getGraph().Connect(timeNode.GetSlotReference(1), multiplyNode.GetSlotReference(0));
            graphManager.getGraph().Connect(multiplierNode.GetSlotReference(0), multiplyNode.GetSlotReference(1));
            graphManager.getGraph().Connect(multiplyNode.GetSlotReference(2), temporizedNode.GetSlotReference(1));

            return true;
        }

        public static bool moveFloatRange(GraphManager graphManager, MutationStrength strength)
        {
            GeneticHandler genes;
            try
            {
                genes = graphManager.getGenes();
            }
            catch (NullReferenceException)
            {
                Debug.LogError("moveRange has been called but the graph has not ranges. This should not happen.");
                throw new ArgumentException("Graph has no genes associated");
            }
            if (genes.inputFloatRanges.Count == 0) return false;

            int chosenIndex = UnityEngine.Random.Range(0, genes.inputFloatRanges.Keys.Count);
            AbstractMaterialNode chosenNode = null;
            int i = 0;
            foreach(AbstractMaterialNode node in genes.inputFloatRanges.Keys)
            {
                if (i == chosenIndex)
                {
                    chosenNode = node;
                    break;
                }
                else i++;
            }

            if (!graphManager.getGraph().ContainsNode(chosenNode)) throw new InvalidOperationException("Node was not found in graph");
            Tuple<float, float> maxRange = evaluateFloatRangeSize(chosenNode.GetOutputSlots<MaterialSlot>());
            float rangeMin = UnityEngine.Random.Range(maxRange.Item1, maxRange.Item2);
            float rangeMax = UnityEngine.Random.Range(rangeMin, maxRange.Item2);

            Tuple<float, float> range = Tuple.Create(rangeMin, rangeMax);
            genes.inputFloatRanges[chosenNode] = range;


            return true;
        }
        #endregion
        public static bool changeColors(GraphManager graphManager)
        {
            Debug.LogFormat("Changing colors in {0}", AssetDatabase.GUIDToAssetPath(graphManager.getGraph().assetGuid));
            List<AbstractMaterialNode> colorNodes = new List<AbstractMaterialNode>(graphManager.getNodes<RandomizableColorNode>());
            colorNodes.AddRange(graphManager.getNodes<AssignableColorNode>());
            GeneticHandler graphGenes = null;
            bool useGeneticValues;
            try
            {
                graphGenes = graphManager.getGenes();
                useGeneticValues = true;
            }
            catch
            {
                useGeneticValues = false;
            }
            if (ShaderGraphUtilities.tryGetBlockNode(graphManager.getGraph(), BlockNodes.fragmentColor)?.IsSlotConnected(0) == false)
            {
                Debug.Log("Changing color on block node");
                Color newColor;
                bool useHDRMode;
                ColorRGBMaterialSlot blockNodeSlot = (ColorRGBMaterialSlot)ShaderGraphUtilities.tryGetBlockNode(graphManager.getGraph(), BlockNodes.fragmentColor).GetSlotReference(0).slot;
                if (useGeneticValues)
                {
                    newColor = graphGenes.getColor();
                    useHDRMode = graphGenes.getBool();
                }
                else
                {
                    newColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                    useHDRMode = UnityEngine.Random.value > 0.5;
                    if (useHDRMode)
                    {
                        switch (blockNodeSlot.owner.sgVersion)
                        {
                            case 0:
                                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                                    newColor = newColor.linear;
                                break;
                            case 1:
                                if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                                    newColor = newColor.gamma;
                                break;
                        }
                    }
                    else
                    {
                        if (PlayerSettings.colorSpace == ColorSpace.Linear)
                            newColor = newColor.linear;
                    }

                }
                ColorRGBMaterialSlot tempSlot = new ColorRGBMaterialSlot(0, "temp", "none", SlotType.Input, newColor, useHDRMode ? ColorMode.HDR : ColorMode.Default);
                blockNodeSlot.CopyValuesFrom(tempSlot);
            }
            if (colorNodes.Count == 0) return true;
            foreach (AbstractMaterialNode colorNode in colorNodes)
            {
                ColorNode node = (ColorNode)colorNode;
                ColorNode.Color newColorValue;
                if (useGeneticValues)
                {
                    Color newColor = graphGenes.getColor();
                    bool useHDRMode = graphGenes.getBool();
                    newColorValue = new RandomizableColorNode.Color(newColor, useHDRMode ? ColorMode.HDR : ColorMode.Default);
                }
                else
                {
                    Color newColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                    bool useHDRMode = UnityEngine.Random.value > 0.5 ;
                    if (useHDRMode)
                    {
                        switch (node.sgVersion)
                        {
                            case 0:
                                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                                    newColor = newColor.linear;
                                break;
                            case 1:
                                if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                                    newColor = newColor.gamma;
                                break;
                        }
                    }
                    else
                    {
                        if (PlayerSettings.colorSpace == ColorSpace.Linear)
                            newColor = newColor.linear;
                    }
                    newColorValue = new RandomizableColorNode.Color(newColor, useHDRMode ? ColorMode.HDR : ColorMode.Default);
                }
                node.color = newColorValue;
            }
            return true;
        }

        public static bool changeFloatValues(GraphManager graphManager)
        {
            List<AbstractMaterialNode> modifiableNodes = new List<AbstractMaterialNode>(graphManager.getNodes<RandomizableVector1Node>());
            modifiableNodes.AddRange(graphManager.getNodes<AssignableVector1Node>());
            bool useGeneticValues;
            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
                useGeneticValues = true;
            }
            catch
            {
                useGeneticValues = false;
            }

            if (modifiableNodes.Count < 1)
                return true;
            
            foreach (AbstractMaterialNode node in modifiableNodes)
            {
                Vector1Node vector1Node = (Vector1Node)node;
                float newValue;
                if (useGeneticValues)
                {
                    newValue = graphGenes.newRandomFloat(vector1Node);
                }
                else
                {
                    newValue = UnityEngine.Random.value * new System.Random().Next() * UnityEngine.Random.value > 0.5 ? -1 : 1;
                }
                Vector1MaterialSlot valueSlot = (Vector1MaterialSlot)vector1Node.GetSlotReference(1).slot;
                Vector1MaterialSlot tempSlot = new Vector1MaterialSlot(0, "testing", "none", SlotType.Input, newValue);
                //valueSlot.value = newValue;
                valueSlot.CopyValuesFrom(tempSlot);
                if (valueSlot.value != newValue)
                {
                    Debug.LogError("CopyValuesFrom not working");
                    valueSlot.value = newValue;
                }
            }
            return true;

        }

        private static Dictionary<Tuple<Type, int>, RangeSize> rangesPerNodeType = new Dictionary<Tuple<Type, int>, RangeSize>()
        {
            {Tuple.Create(typeof(VoronoiNode),1),RangeSize.Medium },
            {Tuple.Create(typeof(VoronoiNode),2),RangeSize.Medium },
            {Tuple.Create(typeof(GradientNoiseNode),1),RangeSize.Medium },
            {Tuple.Create(typeof(NoiseNode),1),RangeSize.Large },
            {Tuple.Create(typeof(StepNode),0),RangeSize.One },
            {Tuple.Create(typeof(MultiplyNode),0),RangeSize.Tiny },
            {Tuple.Create(typeof(MultiplyNode),1),RangeSize.Tiny },
            {Tuple.Create(typeof(PowerNode),1),RangeSize.Small },
            {Tuple.Create(typeof(SaturateNode),0),RangeSize.One },
            {Tuple.Create(typeof(RotateNode),2),RangeSize.Small },
        };

        internal static Tuple<float,float> evaluateFloatRangeSize(IEnumerable<MaterialSlot> outputSlots)
        {
            RangeSize? chosenRange = null;
            foreach(MaterialSlot outputSlot in outputSlots)
            {
                if (outputSlot.isInputSlot) throw new ArgumentException("The list must contain output slots");
                Vector1MaterialSlot outputFloatSlot = null;
                try
                {
                    outputFloatSlot = (Vector1MaterialSlot)outputSlot;   
                }
                catch { continue; }
                if (outputFloatSlot.isConnected)
                {
                    GraphData graph = outputSlot.owner.owner;
                    foreach(IEdge outputEdge in graph.GetEdges(outputFloatSlot.slotReference))
                    {
                        RangeSize foundRange;
                        if (rangesPerNodeType.TryGetValue(Tuple.Create(outputEdge.inputSlot.node.GetType(), outputEdge.inputSlot.slotId),out foundRange)){
                            if (chosenRange != null) chosenRange = (RangeSize)(int)(((int)chosenRange + (int)foundRange) / 2);
                            else chosenRange = foundRange;
                        }
                        if(outputEdge.inputSlot.node is ClampNode)
                        {
                            ClampNode clampNode = (ClampNode)outputEdge.inputSlot.node;
                            float min = ((DynamicVectorMaterialSlot)clampNode.GetSlotReference(1).slot).value.x;
                            float max = ((DynamicVectorMaterialSlot)clampNode.GetSlotReference(2).slot).value.x;
                            return Tuple.Create(min, max);
                        }
                    }

                }
            }
            if (chosenRange == null) chosenRange = RangeSize.Tiny;
            return Tuple.Create<float,float>(-RangeSizeValues.getRange((RangeSize)chosenRange), RangeSizeValues.getRange((RangeSize)chosenRange));
        }

        private static Dictionary<string, Type> blockNodeInputTypes = new Dictionary<string, Type>
        {
            { "VertexDescription.Position" ,    typeof(PositionMaterialSlot)},
            { "VertexDescription.Normal",       typeof(NormalMaterialSlot) },
            { "VertexDescription.Tangent",      typeof(TangentMaterialSlot) },
            { "SurfaceDescription.BaseColor",   typeof(ColorRGBMaterialSlot) },
            { "SurfaceDescription.NormalTS",    typeof(NormalMaterialSlot) },
            { "SurfaceDescription.Metallic",    typeof(Vector1MaterialSlot) },
            { "SurfaceDescription.Smoothness",  typeof(Vector1MaterialSlot) },
            { "SurfaceDescription.Emission",    typeof(ColorRGBMaterialSlot) },
            { "SurfaceDescription.Occlusion",   typeof(Vector1MaterialSlot) }
        };
        private static BlockNode getCompatibleBlockNode(GraphData graph,string blockNodeName)
        {
            Type inputType = blockNodeInputTypes[blockNodeName];
            BlockNode exactMatch = ShaderGraphUtilities.tryGetBlockNode(graph, blockNodeName);
            if (exactMatch.IsSlotConnected(0)) return exactMatch;
            else
            {
                foreach(string blockNode in blockNodeInputTypes.Keys)
                {
                    if(blockNodeInputTypes[blockNode] == inputType)
                    {
                        BlockNode compatibleBlockNode = ShaderGraphUtilities.tryGetBlockNode(graph, blockNode);
                        if (compatibleBlockNode.IsSlotConnected(0)) return compatibleBlockNode;
                    }
                }
            }
            throw new Exception(string.Format("Could not find anything compatible to {0} in {1}, this should not happen", blockNodeName, AssetDatabase.GUIDToAssetPath(graph.assetGuid)));
        }


    }


    public enum ShaderStage
    {
        Vertex,
        Fragment
    }

    public static class RandomizationHelper
    {
        private static int defaultWeight = 10;
        public static List<GraphManager> referenceGraphs {
            get;
            private set;
        }

        private static Dictionary<string,Type> blockNodeNames = new Dictionary<string, Type>
        {
            { "VertexDescription.Position" , typeof(PositionMaterialSlot)},
            { "VertexDescription.Normal", typeof(NormalMaterialSlot) },
            { "VertexDescription.Tangent", typeof(TangentMaterialSlot) },
            { "SurfaceDescription.BaseColor", typeof(ColorRGBMaterialSlot) },
            { "SurfaceDescription.NormalTS", typeof(NormalMaterialSlot) },
            { "SurfaceDescription.Metallic", typeof(Vector1MaterialSlot) },
            { "SurfaceDescription.Smoothness", typeof(Vector1MaterialSlot) },
            { "SurfaceDescription.Emission", typeof(ColorRGBMaterialSlot) },
            { "SurfaceDescription.Occlusion", typeof(Vector1MaterialSlot) }
        };

        private static Dictionary<Tuple<Type,ShaderStage>, List<GraphManager>> graphsPerBlocks;

        private static List<Type> nodeTypes = new List<Type>(NodeClassCache.knownNodeTypes);

        private static Dictionary<string, int> weightPerName = new Dictionary<string, int>()
        {
            {"VertexDescription.Position",1 },
            {"VertexDescription.Normal",1 }
        };
        private static Dictionary<Type, int> weightPerType = new Dictionary<Type, int>()
        {
        };

        internal static AbstractMaterialNode chooseRandomInputNode(List<AbstractMaterialNode> nodes)
        {
            List<int> weightList = new List<int>(nodes.Count);
            int weightSum = 0;
            foreach(var node in nodes)
            {
                int weight = defaultWeight;
                if (weightPerType.ContainsKey(node.GetType()))
                {
                    weight = weightPerType[node.GetType()];
                }
                else
                {
                    foreach(var key in weightPerName.Keys)
                    {
                        if (node.name.Contains(key))
                        {
                            weight = weightPerName[key];
                            break;
                        }
                    }
                }
                weightList.Add(weight);
                weightSum += weight;
            }
            int index = 0, randVal = UnityEngine.Random.Range(1,weightSum);
            while(randVal > 0)
            {
                randVal = randVal - weightList[index];
                index++;
            }
            return nodes[index-1];
        } 

        internal static AbstractMaterialNode chooseRandomNode(List<AbstractMaterialNode> nodes)
        {
            int index = UnityEngine.Random.Range(0, nodes.Count);
            try
            {
                return nodes[index];
            }
            catch(ArgumentOutOfRangeException)
            {
                Debug.LogError(index);
                Debug.LogError(nodes.Capacity);
                Debug.LogError(nodes.Count);
                List<AbstractMaterialNode> noHoleNodes = nodes.FindAll(node => node is AbstractMaterialNode);
                return noHoleNodes[index];
            }

        }

        internal static Type chooseRandomOutputNode(AbstractMaterialNode inputNode, List<Type> compatibleNodeTypes, GraphManager graph)
        {
            int index = randomIndex(compatibleNodeTypes.Count);
            while (DeniedOperationsChecker.isNotValidNodeType(compatibleNodeTypes[index]) || DeniedOperationsChecker.isLinkDisabled(compatibleNodeTypes[index], inputNode.GetType()) || DeniedOperationsChecker.isLinkDisabled(compatibleNodeTypes[index], inputNode.name))
            {
                index = randomIndex(compatibleNodeTypes.Count);
            }
            return compatibleNodeTypes[index];
            throw new NotImplementedException("Implement heuristic for choosing random outputnode");
        }

        internal static MaterialSlot chooseRandomSlot(List<MaterialSlot> slots)
        {
            return slots[randomIndex(slots.Count)];
        }

        internal static Type chooseRandomNodeType()
        {
            int index = randomIndex(nodeTypes.Count);
            return nodeTypes[index];
        }

        private static int randomIndex(int listSize)
        {
            return UnityEngine.Random.Range(0, listSize);
        }

        public static void initializeGraphMutations()
        {
            string refDirectory = ShaderGraphUtilities.referencePath;
            referenceGraphs = new List<GraphManager>();
            graphsPerBlocks = new Dictionary<Tuple<Type,ShaderStage>, List<GraphManager>>();
            int loadedReferences = 0;
            foreach(var file in Directory.GetFiles(refDirectory))
            {
                if (file.EndsWith(ShaderGraphImporter.Extension))
                {
                    //Debug.Log(file.Remove(file.IndexOf(".shadergraph"),ShaderGraphImporter.Extension.Length+1).Remove(file.IndexOf(refDirectory),refDirectory.Length+1));
                    GraphManager referenceGraphManager = new GraphManager(file, false, GraphType.Unknown);
                    referenceGraphs.Add(referenceGraphManager);
                    foreach(BlockNode node in referenceGraphManager.getNodes<BlockNode>())
                    {
                        ShaderStage stage = node.name.Contains("Vertex") ? ShaderStage.Vertex : ShaderStage.Fragment;
                        if (node.IsSlotConnected(0) && blockNodeNames.TryGetValue(node.name,out Type inputSlotType))
                        {
                            Tuple<Type, ShaderStage> key = Tuple.Create(inputSlotType, stage);
                            if (graphsPerBlocks.ContainsKey(key))
                            {
                                graphsPerBlocks[key].Add(referenceGraphManager);
                            }
                            else
                            {
                                graphsPerBlocks.Add(key, new List<GraphManager>() { referenceGraphManager });
                            }
                        }
                    }
                    loadedReferences++;
                }
            }
            ShaderGraphUtilities.numberOfReferences = loadedReferences;

        }
        internal static int chooseGraphMutation()
        {
            if (referenceGraphs == null || referenceGraphs.Count == 0) return -1;
            int indexChosen = UnityEngine.Random.Range(-1, referenceGraphs.Count) ;
            return indexChosen;
        }
        internal static int chooseGraphMutation(MaterialSlot inputSlot)
        {
            Debug.LogFormat("Choosing mutation for {0} {1} in {2}", inputSlot.owner.GetType().Name, inputSlot.owner.name, AssetDatabase.GUIDToAssetPath(inputSlot.owner.owner.assetGuid));

            bool allStagesCompatibility = true;
            ShaderStage stage = ShaderStage.Vertex;
            if(inputSlot is NormalMaterialSlot)
            {
                if (inputSlot.owner.name.Contains("Vertex"))
                {
                    allStagesCompatibility = false;
                    stage = ShaderStage.Vertex;
                }
                else if (inputSlot.owner.name.Contains("Surface"))
                {
                    allStagesCompatibility = false;
                    stage = ShaderStage.Fragment;
                }
            }
            if (referenceGraphs == null || referenceGraphs.Count == 0) return -1;
            if (!allStagesCompatibility) {
                Tuple<Type, ShaderStage> key = Tuple.Create(inputSlot.GetType(), stage);
                if( !graphsPerBlocks.TryGetValue(key, out List<GraphManager> compatibleReferences)) return -1;
                GraphManager chosenReference = compatibleReferences[UnityEngine.Random.Range(0, compatibleReferences.Count)];
                int indexChosen = referenceGraphs.IndexOf(chosenReference);
                return indexChosen;
            }
            else
            {
                List<GraphManager> compatibleReferences = new List<GraphManager>();
                List<GraphManager> tempReferenceList = new List<GraphManager>();
                Tuple<Type, ShaderStage> vertexKey = Tuple.Create(inputSlot.GetType(), ShaderStage.Vertex);
                Tuple<Type, ShaderStage> fragmentKey = Tuple.Create(inputSlot.GetType(), ShaderStage.Fragment);

                if(graphsPerBlocks.TryGetValue(vertexKey, out tempReferenceList)) compatibleReferences.AddRange(tempReferenceList);
                if (graphsPerBlocks.TryGetValue(fragmentKey, out tempReferenceList)) compatibleReferences.AddRange(tempReferenceList);

                if (compatibleReferences.Count == 0)
                    return -1;

                foreach(var reference in compatibleReferences)
                {
                    Debug.LogFormat("Compatible : {0}", AssetDatabase.GUIDToAssetPath(reference.getGraph().assetGuid));
                }
                GraphManager chosenReference = compatibleReferences[UnityEngine.Random.Range(0, compatibleReferences.Count)];
                int indexChosen = referenceGraphs.IndexOf(chosenReference);
                return indexChosen;
            }
        }
    }
    internal static class DeniedOperationsChecker
    {
        private static List<Type> deniedNodeTypes = new List<Type>()
        {
            //Basic from package
            typeof(KeywordNode),
            typeof(PropertyNode),
            typeof(SubGraphNode),
            //added
            typeof(ComputeDeformNode),
            typeof(ParallaxMappingNode),
            typeof(ParallaxOcclusionMappingNode),
            typeof(SampleVirtualTextureNode),
            typeof(SubGraphNode),
            typeof(KeywordNode),
            typeof(PropertyNode)
        };
        private static List<string> deniedFullNameNodes = new List<string>()
        {
            "UnitTests"
        };

        private static List<string> deniedTestNodes = new List<string>()
        {
            "TestNode",
            "TestableNode",
            "DynamicNode"
        };
        
        private static Dictionary<Type, List<Type>> disabledConnections = new Dictionary<Type, List<Type>>() {
        };

        private static Dictionary<Type, List<string>> disabledConnectionsTypeName = new Dictionary<Type, List<string>>()
        {
            {typeof(RoundedPolygonNode), new List<string>(){
                "VertexDescription"
            } }
        };

        private static List<string> disabledInputNodeNames = new List<string>()
        {
            "VertexDescription",
            "NormalTS"
        };

        private static List<Type> disabledInputNodeTypes = new List<Type>() { };
        private static List<Type> disabledInputSlotTypes= new List<Type>()
        {
            typeof(UVMaterialSlot)
        };


        public static bool isLinkDisabled(Type from, string toName)
        {
            try
            {
                return !disabledConnectionsTypeName[from.GetType()].Exists(name => toName.Contains(name));
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
        public static bool isNotValidNodeType(Type n)
        {
            return (!n.IsClass || n.IsAbstract)
                || n == null
                || deniedTestNodes.Contains(n.Name)
                || deniedFullNameNodes.Exists(name => n.FullName.Contains(name))
                || deniedNodeTypes.Contains(n)
                || NodeClassCache.GetAttributeOnNodeType<TitleAttribute>(n) == null
                ;
        }
        public static bool isLinkDisabled(Type from, Type to)
        {

            if (from.IsSubclassOf(typeof(AbstractMaterialNode)) && to.IsSubclassOf(typeof(AbstractMaterialNode)))
            {
                try
                {
                    return !disabledConnections[from.GetType()].Contains(to.GetType());
                }
                catch (KeyNotFoundException)
                {
                    return false;
                }
            }
            else
            {
                if (!from.IsSubclassOf(typeof(AbstractMaterialNode)))
                    throw new ArgumentException(string.Format("The function must receive Node type arguments it received {0} instead", from.Name));
                if (!to.IsSubclassOf(typeof(AbstractMaterialNode)))
                    throw new ArgumentException(string.Format("The function must receive Node type arguments it received {0} instead", to.Name));
                throw new Exception("Unknown error when checking if link is disabled");
            }
        }
        public static bool isRandomizationDisabled(MaterialSlot slot)
        {
            Type nodeType = slot.owner.GetType();
            string nodeName = slot.owner.name;
            return disabledInputNodeNames.Exists(name => nodeName.Contains(name) || name.Contains(nodeName)) ||
                disabledInputNodeTypes.Exists(type=> type.IsEquivalentTo(nodeType)) || disabledInputSlotTypes.Exists(type => slot.GetType().IsEquivalentTo(type) || slot.GetType().IsSubclassOf(type)) ;
        }
    }

    public class GraphPreviewer : VisualElement
    {
        [Obsolete("Please use the custom version of the shader previewer")]
        private MasterPreviewView legacyMaterialPreviewRender;
        [Obsolete("Please use the custom version of the shader previewer")]
        private PreviewManager legacyPreviewManager;


        private GraphData graph;
        private EditorWindow previewWindow;
        private bool previewExists = false;

        private ShaderPreviewManager previewManager;
        private MasterShaderPreview materialPreviewRender;
        private bool legacy = false;



        
        public Vector2 sizePercentage = new Vector2(0.75f, 0.75f);
        private Vector2 currentSize;

        [Obsolete("This is used for the built-in previewer from the shadergraph package")]
        internal GraphPreviewer(PreviewManager legacyPrevManager, GraphData graph)
        {
            legacyPreviewManager = legacyPrevManager;
            this.graph = graph;
            legacyPreviewManager.RenderPreviews(false);
            legacyMaterialPreviewRender = new MasterPreviewView(legacyPreviewManager, graph) { name = "Master Preview" };
            var masterPreviewViewDraggable = new ShaderPreviewWindow(null, this);
            legacyMaterialPreviewRender.AddManipulator(masterPreviewViewDraggable);
            legacyMaterialPreviewRender.previewResizeBorderFrame.maintainAspectRatio = false;
            legacyMaterialPreviewRender.previewResizeBorderFrame.OnResizeFinished += DebugPrintSize;
            legacy = true;
        }

        internal GraphPreviewer(ShaderPreviewManager prevManager,GraphData graph)
        {
            previewManager = prevManager;
            this.graph = graph;
            previewManager.RenderPreviews(false);
            materialPreviewRender = new MasterShaderPreview(previewManager, graph) { name = "Master Preview" };
            var masterPreviewViewDraggable = new ShaderPreviewWindow(null, this);
            materialPreviewRender.AddManipulator(masterPreviewViewDraggable);
            materialPreviewRender.previewResizeBorderFrame.maintainAspectRatio = false;
            materialPreviewRender.previewResizeBorderFrame.OnResizeFinished += DebugPrintSize;
        }

        public void DebugPrintSize()
        {
            Debug.Log("Is active");
            if (legacy)
            {
                Debug.LogWarning("Legacy Version, check this line to uncomment old code");
                /*
                Debug.LogWarningFormat(legacyMaterialPreviewRender.previewResizeBorderFrame.style.width.ToString());
                Debug.LogWarningFormat(legacyMaterialPreviewRender.previewResizeBorderFrame.style.height.ToString());
                */
            }
            else
            {
                Debug.LogWarningFormat(materialPreviewRender.previewResizeBorderFrame.style.width.ToString());
                Debug.LogWarningFormat(materialPreviewRender.previewResizeBorderFrame.style.height.ToString());
            }
        }

        [System.Obsolete("This uses the built-in shader previewer from the shadergraph package, please use the custom version")]
        public void addLegacyMasterPreview(EditorWindow toWindow)
        {
            Debug.LogWarning("Adding preview to loaded graph");

            // Unused resizing of shader preview visual element
            {/*
            style.left = 0f;
            style.right = float.NaN;
            style.top = 0f;
            style.bottom = float.NaN;
            
            style.maxWidth = toWindow.position.size.x;
            style.maxHeight = toWindow.position.size.y;
            style.height = toWindow.position.size.y * sizePercentage.y;
            style.width = toWindow.position.size.x * sizePercentage.x;
            */
            }

            legacyMaterialPreviewRender.style.right = float.NaN;
            legacyMaterialPreviewRender.style.left = 0f;
            legacyMaterialPreviewRender.style.bottom = float.NaN;
            legacyMaterialPreviewRender.style.top = 0f;
            legacyMaterialPreviewRender.style.maxWidth = toWindow.position.size.x;
            legacyMaterialPreviewRender.style.maxHeight = toWindow.position.size.y;
            legacyMaterialPreviewRender.style.height = toWindow.position.size.y * sizePercentage.y;
            legacyMaterialPreviewRender.style.width = toWindow.position.size.x * sizePercentage.x;

            toWindow.rootVisualElement.Add(legacyMaterialPreviewRender);
            //toWindow.rootVisualElement.style.backgroundColor =new Color(1f,0f,0f,0.2f);
            legacyPreviewManager.RenderPreviews(true);
            currentSize = toWindow.position.size * sizePercentage;
            legacyPreviewManager.ResizeMasterPreview(currentSize * 2f);

            previewWindow = toWindow;
            previewExists = true;
            // Resizing
            /*
            previewManager.ResizeMasterPreview(new Vector2(5f, 5f));
            materialPreviewRender.StretchToParentSize();
            materialPreviewRender.previewResizeBorderFrame.style.height = 10f;
            materialPreviewRender.previewResizeBorderFrame.style.minHeight = 10f;
            materialPreviewRender.previewTextureView.style.width = 5f;
            materialPreviewRender.previewTextureView.style.height = 5f;
            materialPreviewRender.preview.StretchToParentSize();
            //previewManager.ResizeMasterPreview(toWindow.maxSize);
            toWindow.Repaint();*/
        }

        public void addMasterPreview(EditorWindow toWindow)
        {
            materialPreviewRender.style.right = float.NaN;
            materialPreviewRender.style.left = 0f;
            materialPreviewRender.style.bottom = float.NaN;
            materialPreviewRender.style.top = 0f;
            materialPreviewRender.style.maxWidth = toWindow.position.size.x;
            materialPreviewRender.style.maxHeight = toWindow.position.size.y;
            materialPreviewRender.style.height = toWindow.position.size.y * sizePercentage.y;
            materialPreviewRender.style.width = toWindow.position.size.x * sizePercentage.x;

            toWindow.rootVisualElement.Add(materialPreviewRender);
            //toWindow.rootVisualElement.style.backgroundColor = new Color(1f, 0f, 0f, 0.2f);
            previewManager.RenderPreviews(true);
            currentSize = toWindow.position.size * sizePercentage;
            previewManager.ResizeMasterPreview(currentSize * 2f);

            previewWindow = toWindow;
            previewExists = true;
        }

        public bool update(EditorWindow toWindow)
        {
            if( (!previewExists && previewWindow == null) ||
                (previewManager == null && legacyPreviewManager == null) ||
                (materialPreviewRender == null && legacyMaterialPreviewRender == null) )
            {
                return true;
            }
            //previewManager.UpdateMasterPreview(ModificationScope.Topological);
            //previewManager.HandleGraphChanges();
            if (legacy)
            {
                Debug.LogWarning("You are running a legacy version of the code, uncomment GraphObject.cs(919-926) if you wish to proceed");
                /*
                if (toWindow.position.size.sqrMagnitude != currentSize.sqrMagnitude)
                {
                    currentSize = toWindow.position.size * sizePercentage;
                    legacyPreviewManager.ResizeMasterPreview(currentSize * 2f);

                }
                legacyPreviewManager.RenderPreviews(true);
                */
            }
            else
            {
                if (toWindow.position.size.sqrMagnitude != currentSize.sqrMagnitude)
                {
                    currentSize = toWindow.position.size * sizePercentage;
                    previewManager.ResizeMasterPreview(currentSize * 2f);

                }
                previewManager.RenderPreviews(true);
            }
            toWindow.Repaint();
            if ((previewExists && previewWindow == null) || toWindow == null)
            {
                Debug.Log("No window, destroying previews");
                previewExists = false;
                destroyRenderingView();
                return true;
            }
            return false;
        }

        public void destroyRenderingView()
        {
            if (!legacy) destroyPreview();
            else
            {
                Debug.LogWarning("If you wish to use the legacy code, uncomment line 956 in GraphObject.cs");
                //destroyLegacyPreview();
            }
        }

        [Obsolete("Old destroy function, this should not be called if not using legacy previewer",false)]
        private void destroyLegacyPreview()
        {
            Debug.Log("Destroying preview");
            try
            {
                legacyMaterialPreviewRender.Clear();
                legacyPreviewManager.Dispose();
                legacyMaterialPreviewRender = null;
                legacyPreviewManager = null;
            }
            catch (Exception e){

                Debug.LogError(e.Message);
            }

        }
        private void destroyPreview()
        {
            Debug.Log("Destroying preview");
            try
            {
                materialPreviewRender.Clear();
                previewManager.Dispose();
                materialPreviewRender = null;
                previewManager = null;
            }
            catch (Exception e)
            {

                Debug.LogError(e.Message);
            }
        }

    }


    public enum GraphType
    {
        Blank,
        Lit,
        Unlit,
        Unknown
    }


    public class GraphManager : IDisposable
    {
        private string assetGuid;
        private bool is_SubGraph;

        private static GraphType type;

        private GraphObject graphObject;
        private ColorSpace ColorSpace;
        private RenderPipelineAsset graphicSettings;

        private List<Type> nodeTypes;

        private MessageManager messageManager;
        private PreviewManager legacyPreviewManager;
        private MasterPreviewView legacyGraphPreview;

        private MasterShaderPreview graphPreview;
        private ShaderPreviewManager previewManager;

        private GraphPreviewer graphPreviewer;
        public int stageNumber
        {
            get { return previewManager!=null ? previewManager.activeStage : -1; }
            set
            {
                if(previewManager != null) previewManager.activeStage = value;
            }
        }

        private List<AbstractMaterialNode> inputNodes;

        public void Dispose()
        {
            graphPreviewer?.destroyRenderingView();
            previewManager = null;
            graphPreview = null;
            if(legacyPreviewManager!=null || legacyGraphPreview != null)
            {
                Debug.LogWarning("You are using an old version of the shader previewer");
                legacyPreviewManager = null;
                legacyGraphPreview = null;
            }

        }

        public GeneticHandler randomRanges;

        public GraphManager(string pathOrGuid, bool isGUID, GeneticHandler genes, GraphType type)
        {
            randomRanges = genes;
            Initialize(pathOrGuid, isGUID, type);
        }

        public GraphManager(string pathOrGuid, bool isGUID, GraphType type)
        {
            Initialize(pathOrGuid, isGUID, type);

        }


        internal List<T> getNodes<T>() where T : AbstractMaterialNode
        {
            List<T> foundNodes = new List<T>(graphObject.graph.GetNodes<T>());
            return foundNodes;
        }

        internal IEdge getInputEdge(SlotReference slotReference)
        {
            if (slotReference.node.owner != graphObject.graph || !slotReference.slot.isConnected) return null;
            foreach(IEdge edge in graphObject.graph.GetEdges(slotReference))
            {
                return edge;
            }
            throw new Exception("Slot results connected but no edge was found");
        }


        internal List<IEdge> getOutputEdges(SlotReference slotReference)
        {

            if (slotReference.node.owner != graphObject.graph || !slotReference.slot.isConnected)
            {
                finalizeChanges();
                throw new ArgumentException(string.Format("Given Slot Reference {0} in {1} is not connected or the graph has wrong owner", slotReference.slot.displayName, AssetDatabase.GUIDToAssetPath(slotReference.node.owner.assetGuid)));
            
            }
            return new List<IEdge>(graphObject.graph.GetEdges(slotReference)); 
        }

        internal GraphData getGraph()
        {
            return graphObject.graph;
        }

        public void setGenes(GeneticHandler genes)
        {
            if (type != genes.graphType) Debug.LogErrorFormat("Graph type is {0} but genes have {1} in {2}",type,genes.graphType,AssetDatabase.GUIDToAssetPath(getGraph().assetGuid));
            randomRanges = genes;
        }

        public void initGenes(MutationStrength strength, bool allowExpansion, bool allowTypeChange, float expansionProbability, float typeChangeProbability)
        {
            randomRanges = new GeneticHandler(allowExpansion,allowTypeChange,expansionProbability,typeChangeProbability) ;
            randomRanges.mutationStrength = strength;
            randomRanges.graphType = type;
        }

        public GraphType getGraphType()
        {
            return type;
        }

        public GeneticHandler getGenes()
        {
            if (randomRanges != null)
            {
                return randomRanges;
            }
            throw new NullReferenceException("Genes have not been initialized");
        }

        private void Initialize(string pathOrGuid, bool isGUID, GraphType graphType)
        {
            string guid = pathOrGuid;
            string path = pathOrGuid;
            type = graphType;
            if (!isGUID)
            {
                guid = AssetDatabase.AssetPathToGUID(pathOrGuid);
                assetGuid = guid;
            }
            else
            {
                path = AssetDatabase.GUIDToAssetPath(pathOrGuid);
                assetGuid = pathOrGuid;
            }
            ColorSpace = PlayerSettings.colorSpace;
            graphicSettings = GraphicsSettings.renderPipelineAsset;
            string readJson = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
            messageManager = new MessageManager();

            graphObject = ScriptableObject.CreateInstance<GraphObject>();
            graphObject.hideFlags = HideFlags.HideAndDontSave;
            graphObject.graph = new GraphData
            {
                assetGuid = guid,
                isSubGraph = checkIfSubgraph(path)
            };
            MultiJson.Deserialize(graphObject.graph, readJson);
            graphObject.Validate();


            if (graphType == GraphType.Unknown)
            {
                int numberOfBlockNodes = new List<AbstractMaterialNode>(graphObject.graph.GetNodes<BlockNode>()).Count;
                if (numberOfBlockNodes == 0) type = GraphType.Blank;
                else if (numberOfBlockNodes > 4) type = GraphType.Lit;
                else type = GraphType.Unlit;
            }

            nodeTypes = new List<Type>(NodeClassCache.knownNodeTypes);
            inputNodes = new List<AbstractMaterialNode>();
        }

        private bool checkIfSubgraph(string path)
        {
            string extension = System.IO.Path.GetExtension(path);
            extension = extension.Substring(1).ToLowerInvariant();
            switch (extension)
            {
                case ShaderGraphImporter.Extension:
                    return false;
                case ShaderSubGraphImporter.Extension:
                    return true;
                default:
                    throw new Exception("Extension is wrong");
            }
        }
        public List<string> getNodeList()
        {
            List<string> nodeList = new List<string>();

            IEnumerable<AbstractMaterialNode> nodeTypeList = graphObject.graph.GetNodes<AbstractMaterialNode>();
            foreach (AbstractMaterialNode n in nodeTypeList)
            {
                nodeList.Add(n.GetType().Name + " " + n.name);
            }
            return nodeList;
        }

        [System.Obsolete("This uses the built-in shader preview from the ShaderGraph package, please use the custom version.")]
        public void addLegacyShaderPreview(EditorWindow toWindow)
        {
            legacyPreviewManager = new PreviewManager(graphObject.graph, messageManager);
            legacyGraphPreview = new MasterPreviewView(legacyPreviewManager, graphObject.graph);

            if (graphPreviewer != null) graphPreviewer.destroyRenderingView();
            graphPreviewer = new GraphPreviewer(legacyPreviewManager, graphObject.graph);
            graphObject.graph.ValidateGraph();
            graphPreviewer.addLegacyMasterPreview(toWindow);
        }

        public void addShaderPreview(EditorWindow toWindow)
        {
            previewManager = new ShaderPreviewManager(graphObject.graph, messageManager);
            graphPreview = new MasterShaderPreview(previewManager, graphObject.graph);

            if (graphPreviewer != null) graphPreviewer.destroyRenderingView();
            graphPreviewer = new GraphPreviewer(previewManager, graphObject.graph);
            graphObject.graph.ValidateGraph();
            graphPreviewer.addMasterPreview(toWindow);
        }

        public void updateShaderPreview(EditorWindow toWindow)
        {
            bool? destroyPreview = false;
            destroyPreview = graphPreviewer?.update(toWindow);
            if (destroyPreview == true)
            {
                graphPreviewer.Clear();
                graphPreviewer.SetEnabled(false);
                graphPreviewer.RemoveFromHierarchy();
                graphPreviewer = null;
            }
        }

        public void updateGraph()
        {
            string readJson = File.ReadAllText(AssetDatabase.GUIDToAssetPath(assetGuid), System.Text.Encoding.UTF8);
            MultiJson.Deserialize(graphObject.graph, readJson);
        }

        [System.Obsolete("Please use addRandomCompatibleNode")]
        public void AddRandomNode()
        {
            updateGraph();
            GraphData graph = graphObject.graph;
            Type n = RandomizationHelper.chooseRandomNodeType();
            while (DeniedOperationsChecker.isNotValidNodeType(n))
            {
                n = RandomizationHelper.chooseRandomNodeType();
            }
            AbstractMaterialNode realNode = (AbstractMaterialNode)Activator.CreateInstance(n);
            /*
            if (NodeClassCache.GetAttributeOnNodeType<TitleAttribute>(n) == null)
            {
                //Debug.LogErrorFormat("No Title Attribute found on {0}",n.Name);
                return;
            }
            //Debug.Log(string.Format("Added {0}", realNode.name));
            */
            graph.AddNode(realNode);
            FileUtilities.WriteShaderGraphToDisk(AssetDatabase.GUIDToAssetPath(assetGuid), graph);
            AssetDatabase.Refresh();
            return;
        }

        public void addRandomCompatibleNode()
        {
            updateGraph();
            GraphData graph = graphObject.graph;
            try
            {
                graph = addRandomCompatibleNodeNoSave();
            }
            catch (Exception e)
            {
                Debug.LogError("Error generating node");
                Debug.LogError(e.Message);
            }
            FileUtilities.WriteShaderGraphToDisk(AssetDatabase.GUIDToAssetPath(assetGuid), graph);
            AssetDatabase.Refresh();
            return;
        }
        private GraphData addRandomCompatibleNodeNoSave()
        {
            GraphData graph = graphObject.graph;
            List<AbstractMaterialNode> nodesInGraph = new List<AbstractMaterialNode>(graph.GetNodes<AbstractMaterialNode>());
            nodesInGraph.RemoveAll(node => inputNodes.Contains(node));
            AbstractMaterialNode inputNode = RandomizationHelper.chooseRandomInputNode(nodesInGraph);

            //Debug.LogWarningFormat("Chose {0} or {1} as origin in {2}", originNode.name, originNode.GetType().Name ,assetGuid.Substring(0, 10));
            List<Type> compatibleNodeTypes = new List<Type>();
            compatibleNodeTypes.AddRange(nodeTypes.FindAll(type => checkCompatible(type, inputNode, true) && !checkIndirectDisabled(type, inputNode, true)));
            compatibleNodeTypes.TrimExcess();
            if (compatibleNodeTypes.Count <= 0)
            {
                return graph;
            }
            //Type n = compatibleNodeTypes[rand.Next(compatibleNodeTypes.Count)];
            Type n = RandomizationHelper.chooseRandomOutputNode(inputNode, compatibleNodeTypes, this);
            /*
            while (DeniedOperationsChecker.isNotValidNodeType(n) || DeniedOperationsChecker.isLinkDisabled(n, inputNode.GetType()) || DeniedOperationsChecker.isLinkDisabled(n, inputNode.name))
            {
                //Debug.Log("Regenerating");
                n = compatibleNodeTypes[rand.Next(compatibleNodeTypes.Count)];
            }
            if (NodeClassCache.GetAttributeOnNodeType<TitleAttribute>(n) == null)
            {
                Debug.LogErrorFormat("No Title Attribute found on {0}, it probably should not be used. Aborting addition of node", n.Name);
                return graph;
            }
            */
            AbstractMaterialNode outputNode = (AbstractMaterialNode)Activator.CreateInstance(n);
            outputNode.previewExpanded = false;
            /*Debug.LogWarningFormat("Added class should be {0} ", realNode.GetType().Name);
            Debug.LogWarningFormat("Original type should be {0} ", n.Name);
            Debug.LogFormat("Adding {0} to {1} in {2}", realNode.name, originNode.name, assetGuid.Substring(0, 10));
            */
            graph.AddNode(outputNode);
            List<MaterialSlot> inputSlots = new List<MaterialSlot>(inputNode.GetInputSlots<MaterialSlot>());
            inputSlots.RemoveAll(slot => getCompatibleSlots(outputNode, slot).Count == 0);
            if (inputSlots.Count == 0)
            {
                Debug.LogError("No slots left that are compatible. This should not happen");
                Debug.LogError("Input Slots");
                foreach (MaterialSlot slot in inputNode.GetInputSlots<MaterialSlot>())
                {
                    Debug.LogErrorFormat("{0} : {1}", slot.displayName, slot.GetType().Name);
                }
                Debug.LogError("Output Slots");
                foreach (MaterialSlot slot in outputNode.GetOutputSlots<MaterialSlot>())
                {
                    Debug.LogErrorFormat("{0} : {1}", slot.displayName, slot.GetType().Name);
                }
                return graph;
            }
            MaterialSlot chosenInputSlot = RandomizationHelper.chooseRandomSlot(inputSlots);
            List<MaterialSlot> compatibleOutputSlots = new List<MaterialSlot>(getCompatibleSlots(outputNode, chosenInputSlot));
            if (compatibleOutputSlots.Count <= 0)
            {
                Debug.LogErrorFormat("No compatible slots when adding {1}, this should not happen. Refer to {0}", AssetDatabase.GUIDToAssetPath(assetGuid), outputNode.name);
                //graph.RemoveNode(realNode);
                return graph;
            }
            if (chosenInputSlot.isConnected) deleteEdgeAndOrphans(chosenInputSlot);
            graph.Connect(RandomizationHelper.chooseRandomSlot(compatibleOutputSlots).slotReference, chosenInputSlot.slotReference);
            return graph;
        }

        private int evolveGraph()
        {
            GraphData graph = graphObject.graph;
            List<AbstractMaterialNode> nodesInGraph = new List<AbstractMaterialNode>(graph.GetNodes<AbstractMaterialNode>());
            nodesInGraph.RemoveAll(node => inputNodes.Contains(node));
            AbstractMaterialNode inputNode = RandomizationHelper.chooseRandomInputNode(nodesInGraph);
            MaterialSlot chosenInputSlot = RandomizationHelper.chooseRandomSlot(new List<MaterialSlot>(inputNode.GetInputSlots<MaterialSlot>()));
            int mutationChosen = RandomizationHelper.chooseGraphMutation(chosenInputSlot);
            if (mutationChosen == -1) addRandomCompatibleNode();
            else
            {
                GraphManager evolutionReferenceGraph = RandomizationHelper.referenceGraphs[mutationChosen];
                List<BlockNode> blockNodes = evolutionReferenceGraph.getNodes<BlockNode>(); 
                Dictionary<AbstractMaterialNode, AbstractMaterialNode> referenceToOwnGraph = new Dictionary<AbstractMaterialNode, AbstractMaterialNode>();
                bool evolutionSuccesful = false;
                foreach (BlockNode referenceBlockNode in blockNodes)
                {
                    if(referenceBlockNode.GetSlotReference(0).slot.GetType() == chosenInputSlot.GetType())
                    {
                        try
                        {
                            List<AbstractMaterialNode> associatedNodes = inheritInputNode(referenceBlockNode.GetSlotReference(0).slot, chosenInputSlot);
                            referenceToOwnGraph.Add(associatedNodes[0], associatedNodes[1]);
                            inheritNodes(evolutionReferenceGraph.graphObject.graph, associatedNodes[0], associatedNodes[1], referenceToOwnGraph);
                            evolutionSuccesful = true;
                            inheritInputs(evolutionReferenceGraph.graphObject.graph, referenceToOwnGraph);
                        }
                        catch(ArgumentException e)
                        {
                            continue;
                        }
                    }
                }
                if (!evolutionSuccesful) throw new KeyNotFoundException("No connected reference block was found when importing the reference");
                //randomizeGraphInputs();
                Debug.LogWarning(referenceToOwnGraph.Keys.Count);
                Debug.LogWarning(referenceToOwnGraph.Keys.Count - blockNodes.Count);
                return referenceToOwnGraph.Keys.Count - blockNodes.Count;
            }
            return 1;
        }

        public bool mutateGraph(int numberOfMutations)
        {
            Debug.LogFormat("Mutating {0} {1} times", AssetDatabase.GUIDToAssetPath(graphObject.graph.assetGuid), numberOfMutations);
            bool success = true;
            MutationStrength mutationStrength = MutationStrength.High;
            if (randomRanges != null) mutationStrength = randomRanges.mutationStrength;
            for(int i = 0; i < numberOfMutations; i++)
            {
                Debug.LogFormat("Mutation {0}", i);
                MutationHelper.newMutation(this, mutationStrength);
            }
            finalizeChanges();
            return success;
        }


        public bool addMultipleCompatibleNodes(int numberOfAdds)
        {
            bool success = true;
            GraphData graph;
            //DebugFunctions.debugPrintBlockNodes(graph);
            updateGraph();
            int i = 0;
            while (i < numberOfAdds)
            {
                i += evolveGraph();
                /*catch(Exception e) 
                {
                    Debug.LogErrorFormat("Error generating node at iteration {0}", i);
                    throw e;
                    if (!success)
                    {
                        Debug.LogError("Second error!");
                        return success;
                    }
                    success = false;
                }*/
            }
            graph = graphObject.graph;
            //Debug.Log("Validating");
            finalizeChanges();
            return success;
        }

        internal List<AbstractMaterialNode> inheritInputNode(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            if (!parentSlot.isConnected)
            {
                Debug.LogWarningFormat("{0} in {1} in graph {2} is not connected. This should not happen", parentSlot.displayName, parentSlot.owner.name,AssetDatabase.GUIDToAssetPath(parentSlot.owner.owner.assetGuid));
                throw new ArgumentException(string.Format("{0} in {1} in graph {2} is not connected. This should not happen", parentSlot.displayName, parentSlot.owner.name, AssetDatabase.GUIDToAssetPath(parentSlot.owner.owner.assetGuid)));
            }
            IEdge inputEdge = new List<IEdge>(parentSlot.owner.owner.GetEdges(parentSlot.slotReference))[0];
            AbstractMaterialNode parentOutputNode = inputEdge.outputSlot.node;
            SlotReference parentOutputSlot = inputEdge.outputSlot;

            AbstractMaterialNode childOutputNode = (AbstractMaterialNode)Activator.CreateInstance(parentOutputNode.GetType());
            DrawState childState = new DrawState();
            childState.expanded = parentOutputNode.drawState.expanded;
            childState.position = parentOutputNode.drawState.position;
            childOutputNode.drawState = childState;
            GraphData graph = childSlot.owner.owner;
            graph.AddNode(childOutputNode);
            graph.Connect(childOutputNode.GetSlotReference(parentOutputSlot.slotId), childSlot.slotReference);
            return new List<AbstractMaterialNode>() { parentOutputNode, childOutputNode };
        }

        public void inheritFromGraph(GraphManager parent)
        {
            List<BlockNode> blockNodeList = new List<BlockNode>(graphObject.graph.GetNodes<BlockNode>());
            GraphData parentGraph = parent.graphObject.graph;
            Dictionary<AbstractMaterialNode, AbstractMaterialNode> parentChildCopy = new Dictionary<AbstractMaterialNode, AbstractMaterialNode>();
            //Debug.LogFormat("There are {0} blocknodes in the graph", new List<BlockNode>(graphObject.graph.GetNodes<BlockNode>()).Count);
            foreach (var blockNode in blockNodeList)
            {
                BlockNode parentBlockNode = ShaderGraphUtilities.tryGetBlockNode(parentGraph, blockNode.name);
                //Debug.LogFormat("Inheriting from {0} to {1}, node is {2}, check {3} ", AssetDatabase.GUIDToAssetPath(parentGraph.assetGuid), AssetDatabase.GUIDToAssetPath(assetGuid), blockNode.name, parentBlockNode.name);
                if (parentBlockNode != null)
                {
                    parentChildCopy.Add(parentBlockNode, blockNode);
                    //Debug.Log("Inheriting Nodes");
                    inheritNodes(parentGraph, parentBlockNode, blockNode, parentChildCopy);
                }
                else
                {
                    Debug.LogErrorFormat("Error, no {0} blocknode in parentgraph {1} was found", blockNode.name,AssetDatabase.GUIDToAssetPath(parentGraph.assetGuid));
                }
            }
            foreach(AbstractMaterialNode parentInputNode in parent.inputNodes)
            {
                AbstractMaterialNode childInputNode;
                if(parentChildCopy.TryGetValue(parentInputNode, out childInputNode))
                    inputNodes.Add(childInputNode);
                else
                {
                    Debug.LogError("Input node not mapped");
                }
            }
            //Debug.Log("Inheriting Inputs");
            inheritInputs(parentGraph, parentChildCopy);
            /*
            foreach(var node in graphObject.graph.GetNodes<AbstractMaterialNode>())
            {
                Debug.LogFormat("{0} is in resulting graph", node.name);
            }
            foreach(var key in parentChildCopy.Keys)
            {
                Debug.LogFormat("{0} : {1}",key, parentChildCopy[key]);
            }
            */

            try
            {
                randomRanges.inheritRanges(parentChildCopy, parent.getGenes());
            }
            catch(NullReferenceException e)
            {
                if(!e.Message.Contains("Genes have not been initialized"))
                {
                    throw e;
                }
            }

            finalizeChanges();
        }

        // Inheriting From 2 graphs performs single point crossover over the block nodes
        public void inheritFromGraphs(GraphManager parent1, GraphManager parent2)
        {
            List<BlockNode> parent1BlockNodes = new List<BlockNode>(parent1.graphObject.graph.GetNodes<BlockNode>());
            GraphData parent1Graph = parent1.graphObject.graph;
            List<BlockNode> parent2BlockNodes = new List<BlockNode>(parent2.graphObject.graph.GetNodes<BlockNode>());
            GraphData parent2Graph = parent2.graphObject.graph;
            Dictionary<AbstractMaterialNode, AbstractMaterialNode> parentChildCopy = new Dictionary<AbstractMaterialNode, AbstractMaterialNode>();
            int blockNodeNumber = new List<BlockNode>(graphObject.graph.GetNodes<BlockNode>()).Count,
                crossOverPoint = UnityEngine.Random.Range(0, blockNodeNumber),
                geneIndex = 0;

            // We cast to list because modifying the graph causes the block node list to be set as dirty even if the number of block nodes does not change
            List<BlockNode> childBlockNodes =new List<BlockNode>(graphObject.graph.GetNodes<BlockNode>());
            foreach (var blockNode in childBlockNodes)
            {
                GraphData chosenGraph = geneIndex < crossOverPoint ? parent1Graph : parent2Graph;
                BlockNode parentBlockNode = ShaderGraphUtilities.tryGetBlockNode(chosenGraph, blockNode.name);
                if (parentBlockNode != null)
                {
                    parentChildCopy.Add(parentBlockNode, blockNode);
                    inheritNodes(chosenGraph, parentBlockNode, blockNode, parentChildCopy);
                }
                geneIndex++;
            }
            foreach (AbstractMaterialNode parentInputNode in parent1.inputNodes)
            {
                if (parentChildCopy.ContainsKey(parentInputNode))
                    inputNodes.Add(parentChildCopy[parentInputNode]);
            }
            foreach (AbstractMaterialNode parentInputNode in parent2.inputNodes)
            {
                if (parentChildCopy.ContainsKey(parentInputNode))
                    inputNodes.Add(parentChildCopy[parentInputNode]);
            }
            inheritInputs(parent1Graph,parent2Graph, parentChildCopy);
            finalizeChanges();
        }

        internal void inheritInputs(GraphData parentGraph, Dictionary<AbstractMaterialNode, AbstractMaterialNode> parentToChild)
        {
            List<MaterialSlot> freeParentInputs = new List<MaterialSlot>();
            foreach (var node in parentGraph.GetNodes<AbstractMaterialNode>())
            {
                foreach (var slot in node.GetInputSlots<MaterialSlot>())
                {
                    if (!slot.isConnected) freeParentInputs.Add(slot);
                }
            }
            foreach(ColorNode parentColorNode in parentGraph.GetNodes<ColorNode>())
            {
                AbstractMaterialNode childNode;
                if(parentToChild.TryGetValue(parentColorNode,out childNode))
                {
                    ColorNode childColorNode = (ColorNode)childNode;
                    childColorNode.color = parentColorNode.color;
                }
            }
            foreach(RandomizableVector1Node parentRandFloatNode in parentGraph.GetNodes<RandomizableVector1Node>())
            {
                AbstractMaterialNode childNode;
                if (parentToChild.TryGetValue(parentRandFloatNode, out childNode))
                {
                    RandomizableVector1Node floatChildNode = (RandomizableVector1Node)childNode;
                    Vector1MaterialSlot childInputSlot = (Vector1MaterialSlot)floatChildNode.GetSlotReference(0).slot;
                    Vector1MaterialSlot parentInputSlot = (Vector1MaterialSlot)parentRandFloatNode.GetSlotReference(0).slot;
                    childInputSlot.value = parentInputSlot.value * 2.0f;

                }
            }
            foreach (var freeParentSlot in freeParentInputs)
            {
                AbstractMaterialNode childCopyNode;
                if (parentToChild.TryGetValue(freeParentSlot.owner, out childCopyNode))
                {
                    MaterialSlot freeChildSlot = childCopyNode.GetSlotReference(freeParentSlot.id).slot;
                    try
                    {
                        freeChildSlot.CopyValuesFrom(freeParentSlot);
                    }
                    catch
                    {
                        Debug.LogErrorFormat("Error copying input values at node {0} in {1}", childCopyNode.name, AssetDatabase.GUIDToAssetPath(assetGuid));
                    }
                    /*
                    foreach (var type in transferableSlotTypes.Keys)
                    {
                        //Debug.LogFormat("Testing for {0}", type.Name);
                        if (type.IsEquivalentTo(freeParentSlot.GetType()) || freeParentSlot.GetType().IsSubclassOf(type))
                        {
                            
                            
                            Func<MaterialSlot, MaterialSlot, bool> chosenFunction;
                            if (transferableSlotTypes.TryGetValue(type, out chosenFunction))
                            {
                                if (!chosenFunction.Invoke(freeParentSlot, freeChildSlot))
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    Debug.LogWarningFormat("Could not transfer from parent to child  {1} on slot {0}", freeChildSlot.displayName, path);
                                }
                            }
                            else
                            {
                                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                Debug.LogWarningFormat("Could not find any function for {0} on graph {1}", freeChildSlot.displayName, path);
                            }
                        }
                    }
                    */
                }
                else
                {
                    /*
                    if (freeParentSlot.owner.GetType().IsEquivalentTo(typeof(BlockNode))){
                        string parentBlockNodeName = freeParentSlot.owner.name;
                        List<BlockNode> blockNodes = new List<BlockNode>(graphObject.graph.GetNodes<BlockNode>());
                        if (!blockNodes.Exists(node => node.name == parentBlockNodeName)) continue ;
                    }
                    foreach(var key in parentToChild.Keys)
                    {
                        Debug.LogErrorFormat("{0} : {1}",key, parentToChild[key]);
                    }
                    throw new KeyNotFoundException(string.Format("Slot {0} of node {2} in graph {1} is not mapped to child", freeParentSlot.displayName, AssetDatabase.GUIDToAssetPath(parentGraph.assetGuid), freeParentSlot.owner.name));
                    */
                }
            }
        }

        private void inheritInputs(GraphData parent1Graph, GraphData parent2Graph, Dictionary<AbstractMaterialNode, AbstractMaterialNode> parentToChild)
        {
            List<MaterialSlot> freeParent1Inputs = new List<MaterialSlot>();
            foreach (var node in parent1Graph.GetNodes<AbstractMaterialNode>())
            {
                foreach (var slot in node.GetInputSlots<MaterialSlot>())
                {
                    if (!slot.isConnected) freeParent1Inputs.Add(slot);
                }
            }
            List<MaterialSlot> freeParent2Inputs = new List<MaterialSlot>();
            foreach (var node in parent2Graph.GetNodes<AbstractMaterialNode>())
            {
                foreach (var slot in node.GetInputSlots<MaterialSlot>())
                {
                    if (!slot.isConnected) freeParent2Inputs.Add(slot);
                }
            }
            foreach (var freeParentSlot in freeParent1Inputs)
            {
                AbstractMaterialNode childCopyNode;
                if (parentToChild.TryGetValue(freeParentSlot.owner, out childCopyNode))
                {
                    MaterialSlot freeChildSlot = childCopyNode.GetSlotReference(freeParentSlot.id).slot;
                    try
                    {
                        freeChildSlot.CopyValuesFrom(freeParentSlot);
                    }
                    catch
                    {
                        Debug.LogErrorFormat("Error copying input values at node {0} in {1}", childCopyNode.name, AssetDatabase.GUIDToAssetPath(assetGuid));
                    }
                    #region Old Transfering method, Obsolete
                    /*
                    foreach (var type in transferableSlotTypes.Keys)
                    {
                        //Debug.LogFormat("Testing for {0}", type.Name);
                        if (type.IsEquivalentTo(freeParentSlot.GetType()) || freeParentSlot.GetType().IsSubclassOf(type))
                        {
                            Func<MaterialSlot, MaterialSlot, bool> chosenFunction;
                            if (transferableSlotTypes.TryGetValue(type, out chosenFunction))
                            {
                                if (!chosenFunction.Invoke(freeParentSlot, freeChildSlot))
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    Debug.LogWarningFormat("Could not transfer from parent to child  {1} on slot {0}", freeChildSlot.displayName, path);
                                }
                            }
                            else
                            {
                                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                Debug.LogWarningFormat("Could not find any function for {0} on graph {1}", freeChildSlot.displayName, path);
                            }
                        }
                    }
                    */
                    #endregion
                }
                else
                {
                    continue;
                    //throw new KeyNotFoundException(string.Format("Slot {0} of node {2} in graph {1} is not mapped to child", freeParentSlot.displayName,AssetDatabase.GUIDToAssetPath(parent1Graph.assetGuid),freeParentSlot.owner.name));
                }
            }
            foreach (var freeParentSlot in freeParent2Inputs)
            {
                AbstractMaterialNode childCopyNode;
                if (parentToChild.TryGetValue(freeParentSlot.owner, out childCopyNode))
                {
                    MaterialSlot freeChildSlot = childCopyNode.GetSlotReference(freeParentSlot.id).slot;
                    try
                    {
                        freeChildSlot.CopyValuesFrom(freeParentSlot);
                    }
                    catch
                    {
                        Debug.LogErrorFormat("Error copying input values at node {0} in {1}", childCopyNode.name, AssetDatabase.GUIDToAssetPath(assetGuid));
                    }
                    #region Old Transfer method, obsolete
                    /*
                    foreach (var type in transferableSlotTypes.Keys)
                    {
                        //Debug.LogFormat("Testing for {0}", type.Name);
                        if (type.IsEquivalentTo(freeParentSlot.GetType()) || freeParentSlot.GetType().IsSubclassOf(type))
                        {
                            Func<MaterialSlot, MaterialSlot, bool> chosenFunction;
                            if (transferableSlotTypes.TryGetValue(type, out chosenFunction))
                            {
                                if (!chosenFunction.Invoke(freeParentSlot, freeChildSlot))
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    Debug.LogWarningFormat("Could not transfer from parent to child  {1} on slot {0}", freeChildSlot.displayName, path);
                                }
                            }
                            else
                            {
                                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                                Debug.LogWarningFormat("Could not find any function for {0} on graph {1}", freeChildSlot.displayName, path);
                            }
                        }
                    }
                    */
                    #endregion
                }
                else
                {
                    continue;
                    //throw new KeyNotFoundException(string.Format("Slot {0} of node {2} in graph {1} is not mapped to child", freeParentSlot.displayName,AssetDatabase.GUIDToAssetPath(parent1Graph.assetGuid),freeParentSlot.owner.name));
                }
            }
        }
        internal void inheritNodes(GraphData parentGraph,AbstractMaterialNode parentNode,AbstractMaterialNode currentNode, Dictionary<AbstractMaterialNode,AbstractMaterialNode> parentToChild)
        {
            //Debug.LogFormat("There are {0} from parent {1}", new List<MaterialSlot>(parentNode.GetInputSlots<MaterialSlot>()).Count,parentNode.name);
            foreach(var parentInputSlot in parentNode.GetInputSlots<MaterialSlot>())
            {
                if (!parentInputSlot.isConnected) continue;
                //Debug.LogFormat("{0} in {1} is connected", parentInputSlot.displayName, parentInputSlot.owner.name);
                List<IEdge> inputEdges = new List<IEdge>();
                parentGraph.GetEdges(parentInputSlot.slotReference, inputEdges);
                //Debug.LogFormat("{0} input edges", inputEdges.Count);
                foreach(var edge in inputEdges)
                {
                    AbstractMaterialNode parentOutputNode = edge.outputSlot.node, 
                        childOutputCopy; ;

                    int inputId = parentInputSlot.id,
                        outputId = edge.outputSlot.slotId;

                    SlotReference inputSlotRef = currentNode.GetSlotReference(inputId),
                        outputSlotRef;
                    if (parentToChild.TryGetValue(parentOutputNode,out childOutputCopy))
                    {
                        //Debug.LogFormat("{0} key is present, points to {1}", parentOutputNode.name, childOutputCopy.name);
                        outputSlotRef = childOutputCopy.GetSlotReference(outputId);
                        graphObject.graph.Connect(outputSlotRef, inputSlotRef);
                    }
                    else
                    {
                        Type nodeType = parentOutputNode.GetType();
                        childOutputCopy = (AbstractMaterialNode)Activator.CreateInstance(nodeType);
                        childOutputCopy.drawState = parentOutputNode.drawState; 
                        //Debug.LogFormat("{0} not present, adding {1}", parentOutputNode.name, childOutputCopy.name);
                        parentToChild.Add(parentOutputNode, childOutputCopy);
                        graphObject.graph.AddNode(childOutputCopy);
                        childOutputCopy.previewExpanded = false;
                        outputSlotRef = childOutputCopy.GetSlotReference(outputId);
                        graphObject.graph.Connect(outputSlotRef, inputSlotRef);
                        //Debug.LogFormat("{0} connected to {1}", outputSlotRef.node.name, inputSlotRef.node.name);
                        inheritNodes(parentGraph,parentOutputNode,childOutputCopy,parentToChild);
                    }
                }
            }
        }

        public void generateRandomInputs()
        {

            MutationHelper.changeFloatValues(this);
            MutationHelper.changeColors(this);
            finalizeChanges();
            //updateGraph();

        }

        [Obsolete("Randomize inputs function does not take into account node heuristic, please use generateRandomInputs instead")]
        public void randomizeGraphInputs()
        {
            GraphData graph = graphObject.graph;
            List<MaterialSlot> freeInputSlots = new List<MaterialSlot>();
            foreach(var node in graph.GetNodes<AbstractMaterialNode>())
            {
                foreach(var slot in node.GetInputSlots<MaterialSlot>())
                {
                    if (!slot.isConnected)
                    {
                        freeInputSlots.Add(slot);
                        //Debug.Log("Adding slot to list of free input slots");
                    }
                }
            }
            foreach(var freeSlot in freeInputSlots)
            {
                if (DeniedOperationsChecker.isRandomizationDisabled(freeSlot)) continue;
                if (!randomizeSlotValue(freeSlot))
                {
                    Debug.LogErrorFormat("SlotType parameter is {0}",freeSlot.slotType.ToString());
                    Debug.LogErrorFormat("Class Type is {0}", freeSlot.GetType().FullName);
                    Debug.LogErrorFormat("Base type is : {0} ", freeSlot.GetType().BaseType.FullName);
                    Debug.LogErrorFormat("Inputs to node {0} of type {1}", freeSlot.owner.name, freeSlot.owner.GetType().Name);
                }
                else
                {
                    finalizeChanges();
                }
            }
            

        }

        private void deleteEdgeAndOrphans(MaterialSlot removedSlot)
        {

            propagateDelete(removedSlot);
            /*
            try
            {
                graphObject.graph.RemoveEdge(new List<IEdge>(graphObject.graph.GetEdges(removedSlot.slotReference))[0]);
            }
            catch(ArgumentOutOfRangeException e)
            {
                Debug.LogError("Error with edges");
            }
            */
        }

        private void propagateDelete(MaterialSlot startSlot)
        {
            if (startSlot.isOutputSlot)
            {
                Debug.LogError("Slot shouldn't be output slot!");
                return;
            }
            GraphData graph = graphObject.graph;
            List<IEdge> edgesToSlot = new List<IEdge>(graph.GetEdges(startSlot.slotReference));
            if(edgesToSlot.Count > 1)
            {
                Debug.LogErrorFormat("Number of edges is {0} when it should be 1", edgesToSlot.Count);
                /*foreach(var edge in edgesToSlot)
                {
                    Debug.LogFormat("{0} goes to {1}", edge.outputSlot.slot.displayName, edge.inputSlot.slot.displayName);
                }*/
            }
            MaterialSlot nextSlot = edgesToSlot[0].outputSlot.slot;
            propagateDelete(nextSlot.owner,startSlot);
            graph.RemoveEdge(edgesToSlot[0]);
        }

        private void propagateDelete(AbstractMaterialNode startNode, MaterialSlot prevSlot)
        {
            if(inputNodes.Remove(startNode)) Debug.Log("Removed node was input node");

            List<MaterialSlot> outSlots = new List<MaterialSlot>();
            startNode.GetOutputSlots(outSlots);
            List<IEdge> outEdges = new List<IEdge>();
            List<IEdge> partEdges = new List<IEdge>();
            foreach(MaterialSlot slot in outSlots)
            {
                graphObject.graph.GetEdges(slot.slotReference,partEdges);
                outEdges.AddRange(partEdges);
            }
            outEdges.RemoveAll(edge => edge.inputSlot.slot == prevSlot);
            if (outEdges.Count > 0)
            {
                //Debug.LogWarning("Completed pruning");
                return;
            }
            else
            {
                //Debug.LogWarningFormat("{0} is to be deleted", startNode.name);
                List<MaterialSlot> inSlots = new List<MaterialSlot>();
                startNode.GetInputSlots(inSlots);
                foreach(var slot in inSlots)
                {
                    if (slot.isConnected)
                    {
                        propagateDelete(slot);
                    }
                }
                graphObject.graph.RemoveNode(startNode);
            }
        }

        private List<MaterialSlot> getCompatibleSlots(AbstractMaterialNode fromNode, MaterialSlot toSlot)
        {
            List<MaterialSlot> slots;
            if (toSlot.isInputSlot)
            {
                slots = new List<MaterialSlot>(fromNode.GetOutputSlots<MaterialSlot>());
                slots.TrimExcess();
            }
            else if (toSlot.isOutputSlot)
            {
                slots = new List<MaterialSlot>(fromNode.GetInputSlots<MaterialSlot>());
                slots.TrimExcess();
            }
            else
            {
                Debug.LogError("Slots are of wrong type");
                throw new Exception();
            }
            foreach(MaterialSlot s in slots)
            {
                if(s.owner == toSlot.owner)
                {
                    Debug.LogError("Not same owner");
                    throw new Exception();
                }
            }
            slots.RemoveAll(slot => !toSlot.IsCompatibleWith(slot));
            slots.TrimExcess();
            return slots;
        }

        private bool checkCompatible(Type nodeType,AbstractMaterialNode refNode,bool refTakesInput)
        {
            foreach(var field in graphObject.graph.blockFieldDescriptors) { 

            }
            AbstractMaterialNode typeInstance = (AbstractMaterialNode) Activator.CreateInstance(nodeType);
            List<MaterialSlot> typeSlots;
            List<MaterialSlot> refSlots;
            if (refTakesInput)
            {
                typeSlots = new List<MaterialSlot>(typeInstance.GetOutputSlots<MaterialSlot>());
                refSlots =  new List<MaterialSlot> (refNode.GetInputSlots<MaterialSlot>());
            }
            else
            {
                typeSlots = new List<MaterialSlot>(typeInstance.GetInputSlots<MaterialSlot>());
                refSlots = new List<MaterialSlot>(refNode.GetOutputSlots<MaterialSlot>());
            }
            typeSlots.RemoveAll(slot => !refSlots.Exists(refSlot => slot.IsCompatibleWith(refSlot)));
            typeSlots.RemoveAll(slot => !refSlots.Exists(refSlot => slot.IsCompatibleStageWith(refSlot)));
            return typeSlots.Count > 0;
        }


        private bool checkIndirectDisabled(Type nodeType, AbstractMaterialNode refNode, bool refTakesInput)
        {
            if (refTakesInput)
            {
                List<AbstractMaterialNode> nodes = new List<AbstractMaterialNode>() { refNode };
                nodes.AddRange(getReachableFrom(refNode));
                foreach (var n in nodes)
                {
                    if(DeniedOperationsChecker.isLinkDisabled(nodeType,n.name) ||
                        DeniedOperationsChecker.isLinkDisabled(nodeType, n.GetType()))
                    {
                        return true;
                    }
                }
                return false;
            }
            else return false;
        }
        internal List<AbstractMaterialNode> getReachableFrom(AbstractMaterialNode startNode)
        {
            List<AbstractMaterialNode> reached = new List<AbstractMaterialNode>();
            reached.Add(startNode);
            GraphData graph = graphObject.graph;
            foreach(var slot in startNode.GetOutputSlots<MaterialSlot>())
            {
                if (slot.isConnected)
                {
                    foreach(var edge in graph.GetEdges(slot.slotReference))
                    {
                        reached.AddRange(getReachableFrom(edge.inputSlot.slot.owner));
                    }
                }
            }
            return reached;
        }
        internal List<T> getReachableFrom<T>(AbstractMaterialNode startNode) where T:AbstractMaterialNode
        {
            List<T> reached = new List<T>();
            GraphData graph = graphObject.graph;

            if (startNode.GetType().IsEquivalentTo(typeof(T))) 
                reached.Add((T)startNode);
            foreach (var slot in startNode.GetOutputSlots<MaterialSlot>())
            {
                if (slot.isConnected)
                {
                    foreach (var edge in graph.GetEdges(slot.slotReference))
                    {
                        reached.AddRange(getReachableFrom<T>(edge.inputSlot.slot.owner));
                    }
                }
            }
            return reached;
        }

        internal List<AbstractMaterialNode> getReachingTo(AbstractMaterialNode endNode)
        {
            List<AbstractMaterialNode> reaching = new List<AbstractMaterialNode>();
            foreach(MaterialSlot inputSlot in endNode.GetInputSlots<MaterialSlot>())
            {
                if (inputSlot.isConnected)
                {
                    foreach (IEdge inputEdge in inputSlot.owner.owner.GetEdges(inputSlot.slotReference))
                    {
                        reaching.Add(inputEdge.outputSlot.slot.owner);
                        reaching.AddRange(getReachingTo(inputEdge.outputSlot.slot.owner));
                    }
                }
            }
            return reaching;
        }

        internal List<T> getReachingTo<T>(AbstractMaterialNode endNode) where T:AbstractMaterialNode
        {
            List<T> reaching = new List<T>();
            foreach (MaterialSlot inputSlot in endNode.GetInputSlots<MaterialSlot>())
            {
                if (inputSlot.isConnected)
                {
                    foreach (IEdge inputEdge in inputSlot.owner.owner.GetEdges(inputSlot.slotReference))
                    {
                        if (inputEdge.outputSlot.slot.owner.GetType().IsEquivalentTo(typeof(T))){
                            reaching.Add((T) inputEdge.outputSlot.slot.owner);
                        }
                        reaching.AddRange(getReachingTo<T>(inputEdge.outputSlot.slot.owner));
                    }
                }
            }
            return reaching;
        }

        private bool isNodeReachable(AbstractMaterialNode from, AbstractMaterialNode to)
        {
            return getReachableFrom(from).Contains(to);
        }


        internal void finalizeChanges()
        {
            GraphData graph = graphObject.graph;
            graph.ValidateGraph();
            FileUtilities.WriteShaderGraphToDisk(AssetDatabase.GUIDToAssetPath(assetGuid), graph);
            AssetDatabase.Refresh();
        }

        // Returns true on successful operation
        private bool randomizeSlotValue(MaterialSlot slot)
        {
            //Debug.LogFormat("Randomizing {0} with type {1}", slot.displayName, slot.GetType());
            foreach(var type in SlotValueManager.randomizableTypes.Keys)
            {
                //Debug.LogFormat("Testing for {0}", type.Name);
                if (type.IsEquivalentTo(slot.GetType()) || slot.GetType().IsSubclassOf(type))
                {
                    Func<MaterialSlot,GraphManager, bool> chosenFunction;
                    if (SlotValueManager.randomizableTypes.TryGetValue(type, out chosenFunction))
                    {
                        if (!chosenFunction.Invoke(slot,this))
                        {
                            Debug.LogWarningFormat("Could not cast the slot {0} using {2} with type {1}", slot.displayName,slot.GetType(), chosenFunction.Method.Name);
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Could not find any function to cast {0} with type {1}", slot.displayName, slot.GetType());
                    }
                }
            }
            Debug.LogErrorFormat("No function suitable to {0} in {1} at {2}",slot.displayName,slot.owner.name,AssetDatabase.GUIDToAssetPath(assetGuid));
            return false;
        }


        internal void AddInputNode(AbstractMaterialNode node)
        {
            inputNodes.Add(node);
        }
    }

    internal static class SlotValueManager
    {
        internal static Dictionary<Type, Func<MaterialSlot, GraphManager, bool>> randomizableTypes = new Dictionary<Type, Func<MaterialSlot, GraphManager, bool>>
        {
                {typeof(BooleanMaterialSlot), randomizeValuesBool   },
                {typeof(GradientInputMaterialSlot), randomizeValuesGrad },
                {typeof(Matrix2MaterialSlot),  randomizeValuesMat2    },
                {typeof(Matrix3MaterialSlot), randomizeValuesMat3},
                {typeof(Matrix4MaterialSlot), randomizeValuesMat4},
                {typeof(RandomizableVector1Node), randomizeValuesVec1},
                {typeof(Vector2MaterialSlot), randomizeValuesVec2},
                {typeof(Vector3MaterialSlot), randomizeValuesVec3},
                {typeof(Vector4MaterialSlot), randomizeValuesVec4},
                {typeof(DynamicVectorMaterialSlot), randomizeValuesDynVec },
                {typeof(DynamicValueMaterialSlot), randomizeValuesDynVal },
                {typeof(DynamicMatrixMaterialSlot), randomizeValuesDynMat },

        };
        [Obsolete("Transfer functions have been superseeded by MaterialSlot.CopyValuesFrom(MaterialSlot slot) , plese use that one instead")]
        internal static Dictionary<Type, Func<MaterialSlot, MaterialSlot, bool>> transferableSlotTypes = new Dictionary<Type, Func<MaterialSlot, MaterialSlot, bool>>
        {
                {typeof(BooleanMaterialSlot), transferValuesBool   },
                {typeof(GradientInputMaterialSlot), transferValuesGrad },
                {typeof(Matrix2MaterialSlot),  transferValuesMat2    },
                {typeof(Matrix3MaterialSlot), transferValuesMat3},
                {typeof(Matrix4MaterialSlot), transferValuesMat4},
                {typeof(Vector1MaterialSlot), transferValuesVec1},
                {typeof(Vector2MaterialSlot), transferValuesVec2},
                {typeof(Vector3MaterialSlot), transferValuesVec3},
                {typeof(Vector4MaterialSlot), transferValuesVec4},
                {typeof(DynamicVectorMaterialSlot), transferValuesDynVec },
                {typeof(DynamicValueMaterialSlot), transferValuesDynVal },
                {typeof(DynamicMatrixMaterialSlot), transferValuesDynMat },

        };

        #region Randomizing Functions
        private static bool randomizeValuesBool(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();
            bool rand;
            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            if (slot.owner is BooleanNode node)
            {
                try
                {
                    if (graphGenes != null) rand = graphGenes.getBool();
                    else rand = new System.Random().NextDouble() > 0.5;
                    node.value = new ToggleData(rand);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    Debug.LogErrorFormat("Type was bool but slot is {0}", slot.GetType().FullName);
                    return false;
                }
            }



            BooleanNode inputNode = (BooleanNode)Activator.CreateInstance(typeof(BooleanNode));
            graph.AddNode(inputNode);
            BooleanMaterialSlot outputSlot = new List<BooleanMaterialSlot>(inputNode.GetOutputSlots<BooleanMaterialSlot>())[0];
            try
            {
                if (graphGenes != null)
                {
                    rand = graphGenes.getBool();
                }
                else rand = new System.Random().NextDouble() > 0.5;
                inputNode.value = new ToggleData(rand);

                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was bool but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }

        private static bool randomizeValuesInt(MaterialSlot slot, GraphManager graphManager)
        {

            GraphData graph = graphManager.getGraph();
            IntegerNode inputNode = (IntegerNode)Activator.CreateInstance(typeof(IntegerNode));
            graph.AddNode(inputNode);
            Vector1MaterialSlot outputSlot = inputNode.GetSlotReference(0).slot as Vector1MaterialSlot;
            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {

                int rand;
                if (graphGenes != null)
                {
                    rand = Mathf.RoundToInt(graphGenes.getFloat());
                }
                else rand = new System.Random().Next() * (new System.Random().NextDouble() > 0.5 ? 1 : -1);
                inputNode.value = rand;

                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;

            }
            catch (Exception e)
            {

                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Integer but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }

        private static bool randomizeValuesGrad(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();
            GradientNode inputNode, node;
            GradientMaterialSlot outputSlot;

            bool generateInputNode = !(slot.owner is GradientNode);

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            if (generateInputNode)
            {
                inputNode = (GradientNode)Activator.CreateInstance(typeof(GradientNode));
                graph.AddNode(inputNode);
                outputSlot = new List<GradientMaterialSlot>(inputNode.GetOutputSlots<GradientMaterialSlot>())[0];
            }
            else
            {
                inputNode = (GradientNode)slot.owner;
                outputSlot = null;
            }
            try
            {
                Color col;
                GradientColorKey[] keys = new GradientColorKey[2];
                GradientAlphaKey[] alphas = new GradientAlphaKey[2];
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    col = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
                    keys[0] = new GradientColorKey(col, 0f);
                    alphas[0] = new GradientAlphaKey((float)rand.NextDouble(), 0f);
                    col = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
                    keys[1] = new GradientColorKey(col, 1f);
                    alphas[1] = new GradientAlphaKey((float)rand.NextDouble(), 1f);
                }
                else
                {
                    col = graphGenes.getColor();
                    keys[0] = new GradientColorKey(col, 0f);
                    alphas[0] = new GradientAlphaKey(graphGenes.getFloat01(), 0f);
                    col = graphGenes.getColor();
                    keys[1] = new GradientColorKey(col, 1f);
                    alphas[1] = new GradientAlphaKey(graphGenes.getFloat01(), 1f);
                }
                Gradient randGrad = new Gradient();
                randGrad.SetKeys(keys, alphas);
                inputNode.gradient = randGrad;
                if (generateInputNode)
                {
                    graph.Connect(outputSlot.slotReference, slot.slotReference);
                    graphManager.AddInputNode(inputNode);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Gradient but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesMat2(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Matrix2Node inputNode = (Matrix2Node)Activator.CreateInstance(typeof(Matrix2Node));
            Matrix2MaterialSlot outputSlot = new List<Matrix2MaterialSlot>(inputNode.GetOutputSlots<Matrix2MaterialSlot>())[0];
            graph.AddNode(inputNode);

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector2 vec0, vec1;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    vec0 = new Vector2((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec1 = new Vector2((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else
                {
                    //newMat = randomRanges.getMat2();
                    // TODO: have random ranges generate directly the matrix instead of manually getting 2 vec2
                    vec0 = graphGenes.getVec2();
                    vec1 = graphGenes.getVec2();
                }
                inputNode.row0 = vec0;
                inputNode.row1 = vec1;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Matrix 2 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesMat3(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Matrix3Node inputNode = (Matrix3Node)Activator.CreateInstance(typeof(Matrix3Node));
            graph.AddNode(inputNode);

            Matrix3MaterialSlot outputSlot = new List<Matrix3MaterialSlot>(inputNode.GetOutputSlots<Matrix3MaterialSlot>())[0];

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector3 vec0, vec1, vec2;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    vec0 = new Vector3((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec1 = new Vector3((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec2 = new Vector3((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else
                {
                    vec0 = graphGenes.getVec3();
                    vec1 = graphGenes.getVec3();
                    vec2 = graphGenes.getVec3();
                }
                inputNode.row0 = vec0;
                inputNode.row1 = vec1;
                inputNode.row2 = vec2;

                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Matrix 3 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesMat4(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Matrix4Node inputNode = (Matrix4Node)Activator.CreateInstance(typeof(Matrix4Node));
            graph.AddNode(inputNode);

            Matrix4MaterialSlot outputSlot = new List<Matrix4MaterialSlot>(inputNode.GetOutputSlots<Matrix4MaterialSlot>())[0];
            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector4 vec0, vec1, vec2, vec3;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    vec0 = new Vector4((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec1 = new Vector4((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec2 = new Vector4((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                    vec3 = new Vector4((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else
                {
                    vec0 = graphGenes.getVec4();
                    vec1 = graphGenes.getVec4();
                    vec2 = graphGenes.getVec4();
                    vec3 = graphGenes.getVec4();
                }
                inputNode.row0 = vec0;
                inputNode.row1 = vec1;
                inputNode.row2 = vec2;
                inputNode.row3 = vec3;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Matrix 4 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesVec1(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Vector1Node inputNode;
            Vector1MaterialSlot outputSlot;
            bool reuseNodeAsInput = slot.owner is Vector1Node;
            if (!reuseNodeAsInput)
            {
                inputNode = (Vector1Node)Activator.CreateInstance(typeof(Vector1Node));
                graph.AddNode(inputNode);
                outputSlot = new List<Vector1MaterialSlot>(inputNode.GetOutputSlots<Vector1MaterialSlot>())[0];
            }
            else
            {
                inputNode = slot.owner as Vector1Node;
                outputSlot = null;
            }
            Vector1MaterialSlot inputSlot = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[0];

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                float input;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    //int scale = rand.Next();
                    input = (float)rand.NextDouble();
                }
                else
                {
                    input = graphGenes.getFloat();
                }
                inputSlot.value = input;
                if (!reuseNodeAsInput)
                {
                    graph.Connect(outputSlot.slotReference, slot.slotReference);
                }
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Vec 1 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesVec2(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Vector2Node inputNode = (Vector2Node)Activator.CreateInstance(typeof(Vector2Node));
            graph.AddNode(inputNode);

            Vector2MaterialSlot outputSlot = new List<Vector2MaterialSlot>(inputNode.GetOutputSlots<Vector2MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot0 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot1 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[1];
            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector2 input;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    input = new Vector2((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else
                {
                    input = graphGenes.getVec2();
                }
                inputSlot0.value = input.x;
                inputSlot1.value = input.y;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Vec 2 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesVec3(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Vector3Node inputNode = (Vector3Node)Activator.CreateInstance(typeof(Vector3Node));
            graph.AddNode(inputNode);

            Vector3MaterialSlot outputSlot = new List<Vector3MaterialSlot>(inputNode.GetOutputSlots<Vector3MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot0 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot1 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[1];
            Vector1MaterialSlot inputSlot2 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[2];

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector3 input;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    input = new Vector3((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else
                {
                    input = graphGenes.getVec3();
                }
                inputSlot0.value = input.x;
                inputSlot1.value = input.y;
                inputSlot2.value = input.z;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Vec3 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesVec4(MaterialSlot slot, GraphManager graphManager)
        {
            GraphData graph = graphManager.getGraph();

            Vector4Node inputNode = (Vector4Node)Activator.CreateInstance(typeof(Vector4Node));
            graph.AddNode(inputNode);

            Vector4MaterialSlot outputSlot = new List<Vector4MaterialSlot>(inputNode.GetOutputSlots<Vector4MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot0 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[0];
            Vector1MaterialSlot inputSlot1 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[1];
            Vector1MaterialSlot inputSlot2 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[2];
            Vector1MaterialSlot inputSlot3 = new List<Vector1MaterialSlot>(inputNode.GetInputSlots<Vector1MaterialSlot>())[3];

            GeneticHandler graphGenes = null;
            try
            {
                graphGenes = graphManager.getGenes();
            }
            catch { }
            try
            {
                Vector4 input;
                if (graphGenes == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    input = new Vector4((float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale, (float)rand.NextDouble() * scale);
                }
                else input = graphGenes.getVec4();
                inputSlot0.value = input.x;
                inputSlot1.value = input.y;
                inputSlot2.value = input.z;
                inputSlot3.value = input.w;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.AddInputNode(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Vec4 but slot is {0}", slot.GetType().FullName);
                return false;
            }
        }
        private static bool randomizeValuesDynVec(MaterialSlot slot, GraphManager graphManager)
        {
            int coordinates = UnityEngine.Random.Range(1, 4);
            switch (coordinates)
            {
                case 1:
                    return randomizeValuesVec1(slot, graphManager);
                case 2:
                    return randomizeValuesVec2(slot, graphManager);
                case 3:
                    return randomizeValuesVec3(slot, graphManager);
                case 4:
                    return randomizeValuesVec4(slot, graphManager);
                default:
                    return randomizeValuesVec4(slot, graphManager);
            }
            /*
            try
            {
                Vector4 newVec;
                if (randomRanges == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    newVec = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                }
                else newVec = randomRanges.getVec4();
                casted.value = newVec;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.inputNodes.Add(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Dynamic Vector but slot is {0}", slot.GetType().FullName);
                return false;
            }*/
        }

        private static bool randomizeValuesDynMat(MaterialSlot slot, GraphManager graphManager)
        {
            int rank = UnityEngine.Random.Range(2, 4);
            switch (rank)
            {
                case 2:
                    return randomizeValuesMat2(slot, graphManager);
                case 3:
                    return randomizeValuesMat2(slot, graphManager);
                case 4:
                    return randomizeValuesMat4(slot, graphManager);
                default:
                    return randomizeValuesMat4(slot, graphManager);
            }
            /*
            GraphData graph = graphManager.graphObject.graph;
            try
            {
                DynamicMatrixMaterialSlot casted = (DynamicMatrixMaterialSlot)slot;
                Matrix4x4 newMat;
                if (randomRanges == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    Vector4 col4 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col3 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col2 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col1 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    newMat = new Matrix4x4(col1, col2, col3, col4);
                }
                else newMat = randomRanges.getMat4();
                casted.value = newMat;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.inputNodes.Add(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Dynamic Matrix but slot is {0}", slot.GetType().FullName);
                return false;
            }*/
        }
        private static bool randomizeValuesDynVal(MaterialSlot slot, GraphManager graphManager)
        {
            bool castToMatrix = UnityEngine.Random.value > 0.5;
            if (castToMatrix) return randomizeValuesDynMat(slot, graphManager);
            else return randomizeValuesDynVec(slot, graphManager);
            /*
            GraphData graph = graphManager.graphObject.graph;
            try
            {
                DynamicValueMaterialSlot casted = (DynamicValueMaterialSlot)slot;

                Matrix4x4 newMat;
                if (randomRanges == null)
                {
                    System.Random rand = new System.Random();
                    float scale = rand.Next() * (rand.NextDouble() > 0.5 ? 1 : -1);
                    Vector4 col4 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col3 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col2 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    Vector4 col1 = new Vector4((float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale, (float) rand.NextDouble() * scale);
                    newMat = new Matrix4x4(col1, col2, col3, col4);
                }
                else newMat = randomRanges.getMat4();
                casted.value = newMat;
                graph.Connect(outputSlot.slotReference, slot.slotReference);
                graphManager.inputNodes.Add(inputNode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                Debug.LogErrorFormat("Type was Dynamic Value but slot is {0}", slot.GetType().FullName);
                return false;
            }*/
        }
        #endregion


        #region Transfer Functions (Obsolete)
        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesBool(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            BooleanMaterialSlot castedParent = (BooleanMaterialSlot)parentSlot;
            BooleanMaterialSlot castedChild = (BooleanMaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesGrad(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            GradientInputMaterialSlot castedParent = (GradientInputMaterialSlot)parentSlot;
            GradientInputMaterialSlot castedChild = (GradientInputMaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesMat2(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Matrix2MaterialSlot castedParent = (Matrix2MaterialSlot)parentSlot;
            Matrix2MaterialSlot castedChild = (Matrix2MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesMat3(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Matrix3MaterialSlot castedParent = (Matrix3MaterialSlot)parentSlot;
            Matrix3MaterialSlot castedChild = (Matrix3MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesMat4(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Matrix4MaterialSlot castedParent = (Matrix4MaterialSlot)parentSlot;
            Matrix4MaterialSlot castedChild = (Matrix4MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesVec1(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Vector1MaterialSlot castedParent = (Vector1MaterialSlot)parentSlot;
            Vector1MaterialSlot castedChild = (Vector1MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesVec2(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Vector2MaterialSlot castedParent = (Vector2MaterialSlot)parentSlot;
            Vector2MaterialSlot castedChild = (Vector2MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesVec3(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Vector3MaterialSlot castedParent = (Vector3MaterialSlot)parentSlot;
            Vector3MaterialSlot castedChild = (Vector3MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesVec4(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Vector4MaterialSlot castedParent = (Vector4MaterialSlot)parentSlot;
            Vector4MaterialSlot castedChild = (Vector4MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesDynVec(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            DynamicVectorMaterialSlot castedParent = (DynamicVectorMaterialSlot)parentSlot;
            DynamicVectorMaterialSlot castedChild = (DynamicVectorMaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesDynMat(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            Matrix4MaterialSlot castedParent = (Matrix4MaterialSlot)parentSlot;
            Matrix4MaterialSlot castedChild = (Matrix4MaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }

        [Obsolete("Transfer functions work but have been superseeded by the built-in method MaterialSlot.CopyValuesFrom(MaterialSlot slot)")]
        private static bool transferValuesDynVal(MaterialSlot parentSlot, MaterialSlot childSlot)
        {
            DynamicValueMaterialSlot castedParent = (DynamicValueMaterialSlot)parentSlot;
            DynamicValueMaterialSlot castedChild = (DynamicValueMaterialSlot)childSlot;
            castedChild.value = castedParent.value;
            return true;
        }
        #endregion

    }

    public class ShaderPreviewWindow : MouseManipulator
    {
        bool m_Active;

        WindowDockingLayout m_WindowDockingLayout;

        Vector2 m_LocalMosueOffset;

        VisualElement m_Handle;
        GraphPreviewer previewWindow;

        public Action OnDragFinished;

        public ShaderPreviewWindow(VisualElement handle = null, VisualElement container = null)
        {
            m_Handle = handle;
            m_Active = false;
            m_WindowDockingLayout = new WindowDockingLayout();
            if (container != null)
            {
                container.style.minHeight = 200f;
                container.style.minWidth = 200f;
                container.StretchToParentSize();
                container.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
            }
            m_WindowDockingLayout.dockingLeft = true;
            m_WindowDockingLayout.dockingTop = true;
            m_WindowDockingLayout.horizontalOffset = 0f;
            m_WindowDockingLayout.verticalOffset = 0f;
            m_WindowDockingLayout.ApplyPosition(container);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            if (m_Handle == null)
                m_Handle = target;
            m_Handle.RegisterCallback(new EventCallback<MouseDownEvent>(OnMouseDown), TrickleDownEnum.NoTrickleDown);
            m_Handle.RegisterCallback(new EventCallback<MouseMoveEvent>(OnMouseMove), TrickleDownEnum.NoTrickleDown);
            m_Handle.RegisterCallback(new EventCallback<MouseUpEvent>(OnMouseUp), TrickleDownEnum.NoTrickleDown);
            target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            /*if (target != null)
            {
                Debug.Log("Registering Callbacks");
                Debug.Log(target.name);
                Debug.Log(target.GetType());
            }*/
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            m_Handle.UnregisterCallback(new EventCallback<MouseDownEvent>(OnMouseDown), TrickleDownEnum.NoTrickleDown);
            m_Handle.UnregisterCallback(new EventCallback<MouseMoveEvent>(OnMouseMove), TrickleDownEnum.NoTrickleDown);
            m_Handle.UnregisterCallback(new EventCallback<MouseUpEvent>(OnMouseUp), TrickleDownEnum.NoTrickleDown);
            target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (previewWindow != null)
                previewWindow.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void OnMouseDown(MouseDownEvent evt)
        {
            return;
            /*
            m_Active = true;

            VisualElement parent = target.parent;
            while (parent != null && !(parent is GraphPreviewer))
                parent = parent.parent;
            previewWindow = parent as GraphPreviewer;

            if (previewWindow != null)
                previewWindow.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // m_LocalMouseOffset is offset from the target element's (0, 0) to the
            // to the mouse position.
            m_LocalMosueOffset = m_Handle.WorldToLocal(evt.mousePosition);

            m_Handle.CaptureMouse();
            evt.StopImmediatePropagation();
            */
        }

        void OnMouseMove(MouseMoveEvent evt)
        {
            if (m_Active)
            {
                // The mouse position of is corrected according to the offset within the target
                // element (m_LocalWorldOffset) to set the position relative to the mouse position
                // when the dragging started.
                Vector2 position = target.parent.WorldToLocal(evt.mousePosition) - m_LocalMosueOffset;

                // Make sure that the object remains in the parent window
                position.x = Mathf.Clamp(position.x, 0f, target.parent.layout.width - target.layout.width);
                position.y = Mathf.Clamp(position.y, 0f, target.parent.layout.height - target.layout.height);

                // While moving, use only the left and top position properties,
                // while keeping the others NaN to not affect layout.
                target.style.left = position.x;
                target.style.top = position.y;
                target.style.right = float.NaN;
                target.style.bottom = float.NaN;
            }
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            return;
            /*
            bool emitDragFinishedEvent = m_Active;

            m_Active = false;

            if (m_Handle.HasMouseCapture())
            {
                m_Handle.ReleaseMouse();
            }

            evt.StopImmediatePropagation();

            // Recalculate which corner to dock to
            m_WindowDockingLayout.CalculateDockingCornerAndOffset(target.layout, target.parent.layout);
            m_WindowDockingLayout.ClampToParentWindow();

            // Use the docking results to figure which of left/right and top/bottom needs to be set.
            m_WindowDockingLayout.ApplyPosition(target);

            // Signal that the dragging has finished.
            if (emitDragFinishedEvent && OnDragFinished != null)
                OnDragFinished();
            */
        }

        void OnGeometryChanged(GeometryChangedEvent geometryChangedEvent)
        {
            // Make the target clamp to the border of the window if the
            // parent window becomes too small to contain it.
            /*Debug.Log("Geometry Changed ");
            if (target != null)
            {
                Debug.LogFormat("{0}", target.name);
            }*/
            return;
            /*
            if (target.parent.layout.width < target.layout.width)
            {
                if (m_WindowDockingLayout.dockingLeft)
                {
                    target.style.left = 0f;
                    target.style.right = float.NaN;
                }
                else
                {
                    target.style.left = float.NaN;
                    target.style.right = 0f;
                }
            }

            if (target.parent.layout.height < target.layout.height)
            {
                if (m_WindowDockingLayout.dockingTop)
                {
                    target.style.top = 0f;
                    target.style.bottom = float.NaN;
                }
                else
                {
                    target.style.top = float.NaN;
                    target.style.bottom = 0f;
                }
            }
            */
        }

        void OnParentGeometryChanged(GeometryChangedEvent geometryChangedEvent)
        {
            // Check if the parent window can no longer contain the target window.
            // If the window is out of bounds, make one edge clamp to the border of the
            // parent window.
            return;
            /*
            if (target.layout.xMin < 0f)
            {
                target.style.left = 0f;
                target.style.right = float.NaN;
            }

            if (target.layout.xMax > geometryChangedEvent.newRect.width)
            {
                target.style.left = float.NaN;
                target.style.right = 0f;
            }

            if (target.layout.yMax > geometryChangedEvent.newRect.height)
            {
                target.style.top = float.NaN;
                target.style.bottom = 0f;
            }

            if (target.layout.yMin < 0f)
            {
                target.style.top = 0f;
                target.style.bottom = float.NaN;
            }
            */
        }
    }
    
    public class Random
    {
        System.Random sysRand;
        private bool useGauss;
        private float maxBound = 100;
        private float minBound = -100f;
        private float gaussAvg = 0f;
        private float gaussDev = 50f;

        public Random(bool useGaussian = false)
        {
            useGauss = useGaussian;
            sysRand = new System.Random();
        }

        public float NextFloat()
        {
            if (useGauss)
            {
                return GaussRandom.NextFloat(gaussAvg, gaussDev, minBound, maxBound);
            }
            else
            {
                return ((float)sysRand.NextDouble() * (maxBound - minBound)) + minBound;
            }

        }

        public int Next()
        {
            if (useGauss) return GaussRandom.NextInt(gaussAvg, gaussDev, minBound, maxBound);
            else return sysRand.Next((int)minBound, (int)maxBound);
        }

        public double NextDouble()
        {
            if (useGauss) return (double)GaussRandom.NextFloat(0.5f, 0.25f, 0f, 1f);
            else return sysRand.NextDouble();
        }
    }

    public static class GaussRandom
    {

        public static float NextFloat()
        {
            float x = UnityEngine.Random.Range(0f, 1f);
            float y = UnityEngine.Random.Range(0f, 1f);
            float r2 = x * x + y * y;
            while (r2 > 1f || r2 ==0f)
            {
                x = UnityEngine.Random.Range(0f, 1f);
                y =UnityEngine.Random.Range(0f, 1f);
                r2 = x * x + y * y;
            }
            r2 = Mathf.Sqrt((-2.0f * Mathf.Log(r2)) / r2);

            return r2 * x;
        }
        
        public static float NextFloat(float avg,float dev)
        {
            return NextFloat() * dev + avg;
        }
        public static float NextFloat(float avg, float dev, float min, float max)
        {
            float result = NextFloat(avg, dev);
            while (result > max || result < min)
            {
                result = NextFloat(avg, dev);
            }
            return result;
        }

        public static int NextInt(float avg, float dev)
        {
            return Mathf.RoundToInt(NextFloat(avg, dev));
        }

        public static int NextInt(float avg, float dev, float min, float max)
        {
            return Mathf.RoundToInt(NextFloat(avg, dev,min,max));
        }

    }
    public static class ShaderGraphUtilities
    {
        public static string referenceGUID = null;
        public static int numberOfReferences = 0;

        public static string referencePath
        {
            get { return AssetDatabase.GUIDToAssetPath(referenceGUID); }
            private set { }
        }
        public static void writeAllPossibleSlotTypes()
        {
            string filePath = AssetDatabase.GenerateUniqueAssetPath("Assets/slotTypes.txt");
            System.IO.StreamWriter file = System.IO.File.CreateText(filePath);
            List<Type> inputSlots = new List<Type>();
            List<Type> outputSlots = new List<Type>();
            foreach (var nodeType in NodeClassCache.knownNodeTypes)
            {
                AbstractMaterialNode node = (AbstractMaterialNode)Activator.CreateInstance(nodeType);
                foreach(var input in node.GetInputSlots<MaterialSlot>())
                {
                    if (!inputSlots.Contains(input.GetType())) inputSlots.Add(input.GetType());
                }
                foreach( var output in node.GetOutputSlots<MaterialSlot>())
                {
                    if (!outputSlots.Contains(output.GetType())) outputSlots.Add(output.GetType());
                }
            }
            file.WriteLine("Inputs:\n");
            foreach (var slotType in inputSlots)
            {
                string line = string.Format("{0} --- {1} ", slotType.Name, slotType.FullName);
                file.WriteLine(line);
            }
            file.WriteLine("Outputs:\n");
            foreach (var slotType in outputSlots)
            {
                string line = string.Format("{0} --- {1} ", slotType.Name, slotType.FullName);
                file.WriteLine(line);
            }
            file.Close();
        }
        internal static BlockNode tryGetBlockNode(GraphData graph, string nodeName)
        {
            foreach(var node in graph.GetNodes<BlockNode>())
            {
                if(node.name.Contains(nodeName) || nodeName.Contains(node.name) || node.name == nodeName)
                {
                    return node;
                }
            }
            return null;
        }
        internal static bool graphHasNode(GraphData graph, Type nodeType)
        {
            return new List<AbstractMaterialNode>(graph.GetNodes<AbstractMaterialNode>()).Exists(n => n.GetType().Name == nodeType.Name || n.GetType().FullName == nodeType.FullName);
        }
        public static int getNumberOfKnownNodes()
        {
            return new List<Type>(NodeClassCache.knownNodeTypes).Count;
        }

        internal static bool hasConnectedInputs(AbstractMaterialNode node)
        {
            foreach(MaterialSlot slot in node.GetInputSlots<MaterialSlot>())
            {
                if (slot.isConnected) return true;
            }
            return false;
        }
    }

    public class NodeNotGeneratedException : Exception { }

    internal static class BlockNodes
    {
        internal const string vertexPosition = "Position";
        internal const string vertexNormal = "Normal";
        internal const string vertexTangent = "Tangent";
        internal const string fragmentColor = "BaseColor";
        internal const string fragmentNormal = "NormalTS";
        internal const string fragmentMetallic = "Metallic";
        internal const string fragmentSmoothness = "Smoothness";
        internal const string fragmentEmission = "Emission";
        internal const string fragmentOcclusion = "Occlusion";
    }
}
