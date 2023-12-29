using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    [Title("Input", "Basic", "Float")]
    class Vector1Node : AbstractMaterialNode, IGeneratesBodyCode, IPropertyFromNode
    {
        [SerializeField]
        private float m_Value = 0;

        protected const string kInputSlotXName = "X";
        protected const string kOutputSlotName = "Out";

        public const int InputSlotXId = 1;
        public const int OutputSlotId = 0;

        public Vector1Node()
        {
            name = "Float";
            synonyms = new string[]{"Vector 1"};
            UpdateNodeAfterDeserialization();
        }


        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new Vector1MaterialSlot(InputSlotXId, kInputSlotXName, kInputSlotXName, SlotType.Input, m_Value));
            AddSlot(new Vector1MaterialSlot(OutputSlotId, kOutputSlotName, kOutputSlotName, SlotType.Output, 0));
            RemoveSlotsNameNotMatching(new[] { OutputSlotId, InputSlotXId });
        }

        public void GenerateNodeCode(ShaderStringBuilder sb, GenerationMode generationMode)
        {
            var inputValue = GetSlotValue(InputSlotXId, generationMode);
            sb.AppendLine(string.Format("$precision {0} = {1};", GetVariableNameForSlot(OutputSlotId), inputValue));
        }

        public AbstractShaderProperty AsShaderProperty()
        {
            var slot = FindInputSlot<Vector1MaterialSlot>(InputSlotXId);
            return new Vector1ShaderProperty { value = slot.value };
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            name = "Float";
        }

        int IPropertyFromNode.outputSlotId { get { return OutputSlotId; } }
    }
    /*
    [Title("Autogen", "Float")]
    class RandomizableVector1Node : Vector1Node, IGeneratesBodyCode, IPropertyFromNode
    {
        int IPropertyFromNode.outputSlotId { get { return OutputSlotId; } }

        public RandomizableVector1Node()
        {
            name = "Randomizable Float";
            synonyms = new string[] { "Vector 1" };
            UpdateNodeAfterDeserialization();
        }
    }*/
}

namespace AutoGen
{

    [UnityEditor.ShaderGraph.Title("Autogen", "Float (Randomizable)")]
    class RandomizableVector1Node : UnityEditor.ShaderGraph.Vector1Node, UnityEditor.ShaderGraph.IGeneratesBodyCode, UnityEditor.ShaderGraph.IPropertyFromNode
    {
        int UnityEditor.ShaderGraph.IPropertyFromNode.outputSlotId { get { return OutputSlotId; } }

        public RandomizableVector1Node()
        {
            name = "Randomizable Float";
            synonyms = new string[] { "Vector 1" };
            UpdateNodeAfterDeserialization();
        }
        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            name = "Randomizable Float";
        }
    }

    [UnityEditor.ShaderGraph.Title("Autogen", "Float (Assignable)")]
    class AssignableVector1Node : UnityEditor.ShaderGraph.Vector1Node, UnityEditor.ShaderGraph.IGeneratesBodyCode, UnityEditor.ShaderGraph.IPropertyFromNode
    {
        int UnityEditor.ShaderGraph.IPropertyFromNode.outputSlotId { get { return OutputSlotId; } }

        public AssignableVector1Node()
        {
            name = "Assignable Float";
            synonyms = new string[] { "Vector 1" };
            UpdateNodeAfterDeserialization();
        }
        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            name = "Assignable Float";
        }
    }
}
