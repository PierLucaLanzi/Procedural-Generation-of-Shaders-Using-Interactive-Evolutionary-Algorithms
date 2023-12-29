using AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using AutoRandom = AutoGen.Random;

public class AutoGenLauncher : EditorWindow
{
    private string graphBaseName = "autogen";
    private int nodeTypesNumber = 0;
    private int massiveGraphNumber = 0;
    private int iterationsNumber = 0;
    private int graphID = 0;
    private int lastGenerationSize = 0;
    private string graphFolder = "./";
    private string configPath = "";
    private bool initConf = false;
    private GraphManager selectedGraph;
    private EditorWindow singlePreviewWindow;
    private List<GraphManager> lastGraphGeneration;
    private ShaderPreviewEditor multiplePreviewsWindow;

    private int activeStageIndex = 1;
    
    private int geneticRunSize = 0;
    private int geneticNodeAdditions = 0;
    private MutationStrength currentMutationStrength;
    private MutationStrength previousMutationStrength;
    private GeneticGraphGenerator currentGeneticRun;
    private Shader customGraph;

    bool usePreExistingGraph = false;
    bool allowBranchExpansionOnMutate;
    bool allowGraphTypeChangeOnMutate;
    bool previousAllowBranch ;
    bool previousAllowTypeChange;

    float previousExpProb;
    float previousTypeProb;
    float expansionProbability;
    float typeChangeProbability;

    private void Awake()
    {

        currentMutationStrength = MutationStrength.Low;
        previousMutationStrength = currentMutationStrength;

        allowBranchExpansionOnMutate = false;
        expansionProbability = 0.5f;
        allowGraphTypeChangeOnMutate = false;
        typeChangeProbability = 0.5f;
        previousExpProb = expansionProbability;
        previousTypeProb = typeChangeProbability;

        previousAllowBranch = allowBranchExpansionOnMutate;
        previousAllowTypeChange = allowGraphTypeChangeOnMutate;

        initializeReferencePath();

        List<string> assets = new List<string>(AssetDatabase.FindAssets(this.GetType().Name));
        configPath = AssetDatabase.GUIDToAssetPath(assets[0]);
        configPath = Path.GetDirectoryName(configPath);
        configPath = Path.GetDirectoryName(configPath);
        configPath = Path.Combine(configPath, "Config");
        configPath = Path.Combine(configPath, "conf.txt");
        initConf = File.Exists(configPath);
        nodeTypesNumber = ShaderGraphUtilities.getNumberOfKnownNodes();
        if (initConf)
        {
            List<string> lines = new List<string>(File.ReadLines(configPath));
            if (lines.Capacity > 0)
            {
                graphFolder = lines[0];
                if(AssetDatabase.AssetPathToGUID(graphFolder) == "") graphFolder = FileUtil.GetProjectRelativePath(lines[0]);
            }
            else initConf = false;
        }
        else
        {
            StreamWriter configFile = File.CreateText(configPath);
            configFile.Close();
        }
    }

    [MenuItem("Tools/Shadergraph AutoGen")]
    public static void ShowWindow()
    {
        GetWindow(typeof(AutoGenLauncher)).titleContent = new GUIContent("Shader Graph AutoGen");

    }

    // On GUI is launched everytime the mouse is hovering on the editor window
    private void OnGUI()
    {

        GUILayout.Label("Generate Shadergraph", EditorStyles.boldLabel);
        if (ShaderGraphUtilities.referencePath == "") initializeReferencePath();
        if (!initConf || graphFolder == null || graphFolder?.Length == 0)
        {
            string graphFolder = EditorUtility.OpenFolderPanel("Choose path for shadergraphs", "", "");
            File.WriteAllText(configPath, graphFolder);
            initConf = true;
        }
        GUILayout.Label(string.Format("Preview Scenes: {0}", EditorSceneManager.previewSceneCount));

        
        graphBaseName = EditorGUILayout.TextField("Base Name", graphBaseName);
        EditorGUILayout.LabelField("Number of node types: " + nodeTypesNumber);
        //EditorGUILayout.LabelField("Last Graph ID:\t\t  " + graphID);
        //EditorGUILayout.LabelField(string.Format("Graph Exists:\t\t  {0}",selectedGraph!=null ));
        EditorGUILayout.LabelField("Number of mutation references: " + ShaderGraphUtilities.numberOfReferences);

        EditorGUILayout.BeginVertical(GUILayout.Height(10f));
        EditorGUILayout.Space();
        EditorGUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.MaxWidth(position.width / 2f)); ;

