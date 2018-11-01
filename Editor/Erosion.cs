using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.TerrainAPI;

public class Erosion : EditorWindow {
    /*
    class ErosionOp {
        enum ErosionOpType {
            Hydraulic,
            Thermal,
            Wind
        };

        public virtual void Execute() = 0;
    }

    class ErosionStack {
        List<ErosionOp> m_Ops = new List<ErosionOp>();
    }
    */

    private bool m_Init = false;

    private ComputeShader m_ComputeShader =   null;
    private Terrain m_TerrainTile =           null;
    private Texture2D m_PrecipitationMask =   null;
    private Texture2D m_HeightInput =         null;

    //decent default values here
    private int m_NumHydraulicIterations =    300;
    private int m_NumThermalIterations =      10;

    private float m_DeltaTime =               0.01f;
    private float m_PrecipRate =              0.1f;
    private float m_FlowRate =               -0.07f;
    private float m_SedimentCapacity =        50.0f;
    private float m_SedimentDissolveRate =    0.01f;
    private float m_SedimentDepositRate =     0.01f;
    private float m_EvaporationRate =         0.0001f;
    private float m_SmoothingFactor =         0.05f;

    //thermal erosion
    private float m_AngleOfRepose =           35.0f; //in degrees

    private int[] m_texDim = { 256, 256 };

    private RenderTexture m_PrecipMaskRT;

    private RenderTexture m_TerrainHeightRT;
    private RenderTexture m_TerrainHeightPrevRT;

    private RenderTexture m_WaterRT;
    private RenderTexture m_WaterPrevRT;

    private RenderTexture m_WaterVelRT;
    private RenderTexture m_WaterVelPrevRT;

    private RenderTexture m_FluxRT;
    private RenderTexture m_FluxPrevRT;

    private RenderTexture m_SedimentRT;
    private RenderTexture m_SedimentPrevRT;
    //private RenderTexture m_TerrainNormalsRT;

    private int tab = 0;

    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Erosion")]
    static void Init() {
        Erosion window = (Erosion)EditorWindow.GetWindow(typeof(Erosion));
        window.Show();
    }

    void InitData() {
        if(m_Init == true) { return; }

        m_texDim[0] = m_texDim[1] = m_TerrainTile.terrainData.heightmapResolution;
        Debug.Log("Initializing textures at " + m_texDim[0]);

        m_PrecipMaskRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_PrecipMaskRT.enableRandomWrite = true;
        m_PrecipMaskRT.Create();

        m_TerrainHeightRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_TerrainHeightRT.format = RenderTextureFormat.RFloat;
        m_TerrainHeightRT.enableRandomWrite = true;
        m_TerrainHeightRT.Create();

        m_TerrainHeightPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_TerrainHeightPrevRT.format = RenderTextureFormat.RFloat;
        m_TerrainHeightPrevRT.enableRandomWrite = true;
        m_TerrainHeightPrevRT.Create();

        //Water level
        m_WaterRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterRT.format = RenderTextureFormat.RFloat;
        m_WaterRT.enableRandomWrite = true;
        m_WaterRT.Create();

        m_WaterPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterPrevRT.format = RenderTextureFormat.RFloat;
        m_WaterPrevRT.enableRandomWrite = true;
        m_WaterPrevRT.Create();

        //water velocity
        m_WaterVelRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterVelRT.format = RenderTextureFormat.RGFloat;
        m_WaterVelRT.enableRandomWrite = true;
        m_WaterVelRT.Create();

        m_WaterVelPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterVelPrevRT.format = RenderTextureFormat.RGFloat;
        m_WaterVelPrevRT.enableRandomWrite = true;
        m_WaterVelPrevRT.Create();

        //flux
        m_FluxRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_FluxRT.format = RenderTextureFormat.ARGBFloat;
        m_FluxRT.enableRandomWrite = true;
        m_FluxRT.Create();

        m_FluxPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_FluxPrevRT.format = RenderTextureFormat.ARGBFloat;
        m_FluxPrevRT.enableRandomWrite = true;
        m_FluxPrevRT.Create();

        //sediment
        m_SedimentRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_SedimentRT.format = RenderTextureFormat.RFloat;
        m_SedimentRT.enableRandomWrite = true;
        m_SedimentRT.Create();

        m_SedimentPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_SedimentPrevRT.format = RenderTextureFormat.RFloat;
        m_SedimentPrevRT.enableRandomWrite = true;
        m_SedimentPrevRT.Create();

        //we did all the things!
        m_Init = true;
    }

