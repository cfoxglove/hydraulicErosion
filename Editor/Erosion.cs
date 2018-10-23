using UnityEngine;
using UnityEditor;

public class Erosion : EditorWindow {
    private bool m_Init = false;

    private ComputeShader m_ComputeShader = null;
    private Terrain m_TerrainTile = null;
    private Texture2D m_PrecipitationMask = null;

    //decent default values here
    private int m_NumHydraulicIterations = 300;
    private int m_NumThermalIterations = 10;

    private float m_DeltaTime = 0.01f;
    private float m_PrecipRate = 0.1f;
    private float m_FlowRate = -0.07f;
    private float m_SedimentCapacity = 50.0f;
    private float m_SedimentDissolveRate = 0.01f;
    private float m_SedimentDepositRate = 0.01f;
    private float m_EvaporationRate = 0.0001f;
    private float m_SmoothingFactor = 0.05f;

    //thermal erosion
    private float m_AngleOfRepose = 35.0f; //in degrees

    private int[] m_texDim = { 256, 256 };
    
    private RenderTexture m_TerrainHeightRT;
    private RenderTexture m_TerrainHeightPrevRT;

    private RenderTexture m_PrecipMaskRT;
    private RenderTexture m_WaterRT;
    private RenderTexture m_WaterVelRT;
    private RenderTexture m_FluxRT;
    private RenderTexture m_SedimentRT;
    //private RenderTexture m_TerrainNormalsRT;

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

        m_TerrainHeightRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        //m_TerrainHeightRT.format = RenderTextureFormat.RFloat;
        m_TerrainHeightRT.format = RenderTextureFormat.RHalf;
        m_TerrainHeightRT.enableRandomWrite = true;
        m_TerrainHeightRT.Create();

        m_TerrainHeightPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        //m_TerrainHeightPrevRT.format = RenderTextureFormat.RFloat;
        m_TerrainHeightPrevRT.format = RenderTextureFormat.RHalf;
        m_TerrainHeightPrevRT.enableRandomWrite = true;
        m_TerrainHeightPrevRT.Create();

        m_PrecipMaskRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_PrecipMaskRT.enableRandomWrite = true;
        m_PrecipMaskRT.Create();

        //Water level
        m_WaterRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterRT.format = RenderTextureFormat.RFloat;
        m_WaterRT.enableRandomWrite = true;
        m_WaterRT.Create();

        //water velocity
        m_WaterVelRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_WaterVelRT.format = RenderTextureFormat.RGFloat;
        m_WaterVelRT.enableRandomWrite = true;
        m_WaterVelRT.Create();

        //flux
        m_FluxRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_FluxRT.format = RenderTextureFormat.ARGBFloat;
        m_FluxRT.enableRandomWrite = true;
        m_FluxRT.Create();

        //sediment
        m_SedimentRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_SedimentRT.format = RenderTextureFormat.RFloat;
        m_SedimentRT.enableRandomWrite = true;
        m_SedimentRT.Create();

        /*
        //Terrain Normals
        m_TerrainNormalsRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_TerrainNormalsRT.format = RenderTextureFormat.ARGBFloat;
        m_TerrainNormalsRT.enableRandomWrite = true;
        m_TerrainNormalsRT.Create();
        */