        EditorGUILayout.LabelField("Graph Path:\n    " + graphFolder, GUILayout.MaxHeight(30f), GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Refrefence Path:\n    " + ShaderGraphUtilities.referencePath, GUILayout.MaxHeight(30f), GUILayout.ExpandWidth(true));

        int numberOfGraphs = Directory.GetFiles(graphFolder).Length/2;
        if (graphID > numberOfGraphs)
        {
            SetSmallestAvailableID();
        }

        #region Unused Deprecated Code
        //GUILayout.Label("Single Graph Generation and Randomization", EditorStyles.boldLabel);
        /*
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Graph"))
        {
            IntermediateInterface.Launch(graphFolder, graphID,GraphType.Blank,graphBaseName);
            graphID += 1;
        }
        if (GUILayout.Button("Generate Lit Graph"))
        {
            IntermediateInterface.Launch(graphFolder, graphID, GraphType.Lit, graphBaseName);
            graphID += 1;
        }
        if (GUILayout.Button("Generate Unlit Graph"))
        {
            IntermediateInterface.Launch(graphFolder, graphID, GraphType.Unlit, graphBaseName);
            graphID += 1;
        }
        GUILayout.EndHorizontal
        
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Open Last Graph in Editor"))
        {
            //Debug.Log(string.Format("{0}/{1}_{2}", graphFolder, "graph", graphID));
            IntermediateInterface.openGraph(string.Format("{0}/{1}_{2}.{3}", graphFolder, graphBaseName, (graphID-1),IntermediateInterface.sgext));
            //IntermediateInterface.readGraphJsonFile(graphFolder+"/graph_0.shadergraph");

        }
        */
        #endregion
        if (GUILayout.Button("Set Graph Folder", GUILayout.ExpandWidth(true)))
        {
            string newGraphFolder = EditorUtility.OpenFolderPanel("Choose path for shadergraphs", "", "");
            if (newGraphFolder != "")
            {
                graphFolder = FileUtil.GetProjectRelativePath(newGraphFolder);
                File.WriteAllText(configPath, graphFolder);
            }
        }
        GUILayout.EndVertical();

        #region Single Graph Management, Currently Unused
        /*
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("Load and Preview Graph From Last Generated File"))
        {
            if(singlePreviewWindow != null)
            {
                singlePreviewWindow.Close();
            }
            selectedGraph = IntermediateInterface.initGraph(string.Format("{0}/{1}_{2}.{3}", graphFolder, graphBaseName, (graphID - 1),IntermediateInterface.sgext),false,GraphType.Unknown);
            singlePreviewWindow = CreateWindow<EditorWindow>(typeof(SceneView));
            singlePreviewWindow.titleContent = new GUIContent("Shaders Preview");
            selectedGraph.addShaderPreview(singlePreviewWindow);
            singlePreviewWindow.Repaint();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add and Connect Random Node") && selectedGraph != null)
        {
            selectedGraph.addRandomCompatibleNode();
        }
        if (GUILayout.Button("Test Input Randomization") && selectedGraph != null)
        {
            selectedGraph.randomizeGraphInputs();
        }
        
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("Add Random Node") && selectedGraph!= null)
        {
            selectedGraph.AddRandomNode();
        }
        */
        #endregion

        GUILayout.BeginVertical(GUILayout.MaxWidth(position.width / 2f));
        GUILayout.Label("Multiple Graph Generation and Randomization", EditorStyles.boldLabel);

        Shader newCustomGraph = (Shader)EditorGUILayout.ObjectField("Custom Graph",customGraph, typeof(Shader), false);

        if (newCustomGraph== null || AssetDatabase.GetAssetPath(newCustomGraph).EndsWith(".shadergraph") )
        {
            customGraph = newCustomGraph;
        }
        else
        {
            Debug.Log(newCustomGraph);
            Debug.LogError("Chosen file is of wrong type. Please choose a .shadergraph file");
        }
        usePreExistingGraph = EditorGUILayout.Toggle("Use Custom Graph", usePreExistingGraph, GUILayout.Height(30f));



        massiveGraphNumber = EditorGUILayout.IntField("Generated Graphs Number", massiveGraphNumber);
        iterationsNumber = EditorGUILayout.IntField("Successive Mutations", iterationsNumber);
        
        geneticRunSize = massiveGraphNumber;
        geneticNodeAdditions = iterationsNumber;

        currentMutationStrength =(MutationStrength) EditorGUILayout.EnumPopup("Mutation Strength",currentMutationStrength);
        allowBranchExpansionOnMutate = EditorGUILayout.Toggle("Allow graph expansion", allowBranchExpansionOnMutate, GUILayout.Height(30f));
        if (allowBranchExpansionOnMutate)
        {
            expansionProbability = EditorGUILayout.Slider("Probability",expansionProbability, 0f, 1f);
        }

        allowGraphTypeChangeOnMutate = EditorGUILayout.Toggle("Allow graph type change", allowGraphTypeChangeOnMutate, GUILayout.Height(30f));
        if (allowGraphTypeChangeOnMutate)
        {
            typeChangeProbability = EditorGUILayout.Slider("Probability", typeChangeProbability, 0f, 1f);
        }
        EditorGUILayout.LabelField("Last Generation Size:  " + lastGenerationSize);
        GUILayout.EndVertical();
        
        #region Checking On Variable Changes

        if (currentMutationStrength != previousMutationStrength && lastGraphGeneration != null)
        {
            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (graph.getGenes() != null)
                {
                    graph.getGenes().mutationStrength = currentMutationStrength;
                }
            }
            if (currentGeneticRun != null)
            {
                currentGeneticRun.setNewMutationStrength(currentMutationStrength);
            }
            previousMutationStrength = currentMutationStrength;
        }
        if (allowGraphTypeChangeOnMutate != previousAllowTypeChange && lastGraphGeneration != null)
        {
            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (graph.getGenes() != null)
                {
                    graph.getGenes().changeTypeAllowed = allowGraphTypeChangeOnMutate;
                }
            }
            if (currentGeneticRun != null)
            {
                currentGeneticRun.setNewTypeChangeAllow(allowGraphTypeChangeOnMutate);
            }
            previousAllowTypeChange = allowGraphTypeChangeOnMutate;
        }
        if (allowBranchExpansionOnMutate != previousAllowBranch && lastGraphGeneration != null)
        {
            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (graph.getGenes() != null)
                {
                    graph.getGenes().expandAllowed = allowBranchExpansionOnMutate;
                }
            }
            if (currentGeneticRun != null)
            {
                currentGeneticRun.setNewExpandAllow(allowBranchExpansionOnMutate);
            }
            previousAllowBranch = allowBranchExpansionOnMutate;
        }
        if (expansionProbability != previousExpProb && lastGraphGeneration != null)
        {
            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (graph.getGenes() != null)
                {
                    graph.getGenes().expansionProbability = expansionProbability;
                }
            }
            if (currentGeneticRun != null)
            {
                currentGeneticRun.setNewExpandProbability(expansionProbability);
            }
            previousExpProb = expansionProbability;
        }
        if (typeChangeProbability != previousTypeProb && lastGraphGeneration != null)
        {
            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (graph.getGenes() != null)
                {
                    graph.getGenes().typeChangeProbability = typeChangeProbability;
                }
            }
            if (currentGeneticRun != null)
            {
                currentGeneticRun.setNewTypeChangeProbability(typeChangeProbability);
            }
            previousTypeProb = typeChangeProbability;
        }

        #endregion


        if (GUILayout.Button("Create new Graph Group"))
        {
            initializeGraphList();
            bool litOrUnlit;
            for (int i = 0; i < massiveGraphNumber; i++)
            {
                litOrUnlit = Random.Range(0f, 1f) >= 0.5f;
                GraphType gt;
                if (litOrUnlit) gt = GraphType.Lit;
                else gt = GraphType.Unlit;
                lastGraphGeneration.Add(GraphInterface.LaunchAndGet(graphFolder, graphID + i, gt, graphBaseName));
            }
            graphID += massiveGraphNumber;
            //Debug.LogFormat("Generated {0} graphs", graphs.Count);
            for (int i = 0; i < lastGraphGeneration.Count; i++)
            {
                lastGraphGeneration[i].addMultipleCompatibleNodes(iterationsNumber);
            }
            lastGenerationSize = lastGraphGeneration.Count;
        }
        if (GUILayout.Button("Create new Mutated Graph Group"))
        {
            initializeGraphList();
            bool litOrUnlit;
            for (int i = 0; i < massiveGraphNumber; i++)
            {
                litOrUnlit = Random.Range(0f, 1f) >= 0.5f;
                GraphType gt;
                if (litOrUnlit) gt = GraphType.Lit;
                else gt = GraphType.Unlit;
                lastGraphGeneration.Add(GraphInterface.LaunchAndGet(graphFolder, graphID + i, gt, graphBaseName));
            }
            graphID += massiveGraphNumber;
            //Debug.LogFormat("Generated {0} graphs", graphs.Count);
            for (int i = 0; i < lastGraphGeneration.Count; i++)
            {
                lastGraphGeneration[i].mutateGraph(iterationsNumber);
            }
            lastGenerationSize = lastGraphGeneration.Count;
        }
        if (GUILayout.Button("Create and Randomize new Graph Group"))
        {
            initializeGraphList();
            bool litOrUnlit;
            for (int i = 0; i < massiveGraphNumber; i++)
            {
                litOrUnlit = Random.Range(0f, 1f) >= 0.5f;
                GraphType gt;
                if (litOrUnlit) gt = GraphType.Lit;
                else gt = GraphType.Unlit;
                lastGraphGeneration.Add(GraphInterface.LaunchAndGet(graphFolder, graphID, gt, graphBaseName));
                graphID++;
            }
            //Debug.LogFormat("Generated {0} graphs", graphs.Count);
            for (int i = 0; i < lastGraphGeneration.Count; i++)
            {
                lastGraphGeneration[i].addMultipleCompatibleNodes(iterationsNumber);
                //Debug.LogErrorFormat("Randomizing graph number {0}", i + graphID-massiveGraphNumber);
                lastGraphGeneration[i].randomizeGraphInputs();
            }
            lastGenerationSize = lastGraphGeneration.Count;
        }
        /*
         * This is a legacy option which used the built-in preview system from the shadergraph previewer in unity
         * The new version uses a custom copy of the built-in version, which allows addition of different previews 
         * 
        if (GUILayout.Button("Load and Preview Last Graph Generation") && lastGraphGeneration?.Count>0)
        {
            if(multiplePreviewsWindow != null)
            {
                multiplePreviewsWindow.Close();
                DestroyImmediate(multiplePreviewsWindow);
            }
            multiplePreviewsWindow = CreateWindow<ShaderPreviewEditor>(typeof(SceneView));
            multiplePreviewsWindow.titleContent = new GUIContent("Previews");
            foreach (GraphManager graph in lastGraphGeneration)
            {
                graph.addShaderPreview(multiplePreviewsWindow);
                multiplePreviewsWindow.Repaint();
            }
        }
        */

        if (GUILayout.Button("Preview Last Generated Group") && lastGraphGeneration?.Count > 0)
        {
            launchGenerationPreview(false);
        }

        if(GUILayout.Button("Start new genetic run"))
        {
            currentGeneticRun = new GeneticGraphGenerator(geneticRunSize, graphFolder, graphBaseName,geneticNodeAdditions,currentMutationStrength,allowBranchExpansionOnMutate,allowGraphTypeChangeOnMutate,expansionProbability,typeChangeProbability);
            lastGraphGeneration = currentGeneticRun.population;
            launchGenerationPreview(true);

        }
        EditorGUI.BeginDisabledGroup(lastGraphGeneration == null);
        if (GUILayout.Button("New Generation"))
        {
            if(currentGeneticRun == null)
            {
                currentGeneticRun = new GeneticGraphGenerator(geneticRunSize, graphFolder, graphBaseName, geneticNodeAdditions, currentMutationStrength, allowBranchExpansionOnMutate, allowGraphTypeChangeOnMutate, expansionProbability, typeChangeProbability);
                lastGraphGeneration = currentGeneticRun.population;
                launchGenerationPreview(true);
            }
            else if(multiplePreviewsWindow != null)
            {
                try
                {
                    foreach (int index in multiplePreviewsWindow.selectedCells)
                    {
                        //Debug.LogFormat("Accessing {0}", index);
                        currentGeneticRun.selectGraph(lastGraphGeneration[index]);
                    }
                    multiplePreviewsWindow.Close();
                    currentGeneticRun.newGeneration();
                    lastGraphGeneration = currentGeneticRun.population;
                    launchGenerationPreview(true);
                }
                catch(ArgumentOutOfRangeException e)
                {
                    Debug.LogError(e.Message);
                    foreach(var key in e.Data.Keys)
                    {
                        Debug.LogErrorFormat("{0} : {1}", key, e.Data[key]);
                    }
                    Debug.LogError("Index error, graphgen size and selectedcells size");
                    Debug.LogError(lastGraphGeneration.Count);
                    Debug.LogError(multiplePreviewsWindow.selectedCells.Count);
                }
            }
        }
        EditorGUI.EndDisabledGroup();


        if (GUILayout.Button("Reset"))
        {
            selectedGraph = null;
            lastGenerationSize = 0;
            lastGraphGeneration = null;
        }
    }



    private void launchGenerationPreview(bool useSelector)
    {
        //Debug.LogError("Launching Preview");
        if (multiplePreviewsWindow != null) multiplePreviewsWindow.Close();
        multiplePreviewsWindow = CreateWindow<ShaderPreviewEditor>(typeof(SceneView));
        multiplePreviewsWindow.titleContent = new GUIContent("Previews");
        multiplePreviewsWindow.selectorIsActive = true;
        multiplePreviewsWindow.legacyPreview = false;
        foreach (GraphManager graph in lastGraphGeneration)
        {
            //Debug.Log(lastGraphGeneration.IndexOf(graph));
            graph.addShaderPreview(multiplePreviewsWindow);
            multiplePreviewsWindow.Repaint();
        }
        //Debug.LogError("Launched Preview");
    }

    // Update is called once per frame
    void Update()
    {

        int numberOfGraphs = 0;
        // Size Checks
        try
        {
            numberOfGraphs = Directory.GetFiles(graphFolder).Length / 2;
        }
        catch
        {
            string newGraphFolder = EditorUtility.OpenFolderPanel("Choose path for shadergraphs", "", "");
            if (newGraphFolder != "")
            {
                graphFolder = FileUtil.GetProjectRelativePath(newGraphFolder);
                File.WriteAllText(configPath, graphFolder);
            }

        }
        if (numberOfGraphs > graphID)
        {
            SetSmallestAvailableID();
        }
        if (lastGraphGeneration != null)
        {
            lastGenerationSize = lastGraphGeneration.Count;
        }
        else lastGenerationSize = 0;

        // Updating views
        if(selectedGraph == null && singlePreviewWindow != null) singlePreviewWindow.Close();
        selectedGraph?.updateShaderPreview(singlePreviewWindow);

        if ((lastGraphGeneration == null || lastGraphGeneration.Count == 0) && multiplePreviewsWindow != null) multiplePreviewsWindow.Close();
        if (lastGraphGeneration!=null) {
            bool updateStage = false;
            if (multiplePreviewsWindow != null)
            {
                updateStage = activeStageIndex != multiplePreviewsWindow.activeStageIndex;
                if (updateStage) activeStageIndex = multiplePreviewsWindow.activeStageIndex;
            }

            foreach (GraphManager graph in lastGraphGeneration)
            {
                if (updateStage) graph.stageNumber = activeStageIndex;
                graph.updateShaderPreview(multiplePreviewsWindow);
            }
        }
    }

    void SetSmallestAvailableID()
    {
        int numberOfFiles = Directory.GetFiles(graphFolder).Length / 2;
        if (numberOfFiles == 0)
        {
            graphID = 0;
            return;
        }
        int smallestAvailableID = graphID;
        if(graphID > numberOfFiles)
        {
            smallestAvailableID = 0;
        }
        for (int i = smallestAvailableID; i < numberOfFiles; i++)
        {
            string graphPath = Utilities.generateFilePath(graphFolder, graphBaseName, i.ToString());
            if (!File.Exists(graphPath))
            {
                smallestAvailableID = i;
            }
        }
        graphID = smallestAvailableID + 1;
    }
    private void initializeGraphList()
    {
        if (lastGraphGeneration?.Count > 0) foreach (GraphManager gm in lastGraphGeneration) gm.Dispose();
        lastGraphGeneration = new List<GraphManager>(massiveGraphNumber);
    }

    private void initializeReferencePath()
    {
        List<string> assets = new List<string>(AssetDatabase.FindAssets(this.GetType().Name));
        string editorPath = AssetDatabase.GUIDToAssetPath(assets[0]);
        editorPath = Path.GetDirectoryName(editorPath);
        string referenceGUID = Path.GetDirectoryName(editorPath);
        referenceGUID = Path.Combine(referenceGUID, "References");
        if (AssetDatabase.AssetPathToGUID(referenceGUID) == "") referenceGUID = AssetDatabase.AssetPathToGUID(FileUtil.GetProjectRelativePath(referenceGUID));
        else referenceGUID = AssetDatabase.AssetPathToGUID(referenceGUID);
        ShaderGraphUtilities.referenceGUID = referenceGUID;
        RandomizationHelper.initializeGraphMutations();
    }
}


