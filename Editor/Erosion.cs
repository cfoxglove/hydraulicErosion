using UnityEngine;
using UnityEditor;

public class Erosion : EditorWindow {
    private bool m_Init = false;

    [SerializeField]
    private ComputeShader m_ComputeShader = null;
    [SerializeField]
    private Terrain m_TerrainTile = null;
    [SerializeField]
    private Texture2D m_PrecipitationMask = null;
    [SerializeField]
    private int m_NumIterations = 10;
    [SerializeField]
    private float m_DeltaTime = 0.01f;
    [SerializeField]
    private float m_PrecipRate = 0.05f;
    [SerializeField]
    private float m_FlowRate = -0.05f;

    private int[] m_texDim = { 256, 256 };
    
    private RenderTexture m_InHeightRT;
    private RenderTexture m_PrecipMaskRT;
    
    private RenderTexture m_WaterRT;
    private RenderTexture m_WaterVelRT;

    private RenderTexture m_FluxRT;

    

    /*
    private RenderTexture m_LoamRT;
    private RenderTexture m_SedimentRT;
    private RenderTexture m_TempSedimentRT;
    private RenderTexture m_WaterVelRT;
    private RenderTexture m_FluxRT;
    */

    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Erosion")]
    static void Init() {
        Erosion window = (Erosion)EditorWindow.GetWindow(typeof(Erosion));
        window.Show();
    }

    void InitData() {
        if(m_Init == true) { return; }

        m_InHeightRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
        m_InHeightRT.format = RenderTextureFormat.RFloat;
        m_InHeightRT.enableRandomWrite = true;
        m_InHeightRT.Create();

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

        //we did all the things!
        m_Init = true;
    }

    void ReleaseData() {
        if (m_Init) {
            m_InHeightRT.Release();
            m_PrecipMaskRT.Release();
            m_WaterRT.Release();
            m_WaterVelRT.Release();
            m_FluxRT.Release();

            m_Init = false;
        }
    }

    void PrepareTextureData() {
        if(m_TerrainTile != null && m_InHeightRT != null) {
            Graphics.Blit(m_TerrainTile.terrainData.heightmapTexture, m_InHeightRT);
        }
        if(m_PrecipitationMask != null && m_PrecipMaskRT != null) {
            Graphics.Blit(m_PrecipitationMask, m_PrecipMaskRT);
        }
    }

    void Simulate() {
        if (m_ComputeShader != null) {
            InitData();
            int[] numWorkGroups = { 8, 8, 1 };
            int kidx = m_ComputeShader.FindKernel("HydraulicErosion");

            //set up textures for compute shader
            PrepareTextureData();
            m_ComputeShader.SetTexture(kidx, "InHeight", m_InHeightRT);
            m_ComputeShader.SetTexture(kidx, "PrecipMask", m_PrecipMaskRT);
            
            m_ComputeShader.SetTexture(kidx, "Water", m_WaterRT);
            m_ComputeShader.SetTexture(kidx, "WaterVel", m_WaterVelRT);
            m_ComputeShader.SetTexture(kidx, "Flux", m_FluxRT);

            m_ComputeShader.SetFloat("dt", m_DeltaTime);
            m_ComputeShader.SetFloat("precipRate", m_PrecipRate);
            m_ComputeShader.SetFloat("flowRate", m_FlowRate);
            m_ComputeShader.SetVector("texDim", new Vector4(m_texDim[0], m_texDim[1], 0.0f, 0.0f));

            for (int i = 0; i < m_NumIterations; i++) {
                m_ComputeShader.Dispatch(kidx, m_texDim[0] / numWorkGroups[0], m_texDim[0] / numWorkGroups[1], numWorkGroups[2]);
            }

            //Blit the output back to the terrain heightmap
            //Graphics.Blit(m_ResultRT, m_TerrainTile.terrainData.heightmapTexture);
        }
    }

    void OnGUI() {
        m_ComputeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", m_ComputeShader, typeof(ComputeShader));
        m_TerrainTile = (Terrain)EditorGUILayout.ObjectField("Terrain Tile", m_TerrainTile, typeof(Terrain));
        m_PrecipitationMask = (Texture2D)EditorGUILayout.ObjectField("Precipitation Mask", m_PrecipitationMask, typeof(Texture2D));
        m_NumIterations = EditorGUILayout.IntField("# Iterations", m_NumIterations);
        m_PrecipRate = EditorGUILayout.FloatField("Precipitation Rate", m_PrecipRate);
        m_FlowRate = EditorGUILayout.FloatField("Flow Rate", m_FlowRate);

        if(GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }
        if(GUILayout.Button("Execute")) {
            Simulate();
        }

        int dy = 240;
        int x = 0;
        int y = dy;

        EditorGUI.LabelField(new Rect(x, y - 20, m_texDim[0], m_texDim[1]), "Water Level");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, m_texDim[0], m_texDim[1]), "Water Velocity");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterVelRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.LabelField(new Rect(x, y - 20, m_texDim[0], m_texDim[1]), "Water Flux");
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_FluxRT);

        this.Repaint();
    }
}