    void ReleaseData() {
        if (m_Init) {
            m_PrecipMaskRT.Release();

            m_TerrainHeightRT.Release();
            m_TerrainHeightPrevRT.Release();

            m_WaterRT.Release();
            m_WaterPrevRT.Release();

            m_WaterVelRT.Release();
            m_WaterVelPrevRT.Release();

            m_FluxRT.Release();
            m_FluxPrevRT.Release();

            m_SedimentRT.Release();
            m_SedimentPrevRT.Release();

            //m_TerrainNormalsRT.Release();

            m_Init = false;
        }
    }

    void PrepareTextureData() {
        if(m_Init) {
            //Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_TerrainHeightRT);
            //Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_TerrainHeightPrevRT);
            Graphics.Blit(m_HeightInput, m_TerrainHeightRT);
            Graphics.Blit(m_HeightInput, m_TerrainHeightPrevRT);
            Graphics.Blit(m_PrecipitationMask, m_PrecipMaskRT);
        }
    }

    void SwapBuffers(RenderTexture a, RenderTexture b) {
        
        RenderTexture tmp = RenderTexture.GetTemporary(m_texDim[0], m_texDim[1], 0, a.format);

        Graphics.Blit(a, tmp);
        Graphics.Blit(b, a); //possibly only need to do the first blit, since we don't care about previous data
        Graphics.Blit(tmp, b);
        
        RenderTexture.ReleaseTemporary(tmp);
    }

    void Simulate() {
        if (m_ComputeShader != null) {
            InitData();
            PrepareTextureData();

            int[] numWorkGroups = { 8, 8, 1 };
            int hydraulicKernelIdx = m_ComputeShader.FindKernel("HydraulicErosion");
            int thermalKernelIdx = m_ComputeShader.FindKernel("ThermalErosion");
            int waterFlowKernelIdx = m_ComputeShader.FindKernel("SimulateWaterFlow");
            int diffuseHeightKernelIdx = m_ComputeShader.FindKernel("DiffuseHeight");

            float m = Mathf.Tan(m_AngleOfRepose * Mathf.Deg2Rad);

            Debug.Log(m_TerrainTile.terrainData.size);

            //global parameters
            m_ComputeShader.SetVector("texDim", new Vector4(m_texDim[0], m_texDim[1], 0.0f, 0.0f));
            m_ComputeShader.SetVector("terrainDim", new Vector4(m_TerrainTile.terrainData.size.x, m_TerrainTile.terrainData.size.y, m_TerrainTile.terrainData.size.z));
            m_ComputeShader.SetFloat("dt", m_DeltaTime);
            m_ComputeShader.SetFloat("precipRate", m_PrecipRate);
            m_ComputeShader.SetFloat("flowRate", m_FlowRate);
            m_ComputeShader.SetFloat("sedimentCapacity", m_SedimentCapacity);
            m_ComputeShader.SetFloat("sedimentDissolveRate", m_SedimentDissolveRate);
            m_ComputeShader.SetFloat("sedimentDepositRate", m_SedimentDepositRate);
            m_ComputeShader.SetFloat("evaporationRate", m_EvaporationRate);
            m_ComputeShader.SetInt("numThermalIterations", m_NumThermalIterations);
            m_ComputeShader.SetFloat("angleOfRepose", m);

            //water flow simulation
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "PrecipMask", m_PrecipMaskRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "Water", m_WaterRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "WaterPrev", m_WaterPrevRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "WaterVel", m_WaterVelRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "WaterVelPrev", m_WaterVelPrevRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "Flux", m_FluxRT);
            m_ComputeShader.SetTexture(waterFlowKernelIdx, "FluxPrev", m_FluxPrevRT);

            //set up textures for sediment transport + erosion
            //presumably we've already calculated our water levels and velocities in the
            //previous step, and no longer need their previous values
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeight", m_TerrainHeightRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Water", m_WaterRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "WaterPrev", m_WaterPrevRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "WaterVel", m_WaterVelRT);
            //m_ComputeShader.SetTexture(hydraulicKernelIdx, "WaterVelPrev", m_WaterVelPrevRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Flux", m_FluxRT);
            //m_ComputeShader.SetTexture(hydraulicKernelIdx, "FluxPrev", m_FluxPrevRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Sediment", m_SedimentRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "SedimentPrev", m_SedimentPrevRT);