namespace AutoGen
{

    public class GeneticGraphGenerator
    {
        private int popSize;
        public List<GraphManager> selectedSamples;
        public List<GraphManager> population;
        private string graphFolder;
        private string baseName;
        private int generation;
        private string ID = null;
        private int mutationsAtInit = 1;
        private MutationStrength lastSetStrength;
        private bool allowGraphExpansion;
        private bool allowTypeChange;
        private float expansionProbability;
        private float typeChangeProbability;

        public GeneticGraphGenerator(int size, string folderPath, string baseName, int mutationsAtInit, MutationStrength strength = MutationStrength.High, bool allowExpansion = false, bool allowTypeChange = false, float expansionProbability = 0f, float typeChangeProbability = 0f)
        {
            this.expansionProbability = expansionProbability;
            this.typeChangeProbability = typeChangeProbability;
            allowGraphExpansion = allowExpansion;
            this.allowTypeChange = allowTypeChange;
            lastSetStrength = strength;
            this.mutationsAtInit = mutationsAtInit;
            popSize = size;
            Initialize(folderPath, baseName);
            population = initNewPopulation(size, lastSetStrength);
            generation++;
        }

        public GeneticGraphGenerator(int size, string folderPath, string name, MutationStrength strength = MutationStrength.High)
        {
            lastSetStrength = strength;
            popSize = size;
            Initialize(folderPath, name);
            population = initNewPopulation(size, lastSetStrength);
            generation++;
        }

