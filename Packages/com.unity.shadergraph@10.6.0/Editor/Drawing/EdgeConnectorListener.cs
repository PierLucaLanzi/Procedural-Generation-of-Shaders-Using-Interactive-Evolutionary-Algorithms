using UnityEngine;
using UnityEditor.Experimental.GraphView;
using Edge = UnityEditor.Experimental.GraphView.Edge;
using UnityEditor.Searcher;

namespace UnityEditor.ShaderGraph.Drawing
{
    class EdgeConnectorListener : IEdgeConnectorListener
    {
        readonly GraphData m_Graph;
        readonly SearchWindowProvider m_SearchWindowProvider;
        readonly EditorWindow m_editorWindow;

        public EdgeConnectorListener(GraphData graph, SearchWindowProvider searchWindowProvider, EditorWindow editorWindow)
        {
            m_Graph = graph;
            m_SearchWindowProvider = searchWindowProvider;
            m_editorWindow = editorWindow;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var draggedPort = (edge.output != null ? edge.output.edgeConnector.edgeDragHelper.draggedPort : null) ?? (edge.input != null ? edge.input.edgeConnector.edgeDragHelper.draggedPort : null);
            try { 
                Debug.Log(string.Format("before:{0}",m_SearchWindowProvider.connectedPort.node.title));
                Debug.Log(edge.output.GetSlot().owner.name);
                Debug.Log(edge.output.GetSlot().id);
                Debug.Log(edge.output.GetSlot().owner.GetType());
                Debug.Log(edge.output.GetSlot().displayName);
                Debug.Log(edge.output.GetSlot().GetType().Name);
                MaterialSlot outslot = edge.output.GetSlot();
                Debug.Log(outslot.concreteValueType);
                try
                {

                    ColorNode colorNode = (ColorNode)outslot.owner;
                    Debug.Log(colorNode.sgVersion);
                    Debug.Log(colorNode.color);
                    Debug.Log(colorNode.color.color);
                    Debug.Log(colorNode.color.mode);
                    Debug.Log(colorNode.color.color.r);
                    Debug.Log(colorNode.color.color.g);
                    Debug.Log(colorNode.color.color.b);
                    Debug.Log(colorNode.color.color.a);


                }
                catch { }
            }
            catch { }
            m_SearchWindowProvider.connectedPort = (ShaderPort)draggedPort;
            try {
                Debug.Log(string.Format("after:{0}", m_SearchWindowProvider.connectedPort.node.title));
                Debug.LogFormat("Id : {0}", edge.input.GetSlot().id);
                Debug.Log(edge.input.GetSlot().owner.name);
                Debug.Log(edge.input.GetSlot().displayName);
                Debug.Log(edge.input.GetSlot().GetType().Name);
                try
                {

                    DynamicVectorMaterialSlot dynamicSlot = (DynamicVectorMaterialSlot)edge.input.GetSlot();
                    Debug.LogFormat("{0} ---",dynamicSlot.concreteValueType);
                    Debug.Log("E");
                    Debug.Log(dynamicSlot.value);
                    ClampNode node = (ClampNode)dynamicSlot.owner;
                    Debug.Log(node.GetSlotValue(1, GenerationMode.Preview));
                    Debug.Log(node.GetSlotValue(2, GenerationMode.Preview));


                }
                catch { }
            }
            catch { Debug.Log("e"); }
            m_SearchWindowProvider.regenerateEntries = true;//need to be sure the entires are relevant to the edge we are dragging
            SearcherWindow.Show(m_editorWindow, (m_SearchWindowProvider as SearcherProvider).LoadSearchWindow(), 
                item => (m_SearchWindowProvider as SearcherProvider).OnSearcherSelectEntry(item, position),
                position, null);
            m_SearchWindowProvider.regenerateEntries = true;//entries no longer necessarily relevant, need to regenerate
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            var leftSlot = edge.output.GetSlot();
            var rightSlot = edge.input.GetSlot();
            if (leftSlot != null && rightSlot != null)
            {
                m_Graph.owner.RegisterCompleteObjectUndo("Connect Edge");
                m_Graph.Connect(leftSlot.slotReference, rightSlot.slotReference);
            }
        }
    }
}