            //thermal erosion shader textures
            m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeight", m_TerrainHeightRT);
            m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);

            //diffuse height parameters
            m_ComputeShader.SetTexture(diffuseHeightKernelIdx, "TerrainHeight", m_TerrainHeightRT);
            m_ComputeShader.SetTexture(diffuseHeightKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);


            for (int i = 0; i < m_NumHydraulicIterations; i++) {
                float rainScale = Mathf.Lerp(100.0f, 500.0f, Random.value);
                float rainPos = rainScale * Random.value;
                m_ComputeShader.SetFloat("rainPosition", rainPos);
                m_ComputeShader.Dispatch(waterFlowKernelIdx, m_texDim[0] / numWorkGroups[0], m_texDim[1] / numWorkGroups[1], numWorkGroups[2]);
                m_ComputeShader.Dispatch(hydraulicKernelIdx, m_texDim[0] / numWorkGroups[0], m_texDim[1] / numWorkGroups[1], numWorkGroups[2]);
                
                //curr -> prev (and we don't care what curr is after this because we only write to it)
                Graphics.Blit(m_TerrainHeightRT, m_TerrainHeightPrevRT);
                Graphics.Blit(m_SedimentRT, m_SedimentPrevRT);
                Graphics.Blit(m_WaterRT, m_WaterPrevRT);
                Graphics.Blit(m_WaterVelRT, m_WaterVelPrevRT);
                Graphics.Blit(m_FluxRT, m_FluxPrevRT);
            }

            for (int j = 0; j < m_NumThermalIterations; j++) {
                m_ComputeShader.Dispatch(thermalKernelIdx, m_texDim[0] / numWorkGroups[0], m_texDim[1] / numWorkGroups[1], numWorkGroups[2]);
                //SwapBuffers(m_TerrainHeightRT, m_TerrainHeightPrevRT);
                Graphics.Blit(m_TerrainHeightRT, m_TerrainHeightPrevRT);
            }

            Graphics.Blit(m_TerrainHeightRT, m_TerrainTile.terrainData.heightmapTexture);
            m_TerrainTile.terrainData.UpdateDirtyRegion(0, 0, m_TerrainTile.terrainData.heightmapTexture.width, m_TerrainTile.terrainData.heightmapTexture.height, true);
        }
    }

    void OnGUIHydraulic() {
        m_ComputeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", m_ComputeShader, typeof(ComputeShader));
        m_TerrainTile = (Terrain)EditorGUILayout.ObjectField("Terrain Tile", m_TerrainTile, typeof(Terrain));
        m_HeightInput = (Texture2D)EditorGUILayout.ObjectField("Input Heightfield", m_HeightInput, typeof(Texture2D));
        m_PrecipitationMask = (Texture2D)EditorGUILayout.ObjectField("Precipitation Mask", m_PrecipitationMask, typeof(Texture2D));
        m_NumHydraulicIterations = EditorGUILayout.IntField("# Iterations", m_NumHydraulicIterations);
        m_DeltaTime = EditorGUILayout.FloatField("Simulation time step", m_DeltaTime);
        m_PrecipRate = EditorGUILayout.FloatField("Precipitation Rate", m_PrecipRate);
        m_FlowRate = EditorGUILayout.FloatField("Flow Rate", m_FlowRate);
        m_SedimentCapacity = EditorGUILayout.FloatField("Sediment Capacity", m_SedimentCapacity);
        m_SedimentDissolveRate = EditorGUILayout.FloatField("Sediment Dissolve Rate", m_SedimentDissolveRate);
        m_SedimentDepositRate = EditorGUILayout.FloatField("Sediment Deposit Rate", m_SedimentDepositRate);
        m_EvaporationRate = EditorGUILayout.FloatField("Evaporation Rate", m_EvaporationRate);

        if (GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }

        if (GUILayout.Button("Execute")) {
            Simulate();
        }
    }

    void OnGUIThermal() {
        m_NumThermalIterations = EditorGUILayout.IntField("# Iterations", m_NumThermalIterations);
        m_SmoothingFactor = EditorGUILayout.FloatField("Smoothing Factor", m_SmoothingFactor);
        m_AngleOfRepose = EditorGUILayout.FloatField("Angle of Repose", m_AngleOfRepose);
    }

    void OnGUIDebug() {
        int dy = 60;
        int x = 0;
        int y = dy;

        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Level");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterRT);
        x += 256 + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Velocity");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterVelRT);
        x += 256 + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Flux");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_FluxRT);

        //bottom row
        x = 0;
        y += 256 + 30;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Sediment");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_SedimentRT);
        x += 256 + 2; y = dy + 256 + 30;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Output Height");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_TerrainHeightRT);
    }

    void OnGUI() {
        tab = GUILayout.Toolbar(tab, new string[] { "Hydraulic", "Thermal", "Debug" });//"Wind", "Tectonics", "Volcanic" });

        switch(tab) {
            case 0:
                OnGUIHydraulic();
                break;
            case 1:
                OnGUIThermal();
                break;
            case 2:
                OnGUIDebug();
                break;
            default:
                break;
        }

        this.Repaint();
    }
}