        // This is only used when inheriting from an existing population which is not handled by the genetic graph generator
        public GeneticGraphGenerator(List<GraphManager> graphPopulation, string folderPath, string name, int nodeAtInit, MutationStrength strength = MutationStrength.High)
        {
            lastSetStrength = strength;
            mutationsAtInit = nodeAtInit;
            popSize = graphPopulation.Count;
            Initialize(folderPath, name);
            population = new List<GraphManager>(graphPopulation);
            generation++;
        }
        public GeneticGraphGenerator(List<GraphManager> graphPopulation, string folderPath, string name, MutationStrength strength = MutationStrength.High)
        {
            lastSetStrength = strength;
            popSize = graphPopulation.Count;
            Initialize(folderPath, name);
            population = new List<GraphManager>(graphPopulation);
            generation++;
        }

        private void Initialize(string folderPath, string baseName)
        {
            selectedSamples = new List<GraphManager>();
            this.baseName = baseName;
            generation = 0;
            string folderName = "";
            while (ID == null || File.Exists(graphFolder))
            {
                ID = Hash128.Compute(UnityEngine.Random.value).ToString();
                folderName = string.Format("run_{0}", ID.Substring(0, 5));
                graphFolder = Path.Combine(folderPath, folderName);
                Debug.Log(graphFolder);
            }
            if (AssetDatabase.CreateFolder(folderPath, folderName) == "") throw new FileNotFoundException("Tried creating run folder but failed");

        }