        //we did all the things!
        m_Init = true;
    }

    void ReleaseData() {
        if (m_Init) {
            m_TerrainHeightRT.Release();
            m_TerrainHeightPrevRT.Release();
            m_PrecipMaskRT.Release();
            m_WaterRT.Release();
            m_WaterVelRT.Release();
            m_FluxRT.Release();
            m_SedimentRT.Release();
            //m_TerrainNormalsRT.Release();

            m_Init = false;
        }
    }

    void PrepareTextureData() {
        if(m_Init) {
            Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_TerrainHeightRT);
            //Graphics.Blit(m_TerrainTile.terrainData.normalMapTexture, m_TerrainNormalsRT);
            Graphics.Blit(m_PrecipitationMask, m_PrecipMaskRT);
        }
    }

    void SwapBuffers() {
        /*RenderTexture tmp = m_TerrainHeightRT;
        m_TerrainHeightRT = m_TerrainHeightPrevRT;
        m_TerrainHeightPrevRT = tmp;
        */
        
        RenderTexture tmp = RenderTexture.GetTemporary(m_texDim[0], m_texDim[1]);

        Graphics.Blit(m_TerrainHeightPrevRT, tmp);
        Graphics.Blit(m_TerrainHeightRT, m_TerrainHeightPrevRT);
        Graphics.Blit(tmp, m_TerrainHeightRT);

        RenderTexture.ReleaseTemporary(tmp);
    }

    void Simulate() {
        if (m_ComputeShader != null) {
            InitData();

            int[] numWorkGroups = { 8, 8, 1 };
            int hydraulicKernelIdx = m_ComputeShader.FindKernel("HydraulicErosion");
            int thermalKernelIdx = m_ComputeShader.FindKernel("ThermalErosion");

            //set up textures for compute shader
            PrepareTextureData();
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeight", m_TerrainHeightRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);

            m_ComputeShader.SetTexture(hydraulicKernelIdx, "PrecipMask", m_PrecipMaskRT);
            
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Water", m_WaterRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "WaterVel", m_WaterVelRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Flux", m_FluxRT);
            m_ComputeShader.SetTexture(hydraulicKernelIdx, "Sediment", m_SedimentRT);

            m_ComputeShader.SetFloat("dt", m_DeltaTime);
            m_ComputeShader.SetFloat("precipRate", m_PrecipRate);
            m_ComputeShader.SetFloat("flowRate", m_FlowRate);
            m_ComputeShader.SetFloat("sedimentCapacity", m_SedimentCapacity);
            m_ComputeShader.SetFloat("sedimentDissolveRate", m_SedimentDissolveRate);
            m_ComputeShader.SetFloat("sedimentDepositRate", m_SedimentDepositRate);
            m_ComputeShader.SetFloat("evaporationRate", m_EvaporationRate);

            m_ComputeShader.SetVector("texDim", new Vector4(m_texDim[0], m_texDim[1], 0.0f, 0.0f));

            float m = Mathf.Tan(m_AngleOfRepose * Mathf.Deg2Rad);
            Debug.Log("angle of repose slope: " + m);

            // Set parameters for the thermal erosion shader
            m_ComputeShader.SetInt("numThermalIterations", m_NumThermalIterations);
            m_ComputeShader.SetFloat("angleOfRepose", m);
            m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeight", m_TerrainHeightRT);
            m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);

            //should probably do these loops inside the compute shader?
            /*for (int i = 0; i < m_NumHydraulicIterations; i++) {
                m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeight", m_TerrainHeightRT);
                m_ComputeShader.SetTexture(hydraulicKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);
                m_ComputeShader.Dispatch(hydraulicKernelIdx, m_texDim[0] / numWorkGroups[0], m_texDim[1] / numWorkGroups[1], numWorkGroups[2]);

                SwapBuffers();*/
            for (int j = 0; j < m_NumThermalIterations; j++) {

                    //m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeight", m_TerrainHeightRT);
                    //m_ComputeShader.SetTexture(thermalKernelIdx, "TerrainHeightPrev", m_TerrainHeightPrevRT);
                    m_ComputeShader.Dispatch(thermalKernelIdx, m_texDim[0] / numWorkGroups[0], m_texDim[1] / numWorkGroups[1], numWorkGroups[2]);
                    SwapBuffers();
                }
            //}

            //Blit the output back to the terrain heightmap
            Graphics.Blit(m_TerrainHeightRT, m_TerrainTile.terrainData.heightmapTexture);
            //Graphics.Blit(m_SedimentRT, m_TerrainTile.terrainData.heightmapTexture);
            //m_TerrainTile.terrain.terrainData.UpdateDirtyRegion(terrainTile.clippedLocal.x, terrainTile.clippedLocal.y, terrainTile.clippedLocal.width, terrainTile.clippedLocal.height, !terrainTile.terrain.drawInstanced);
            //m_TerrainTile.terrainData.UpdateDirtyRegion(0, 0, (int)m_TerrainTile.terrainData.size.x, (int)m_TerrainTile.terrainData.size.y, true);
        }
    }

    void OnGUI() {
        m_ComputeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", m_ComputeShader, typeof(ComputeShader));
        m_TerrainTile = (Terrain)EditorGUILayout.ObjectField("Terrain Tile", m_TerrainTile, typeof(Terrain));
        m_PrecipitationMask = (Texture2D)EditorGUILayout.ObjectField("Precipitation Mask", m_PrecipitationMask, typeof(Texture2D));
        m_NumHydraulicIterations = EditorGUILayout.IntField("# Iterations", m_NumHydraulicIterations);
        m_DeltaTime = EditorGUILayout.FloatField("Simulation time step", m_DeltaTime);
        m_PrecipRate = EditorGUILayout.FloatField("Precipitation Rate", m_PrecipRate);
        m_FlowRate = EditorGUILayout.FloatField("Flow Rate", m_FlowRate);
        m_SedimentCapacity = EditorGUILayout.FloatField("Sediment Capacity", m_SedimentCapacity);
        m_SedimentDissolveRate = EditorGUILayout.FloatField("Sediment Dissolve Rate", m_SedimentDissolveRate);
        m_SedimentDepositRate = EditorGUILayout.FloatField("Sediment Deposit Rate", m_SedimentDepositRate);
        m_EvaporationRate = EditorGUILayout.FloatField("Evaporation Rate", m_EvaporationRate);

        m_NumThermalIterations = EditorGUILayout.IntField("# Iterations", m_NumThermalIterations);
        m_SmoothingFactor = EditorGUILayout.FloatField("Smoothing Factor", m_SmoothingFactor);
        m_AngleOfRepose = EditorGUILayout.FloatField("Angle of Repose", m_AngleOfRepose);

        if (GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }

        if(GUILayout.Button("Execute")) {
            Simulate();
        }

        int dy = 340;
        int x = 0;
        int y = dy;

        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Level");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Velocity");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_WaterVelRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Water Flux");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_FluxRT);

        //bottom row
        x = 0;
        y += m_texDim[1] + 30;
        EditorGUI.LabelField(new Rect(x, y - 20, 256, 256), "Sediment");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, 256, 256), m_SedimentRT);

        this.Repaint();
    }
}