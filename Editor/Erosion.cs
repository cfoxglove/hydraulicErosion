using UnityEngine;
using UnityEditor;

public class Erosion : EditorWindow {
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
    private float m_PrecipRate = 10.0f;

    private int[] m_texDim = { 256, 256 };

    private RenderTexture m_ResultRT;
    private RenderTexture m_InHeightRT;
    private RenderTexture m_PrecipMaskRT;
    
    private RenderTexture m_WaterRT;
    private RenderTexture m_WaterVelRT;
    private RenderTexture m_WaterVelPrevRT;

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
        //create the result texture we'll be writing to from our compute shader
        if (m_ResultRT == null) {
            m_ResultRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_ResultRT.format = RenderTextureFormat.ARGBFloat;
            m_ResultRT.enableRandomWrite = true;
            m_ResultRT.Create();
        }
        if(m_InHeightRT == null) {
            m_InHeightRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_InHeightRT.enableRandomWrite = true;
            m_InHeightRT.Create();
        }
        if(m_PrecipMaskRT == null) {
            m_PrecipMaskRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_PrecipMaskRT.enableRandomWrite = true;
            m_PrecipMaskRT.Create();
        }

        //Water level
        if (m_WaterRT == null) {
            m_WaterRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_WaterRT.format = RenderTextureFormat.RFloat;
            m_WaterRT.enableRandomWrite = true;
            m_WaterRT.Create();
        }

        //water velocity
        if(m_WaterVelPrevRT == null) {
            m_WaterVelPrevRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_WaterVelPrevRT.format = RenderTextureFormat.RGFloat;
            m_WaterVelPrevRT.enableRandomWrite = true;
            m_WaterVelPrevRT.Create();
        }
        if (m_WaterVelRT == null) {
            m_WaterVelRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_WaterVelRT.format = RenderTextureFormat.RGFloat;
            m_WaterVelRT.enableRandomWrite = true;
            m_WaterVelRT.Create();
        }

        //flux
        if(m_FluxRT == null) {
            m_FluxRT = new RenderTexture(m_texDim[0], m_texDim[1], 0);
            m_FluxRT.format = RenderTextureFormat.ARGBFloat;
            m_FluxRT.enableRandomWrite = true;
            m_FluxRT.Create();
        }
    }

    void ReleaseData() {
        if (m_ResultRT != null) {
            m_ResultRT.Release();
            m_ResultRT = null;
        }
        if (m_InHeightRT != null) {
            m_InHeightRT.Release();
            m_InHeightRT = null;
        }

        if (m_PrecipMaskRT != null) {
            m_PrecipMaskRT.Release();
            m_PrecipMaskRT = null;
        }

        if (m_WaterRT != null) {
            m_WaterRT.Release();
            m_WaterRT = null;
        }

        if (m_WaterVelRT != null) {
            m_WaterVelRT.Release();
            m_WaterVelRT = null;
        }
        if (m_WaterVelPrevRT != null) {
            m_WaterVelPrevRT.Release();
            m_WaterVelRT = null;
        }
        if(m_FluxRT != null) {
            m_FluxRT.Release();
            m_FluxRT = null;
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

    void SwapBuffers() {
        // current -> previous
        //Graphics.Blit(m_WaterRT, m_WaterPrevRT);
        //Graphics.Blit(m_WaterVelRT, m_WaterVelPrevRT);
    }

    void DispatchComputeShader() {
        if (m_ComputeShader != null) {
            InitData();
            int[] numWorkGroups = { 8, 8, 1 };
            int kidx = m_ComputeShader.FindKernel("CSMain");

            //set up textures for compute shader
            PrepareTextureData();
            m_ComputeShader.SetTexture(kidx, "Result", m_ResultRT);
            m_ComputeShader.SetTexture(kidx, "InHeight", m_InHeightRT);
            m_ComputeShader.SetTexture(kidx, "PrecipMask", m_PrecipMaskRT);

            //m_ComputeShader.SetTexture(kidx, "WaterPrev", m_WaterPrevRT);
            m_ComputeShader.SetTexture(kidx, "Water", m_WaterRT);

            m_ComputeShader.SetTexture(kidx, "WaterVelPrev", m_WaterVelPrevRT);
            m_ComputeShader.SetTexture(kidx, "WaterVel", m_WaterVelRT);

            m_ComputeShader.SetTexture(kidx, "Flux", m_FluxRT);

            m_ComputeShader.SetFloat("dt", m_DeltaTime);
            m_ComputeShader.SetFloat("precipRate", m_PrecipRate);
            m_ComputeShader.SetVector("texDim", new Vector4(m_texDim[0], m_texDim[1], 0.0f, 0.0f));

            for (int i = 0; i < m_NumIterations; i++) {
                //SwapBuffers();
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

        if(GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }
        if(GUILayout.Button("Execute")) {
            Debug.Log("Simulating...");
            DispatchComputeShader();
        }

        int dy = 180;
        int x = 0;
        int y = dy;
        //EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterPrevRT);
        //y += m_texDim[1] + 2;
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterVelPrevRT);
        y += m_texDim[0] + 2;
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_WaterVelRT);
        x += m_texDim[0] + 2; y = dy;
        EditorGUI.DrawPreviewTexture(new Rect(x, y, m_texDim[0], m_texDim[1]), m_FluxRT);
    }
}