        public void selectGraph(GraphManager graph)
        {
            if (!population.Contains(graph))
            {
                throw new ArgumentException("graph passed is not in the population list");
            }
            else
            {
                selectedSamples.Add(graph);
            }
        }

        public void newGeneration()
        {
            if (selectedSamples.Count == 0)
            {
                population = initNewPopulation(popSize, lastSetStrength);
            }
            else if (selectedSamples.Count == 1)
            {
                GraphManager parent = selectedSamples[0];
                population = generateNewGeneration(popSize, parent);
            }
            else if (selectedSamples.Count == 2)
            {
                GraphManager parent1 = selectedSamples[0];
                GraphManager parent2 = selectedSamples[1];
                population = generateNewGeneration(popSize, parent1, parent2);
            }
            else if (selectedSamples.Count > 2)
            {
                List<GraphManager> parents = new List<GraphManager>(selectedSamples);
                population = generateNewGeneration(popSize, parents);

            }
            generation++;
            selectedSamples = new List<GraphManager>();
        }

        private List<GraphManager> initNewPopulation(int size, MutationStrength strength)
        {
            List<GraphManager> generatedGraphs = new List<GraphManager>(size);
            string generationFolderPath = createGenerationFolder();
            for (int i = 0; i < size; i++)
            {
                GraphType gt;
                gt = UnityEngine.Random.Range(0f, 1f) > 0.5f ? GraphType.Lit : GraphType.Unlit;
                generatedGraphs.Add(GraphInterface.LaunchAndGet(generationFolderPath, i, gt, baseName));
            }
            foreach (GraphManager graph in generatedGraphs)
            {
                graph.initGenes(strength, allowGraphExpansion, allowTypeChange, expansionProbability, typeChangeProbability);
                //graph.addMultipleCompatibleNodes(mutationsAtInit);
                graph.mutateGraph(mutationsAtInit);
                //graph.randomizeGraphInputs();
                graph.generateRandomInputs();

            }
            return generatedGraphs;
        }

        // Single Parent Generation
        private List<GraphManager> generateNewGeneration(int size, GraphManager parentGraph)
        {
            GeneticHandler parentGenes = parentGraph.getGenes();
            List<GraphManager> generatedChildren = new List<GraphManager>(size);
            string generationFolderPath = createGenerationFolder();
            for (int i = 0; i < size; i++)
            {
                GraphType gt;
                GeneticHandler childGenes = new GeneticHandler(parentGenes);
                gt = childGenes.graphType;

                childGenes.graphType = gt;
                generatedChildren.Add(GraphInterface.LaunchAndGet(generationFolderPath, i, gt, baseName));
                generatedChildren[i].setGenes(childGenes);
            }
            foreach (GraphManager graph in generatedChildren)
            {

                graph.inheritFromGraph(parentGraph);
                if (graph.getGenes().expandGraph)
                {
                    graph.mutateGraph(1);
                    graph.getGenes().expandGraph = false;
                }
                graph.generateRandomInputs();
            }
            return generatedChildren;
        }

