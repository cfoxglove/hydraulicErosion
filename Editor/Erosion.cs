using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.TerrainAPI;

public class Erosion : EditorWindow {
    #region parameters
    private bool m_Init = false;

    private ComputeShader m_ComputeShader =   null;
    private Terrain m_TerrainTile =           null;
    private Texture2D m_PrecipitationMask =   null;
    private Texture2D m_HeightInput =         null;
    private Texture2D m_ReposeMask =          null;
    private Texture2D m_CollisionMask =       null;

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
    #endregion

    //thermal erosion
    private Vector2 m_AngleOfRepose =         new Vector2(35.0f, 35.0f); //in degrees

    private int[] m_texDim = { 256, 256 };

    #region render textures
    private RenderTexture m_PrecipMaskRT;
    private RenderTexture m_ReposeMaskRT;
    private RenderTexture m_CollisionRT;

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
    #endregion

    private int tab = 0;

    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Erosion")]
    static void Init() {
        Erosion window = (Erosion)EditorWindow.GetWindow(typeof(Erosion));
        window.Show();
    }

    #region alloc / dealloc
    void InitData() {
        if(m_Init == true) { return; }

        m_texDim[0] = m_texDim[1] = m_TerrainTile.terrainData.heightmapResolution;
        Debug.Log("Initializing textures at " + m_texDim[0]);

        m_PrecipMaskRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_PrecipMaskRT.enableRandomWrite = true;
        m_PrecipMaskRT.Create();

        m_CollisionRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_CollisionRT.enableRandomWrite = true;
        m_CollisionRT.format = RenderTextureFormat.RFloat;
        m_CollisionRT.Create();

        m_ReposeMaskRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_ReposeMaskRT.enableRandomWrite = true;
        m_ReposeMaskRT.Create();

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
            m_ReposeMaskRT.Release();
            m_CollisionRT.Release();

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
    #endregion

    #region sim
    ComputeShader GetComputeShader() {
        if (m_ComputeShader == null) {
            m_ComputeShader = (ComputeShader)Resources.Load("Erosion");
        }
        return m_ComputeShader;
    }

    void PrepareTextureData() {
        if(m_Init) {
            if (m_HeightInput == null) {
                Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_TerrainHeightRT);
                Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_TerrainHeightPrevRT);
            } else {
                Debug.Log("Using texture as heightfield");
                Graphics.Blit(m_HeightInput, m_TerrainHeightRT);
                Graphics.Blit(m_HeightInput, m_TerrainHeightPrevRT);
            }
            Graphics.Blit(m_PrecipitationMask, m_PrecipMaskRT);
            Graphics.Blit(m_ReposeMask, m_ReposeMaskRT);
            Graphics.Blit(m_CollisionMask, m_CollisionRT);
        }
    }

    void Simulate() {
        m_ComputeShader = GetComputeShader();
        if (m_ComputeShader != null) {
            InitData();
            PrepareTextureData();

            int[] numWorkGroups = { 8, 8, 1 };
            int hydraulicKernelIdx = m_ComputeShader.FindKernel("HydraulicErosion");
            int thermalKernelIdx = m_ComputeShader.FindKernel("ThermalErosion");
            int waterFlowKernelIdx = m_ComputeShader.FindKernel("SimulateWaterFlow");
            int diffuseHeightKernelIdx = m_ComputeShader.FindKernel("DiffuseHeight");

            //precompute some values on the CPU (constants in the shader)
            Vector2 m = new Vector2(Mathf.Tan(m_AngleOfRepose.x * Mathf.Deg2Rad), Mathf.Tan(m_AngleOfRepose.y * Mathf.Deg2Rad));
            float dx = m_texDim[0] / m_TerrainTile.terrainData.size.x;
            float dy = m_texDim[1] / m_TerrainTile.terrainData.size.y;
            float dxy = Mathf.Sqrt(dx * dx + dy * dy);

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

            m_ComputeShader.SetVector("dxdy", new Vector4(dx, dy, dxy));
            m_ComputeShader.SetVector("angleOfRepose", new Vector4(m.x, m.y, 0.0f, 0.0f));

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
            m_ComputeShader.SetTexture(thermalKernelIdx, "Sediment", m_SedimentRT);
            m_ComputeShader.SetTexture(thermalKernelIdx, "SedimentPrev", m_SedimentPrevRT);
            m_ComputeShader.SetTexture(thermalKernelIdx, "ReposeMask", m_ReposeMaskRT);
            m_ComputeShader.SetTexture(thermalKernelIdx, "Collision", m_CollisionRT);

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
                Graphics.Blit(m_TerrainHeightRT, m_TerrainHeightPrevRT);
                Graphics.Blit(m_SedimentRT, m_SedimentPrevRT);
            }


            //copy our final height back to the terrain height buffer
            Graphics.Blit(m_TerrainHeightRT, m_TerrainTile.terrainData.heightmapTexture);
            m_TerrainTile.terrainData.UpdateDirtyRegion(0, 0, m_TerrainTile.terrainData.heightmapTexture.width, m_TerrainTile.terrainData.heightmapTexture.height, false);
            m_TerrainTile.ApplyDelayedHeightmapModification();
        }
    }
    #endregion

    #region GUI
    void OnGUIHydraulic() {
        m_TerrainTile = (Terrain)EditorGUILayout.ObjectField("Terrain Tile", m_TerrainTile, typeof(Terrain));
        m_HeightInput = (Texture2D)EditorGUILayout.ObjectField("Input Heightfield", m_HeightInput, typeof(Texture2D));
        m_PrecipitationMask = (Texture2D)EditorGUILayout.ObjectField("Precipitation Mask", m_PrecipitationMask, typeof(Texture2D));
        m_CollisionMask = (Texture2D)EditorGUILayout.ObjectField("Collision Mask", m_CollisionMask, typeof(Texture2D));
        m_NumHydraulicIterations = EditorGUILayout.IntField("# Iterations", m_NumHydraulicIterations);
        m_DeltaTime = EditorGUILayout.FloatField("Simulation time step", m_DeltaTime);
        m_PrecipRate = EditorGUILayout.FloatField("Precipitation Rate", m_PrecipRate);
        m_FlowRate = EditorGUILayout.FloatField("Flow Rate", m_FlowRate);
        m_SedimentCapacity = EditorGUILayout.FloatField("Sediment Capacity", m_SedimentCapacity);
        m_SedimentDissolveRate = EditorGUILayout.FloatField("Sediment Dissolve Rate", m_SedimentDissolveRate);
        m_SedimentDepositRate = EditorGUILayout.FloatField("Sediment Deposit Rate", m_SedimentDepositRate);
        m_EvaporationRate = EditorGUILayout.FloatField("Evaporation Rate", m_EvaporationRate);
    }

    void OnGUIThermal() {
        m_NumThermalIterations = EditorGUILayout.IntField("# Iterations", m_NumThermalIterations);
        m_SmoothingFactor = EditorGUILayout.FloatField("Smoothing Factor", m_SmoothingFactor);
        EditorGUILayout.MinMaxSlider("Angle of Repose", ref m_AngleOfRepose.x, ref m_AngleOfRepose.y, 0.0f, 90.0f);
        m_ReposeMask = (Texture2D)EditorGUILayout.ObjectField("Repose Mask", m_ReposeMask, typeof(Texture2D));
    }

    void OnGUIDebug() {
        int dy = 100;
        int x = 0;
        int y = dy;

        if (m_Init) {

            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Level");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterRT);
            x += 256 + 2; y = dy;
            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Velocity");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterVelRT);
            x += 256 + 2; y = dy;
            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Flux");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_FluxRT);

            //bottom row
            int rowY = dy + 256 + 30;
            x = 0;
            y = rowY;
            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Sediment");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_SedimentRT);

            x += 256 + 2;
            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Output Height");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_TerrainHeightRT);

            x += 256 + 2;
            EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Collision");
            EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_CollisionRT);
        }
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

        if (GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }

        if (GUILayout.Button("Execute")) {
            Simulate();
        }

        this.Repaint();
    }
    #endregion
}