        private List<GraphManager> generateNewGeneration(int size, GraphManager parent1, GraphManager parent2)
        {
            GeneticHandler genes1 = parent1.getGenes();
            GeneticHandler genes2 = parent2.getGenes();
            List<GraphManager> generatedChildren = new List<GraphManager>(size);
            string generationFolderPath = createGenerationFolder();
            for (int i = 0; i < size; i++)
            {
                GraphType gt;
                GeneticHandler childGenes = new GeneticHandler(genes1, genes2);
                gt = childGenes.graphType;
                Debug.LogFormat("Graph type is {0}", gt);
                generatedChildren.Add(GraphInterface.LaunchAndGet(generationFolderPath, i, gt, baseName));
                generatedChildren[i].setGenes(childGenes);
            }
            foreach (GraphManager graph in generatedChildren)
            {
                graph.inheritFromGraphs(parent1, parent2);
                if (graph.getGenes().expandGraph)
                {
                    graph.mutateGraph(1);
                    graph.getGenes().expandGraph = false;
                }
                graph.generateRandomInputs();
            }
            return generatedChildren;
        }
        private List<GraphManager> generateNewGeneration(int size, List<GraphManager> parents)
        {
            List<GraphManager> generatedChildren = new List<GraphManager>(size);
            string generationFolderPath = createGenerationFolder();
            List<Vector2Int> parentCouples = new List<Vector2Int>(size);
            for (int i = 0; i < size; i++)
            {
                int p1 = UnityEngine.Random.Range(0, parents.Count);
                int p2 = UnityEngine.Random.Range(0, parents.Count);
                parentCouples.Add(new Vector2Int(p1, p2));
                while (p1 == p2)
                {
                    p2 = UnityEngine.Random.Range(0, parents.Count);
                }
                GraphManager parent1 = parents[p1];
                GraphManager parent2 = parents[p2];
                GeneticHandler genes1 = parent1.getGenes();
                GeneticHandler genes2 = parent2.getGenes();
                GraphType gt;
                GeneticHandler childGenes = new GeneticHandler(genes1, genes2);
                gt = childGenes.graphType;

                generatedChildren.Add(GraphInterface.LaunchAndGet(generationFolderPath, i, gt, baseName));
                generatedChildren[i].setGenes(childGenes);
            }
            for (int i = 0; i < size; i++)
            {
                GraphManager graph = generatedChildren[i];
                graph.inheritFromGraphs(parents[parentCouples[i].x], parents[parentCouples[i].y]);
                if (graph.getGenes().expandGraph)
                {
                    graph.mutateGraph(1);
                    graph.getGenes().expandGraph = false;
                }
                graph.generateRandomInputs();
            }
            return generatedChildren;

        }

        private string createGenerationFolder()
        {
            string genFolderName = string.Format("gen_{0}", generation);
            //Debug.Log(graphFolder);
            string folderGUID = AssetDatabase.CreateFolder(graphFolder, genFolderName);
            if (folderGUID == "") throw new FileNotFoundException("Tried creating generation folder but failed");
            return AssetDatabase.GUIDToAssetPath(folderGUID);

        }

        public List<GraphManager> getPopulation()
        {
            return new List<GraphManager>(population);
        }

        internal void setNewMutationStrength(MutationStrength currentMutationStrength)
        {
            lastSetStrength = currentMutationStrength;
            foreach (GraphManager graph in population)
            {
                if (graph.getGenes().mutationStrength == currentMutationStrength) break;
                graph.getGenes().mutationStrength = currentMutationStrength;
            }
        }

        internal void setNewTypeChangeAllow(bool newAllowTypeChange)
        {
            allowTypeChange = newAllowTypeChange;
            foreach (GraphManager graph in population)
            {
                if (graph.getGenes().changeTypeAllowed == allowTypeChange)
                {
                    Debug.Log("Already set to " + allowTypeChange);
                    break;
                }
                graph.getGenes().changeTypeAllowed = allowTypeChange;
            }
        }

        internal void setNewExpandAllow(bool newAllowGraphExpansion)
        {
            allowGraphExpansion = newAllowGraphExpansion;
            foreach (GraphManager graph in population)
            {
                if (graph.getGenes().expandAllowed == allowGraphExpansion) break;
                graph.getGenes().expandAllowed = allowGraphExpansion;
            }
        }

        internal void setNewExpandProbability(float newExpansionProbability)
        {
            expansionProbability = newExpansionProbability;
            foreach (GraphManager graph in population)
            {
                if (graph.getGenes().expansionProbability == expansionProbability) break;
                graph.getGenes().expansionProbability = expansionProbability;
            }
        }

        internal void setNewTypeChangeProbability(float newTypeChangeProbability)
        {
            typeChangeProbability = newTypeChangeProbability;
            foreach (GraphManager graph in population)
            {
                if (graph.getGenes().typeChangeProbability == typeChangeProbability) break;
                graph.getGenes().typeChangeProbability = typeChangeProbability;
            }
        }
    }

    public static class Utilities
    {
        
        public static string generateFilePath(string folderPath, string baseName, string ID)
        {
            return string.Format("{0}/{1}_{2}", folderPath, baseName, ID);
        }

    }



    public static class GraphInterface
    {
        public const string sgext = "shadergraph";
        public static void Launch(int ID)
        {
            BlankGraphGenerator.Generate("graph");

        }
        public static void Launch(string path, int ID,GraphType type,string baseName)
        {
            switch (type)
            {
                case GraphType.Blank:
                    BlankGraphGenerator.Generate(path, ID,baseName);
                    break;
                case GraphType.Lit:
                    LitGraphGenerator.Generate(path, ID, baseName);
                    break;
                case GraphType.Unlit:
                    UnlitGraphGenerator.Generate(path, ID, baseName);
                    break;
                default:
                    BlankGraphGenerator.Generate(path, ID, baseName);
                    break;

            }
        }

        public static GraphManager LaunchAndGet(string path, int ID, GraphType type, string baseName)
        {
            Launch(path, ID, type, baseName);
            string filePath = string.Format("{0}/{1}_{2}.{3}", path, baseName, ID,sgext);
            //Debug.Log(string.Format("{0}_{1}.{2} --- {3} ", baseName, ID, sgext, AssetDatabase.AssetPathToGUID(filePath)));
            return initGraph(filePath,false,type);
        }

        public static void readGraphJsonFile(string path)
        {
            GraphInfoReader.openGraphJson(path);
        }

        public static void openGraph(string path)
        {
            graphInspectorLauncher.openGraphInInspector(path);
        }

        public static GraphManager initGraph(string pathOrGUID,bool isGUID,GraphType type)
        {
            GraphManager gm = new GraphManager(pathOrGUID, isGUID,type);
            return gm;
        }

    }


    public class ShaderPreviewEditor : EditorWindow
    {
        private static float buttonOffsetY = 25f;

        private Vector2 pageButtonOffset;
        private Vector2 extraCellOffset;

        private float minimumCellWidth = 100f;
        private float minimumCellHeight = 50f;
        private Vector2 windowSize;
        private Vector2 cellSize;
        private int cellCountPerPage;
        private int maxCountPerPage = 1;
        private bool layoutIsOrganized = false;
        private int numberOfPages = 0;
        private int selectedPageIndex = 0;

        private List<VisualElement> cells;
        public List<int> selectedCells
        {
            get;
            private set;
        }

        public bool selectorIsActive = false;
        public bool legacyPreview = false;

        public int activeStageIndex {
            get;
            private set;
        } = 0;


        private void Awake()
        {
            cells = new List<VisualElement>();
            selectedCells = new List<int>();
            windowSize = position.size;
            cellSize = windowSize;
            layoutIsOrganized = false;
            cellCountPerPage = 0;
            pageButtonOffset = new Vector2(0f, buttonOffsetY);
            extraCellOffset = new Vector2(0f, 0f);
        }

        private void OnDestroy()
        {
            foreach (VisualElement cell in cells)
            {
                try
                {
                    IDisposable disposable = (IDisposable)cell;
                    disposable.Dispose();
                }
                catch {
                }
            }
            cells = null;
            
        }

        private void OnGUI()
        {
            if (numberOfPages > 1)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(buttonOffsetY));
                if (GUILayout.Button("Previous Page", GUILayout.ExpandWidth(true)))
                {
                    selectedPageIndex = Mathf.Max(1, selectedPageIndex - 1);
                }
                if (GUILayout.Button("Next Page", GUILayout.ExpandWidth(true)))
                {
                    selectedPageIndex = Mathf.Min(selectedPageIndex + 1, numberOfPages);
                }
                GUILayout.EndHorizontal();
                
            }
            else
            {
                selectedPageIndex = 1;
            }
            if (!legacyPreview)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(buttonOffsetY));
                if (GUILayout.Button("Previous Stage", GUILayout.ExpandWidth(true)))
                {
                    activeStageIndex = StageSelector.getUsableStageIndex(activeStageIndex - 1);
                }
                if (GUILayout.Button("Next Stage", GUILayout.ExpandWidth(true)))
                {
                    activeStageIndex = StageSelector.getUsableStageIndex(activeStageIndex + 1);
                }
                GUILayout.EndHorizontal();
                extraCellOffset = new Vector2(0f, buttonOffsetY);
            }
            if (selectorIsActive)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(buttonOffsetY));
                if (GUILayout.Button(string.Format("{1} for graph {0} next generation", Mathf.Clamp(selectedPageIndex - 1, 0, cells.Count), selectedCells.Contains(selectedPageIndex - 1) ? "Unselect" : "Select"), GUILayout.ExpandWidth(true)))
                {
                    int targetIndex = Mathf.Clamp(selectedPageIndex - 1, 0, cells.Count);
                    if (!selectedCells.Contains(targetIndex))
                        selectedCells.Add(targetIndex);
                    else
                    {
                        selectedCells.Remove(targetIndex);
                        selectedCells.RemoveAll(item => item == targetIndex);
                    }
                }
                GUILayout.EndHorizontal();
                extraCellOffset = new Vector2(0f, buttonOffsetY * 2f);
            }
        }

        private void Update()
        {
            // Check if the size of the window is the same as before
            if (windowSize==null || windowSize.magnitude != position.size.magnitude || cellCountPerPage == 0)
            {
                layoutIsOrganized = false;
                windowSize = position.size;
            }
            if (cells == null)
            {
                Close();
                return;
            }
            bool loadCells = cells.Count == 0;
            int maxTries = 100;
            if (rootVisualElement.contentContainer.childCount > 0 && !layoutIsOrganized)
            {
                numberOfPages = 1;
                if (loadCells)
                {
                    foreach (VisualElement element in rootVisualElement.contentContainer.Children())
                    {
                        if (element.GetType().Name.Contains("MasterPreviewView") || element.GetType().Name.Contains("MasterShaderPreview"))
                        {
                            cells.Add(element);
                        }
                    }
                }
                cellCountPerPage = Mathf.Min(cells.Count, maxCountPerPage);
                cellSize = CellUtilities.getMaxCellSize(position.size, cellCountPerPage);
                numberOfPages = Mathf.CeilToInt(cells.Count / cellCountPerPage);
                //Debug.LogFormat("Number of pages computed is {0}", numberOfPages);
                int tries = 0;
                //Debug.Log("Before Computing Layout {0}", numberOfPages);
                while ((cellSize.x < minimumCellWidth || cellSize.y < minimumCellHeight) && tries < maxTries && cellCountPerPage > 0)
                {
                    cellCountPerPage = Mathf.Max(cellCountPerPage / 2, 1);
                    numberOfPages = Mathf.CeilToInt(cells.Count / cellCountPerPage);
                    cellSize = CellUtilities.getMaxCellSize(position.size, cellCountPerPage);
                    tries += 1;
                    //Debug.Log(Computing Layout {0}", numberOfPages);
                }
                selectedPageIndex = (int)Mathf.Clamp(selectedPageIndex, 1, numberOfPages);


                if (numberOfPages > 1) cellSize.y -= pageButtonOffset.y;
                cellSize.y -= extraCellOffset.y;
                
                //Debug.Log(pageNumber);
                if (tries == maxTries || cellSize.x < minimumCellWidth || cellSize.y < minimumCellHeight)
                {
                    Debug.LogError("Max tries reached");
                    Debug.Log(cellSize);
                    Debug.Log(numberOfPages);
                    Debug.Log(cellCountPerPage);
                }
            }
            int rows = Mathf.CeilToInt(position.size.x / cellSize.x);
            int columns = Mathf.CeilToInt(position.size.y / cellSize.y);
            int columnsFilled = 0;
            int rowsFilled = 0;
            float offsetX = 0f;
            float offsetY = numberOfPages>1?pageButtonOffset.y : 0f;
            offsetY += extraCellOffset.y;
            int i = 0;
            foreach(VisualElement element in cells)
            {
                //Debug.Log(rootVisualElement.contentContainer.childCount);
                if(numberOfPages > 1)
                {
                    if (i>=(selectedPageIndex)*cellCountPerPage || i < (cellCountPerPage * (selectedPageIndex-1)))
                    {
                        element.style.visibility = Visibility.Hidden;
                        element.SendToBack();
                        i++;
                        continue;
                    }
                }
                if (element.GetType().Name.Contains("MasterPreviewView") || element.GetType().Name.Contains("MasterShaderPreview"))
                {
                    element.style.visibility = Visibility.Visible;
                    element.BringToFront();
                    element.style.left = offsetX;
                    element.style.top = offsetY;
                    element.style.width = cellSize.x;
                    element.style.height = cellSize.y;

                    /*
                    Debug.LogFormat("Cell {0}", i);
                    Debug.Log(element.style.width);
                    Debug.Log(element.style.height);
                    */
                    i++;
                    columnsFilled += 1;
                    offsetX += cellSize.x;
                    if(columnsFilled >= columns)
                    {
                        columnsFilled = 0;
                        rowsFilled += 1;
                        offsetY += cellSize.y;
                        offsetX = 0f;
                    }
                    if(rowsFilled > rows)
                    {
                        break;
                    }
                }
                else
                {
                    Debug.LogErrorFormat("Child is of type {0}", element.GetType().Name);
                }
            }
            layoutIsOrganized = true;
        }
    }


    public static class CellUtilities
    {


        public static Vector2 getMaxCellSize(Vector2 windowSize,int cellCount)
        {
            return getMaxCellSize(windowSize.x, windowSize.y,cellCount);
        }
        public static Vector2 getMaxCellSize(float windowWidth,float windowHeight,int cellCount)
        {
            if (cellCount == 1) return new Vector2(windowWidth, windowHeight);
            float windowSizeRatio = windowWidth / windowHeight;
            Vector2 cellSize = new Vector2(windowWidth/cellCount,windowHeight/cellCount);
            // If wide more than high
            if (windowSizeRatio > 1.0f)
            {
                bool cellsFit = false;
                int cellsPerColumn = 1;
                while (!cellsFit && cellSize.x >20f)
                {
                    float cellHeight = windowHeight /cellsPerColumn;
                    float cellWidth = cellHeight * windowSizeRatio;
                    cellsFit = ( Mathf.CeilToInt(((float)cellCount) / cellsPerColumn) * cellWidth) < windowWidth;
                    //Debug.LogFormat("Cell Row size:{0}",Mathf.CeilToInt(((float)cellCount) / cellsPerColumn) * cellWidth);
                    //Debug.LogFormat("Window width:{0}",windowWidth);
                    cellSize.x = cellWidth;
                    cellSize.y = cellHeight;
                    cellsPerColumn += 1;
                }
                if (cellSize.x <= 20f)
                {
                    Debug.LogError("Inadmissible Size");
                }
                return cellSize;
            }
            // if high more than wide
            else
            {
                bool cellsFit = false;
                int cellsPerRow = 1;
                while (!cellsFit && cellSize.y > 20f)
                {
                    float cellWidth = windowWidth /cellsPerRow;
                    float cellHeight = cellWidth / windowSizeRatio;
                    cellsFit = (Mathf.CeilToInt(((float)cellCount )/ cellsPerRow) * cellHeight) < windowHeight;
                    //Debug.LogFormat("Cell Column size:{0}", Mathf.CeilToInt(((float)cellCount) / cellsPerRow) * cellHeight);
                    //Debug.LogFormat("Window height:{0}", windowHeight);
                    cellSize.x = cellWidth;
                    cellSize.y = cellHeight;
                    cellsPerRow += 1;
                }
                if (cellSize.y <= 20f)
                {
                    Debug.LogError("Inadmissible Size");
                }
                return cellSize;
            }
        }
    }

}

