using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Flora : EditorWindow {
    bool m_Init = false;

    struct FloraParticle {
        public Vector3  _pos;
        public Vector3  _vel;
        public float    _age;
    }

    [SerializeField]
    Texture2D m_HeightMapTex;
    Texture2D m_FoliageDensityTex;

    RenderTexture m_HeightMapRT;
    RenderTexture m_WindVelRT; //either user supplied, or from a dynamic fluid sim
    RenderTexture m_SeedDensityRT;
    RenderTexture m_FoliageDensityRT;

    int m_NumFloraParticles = 1024;
    FloraParticle[] m_FloraParticles;
    ComputeBuffer m_FloraParticleBuffer;

    ComputeShader m_FloraCompute;

    int m_NumIterations = 10;

    #region GUI
    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Flora")]
    static void Init() {
        Flora window = (Flora)EditorWindow.GetWindow(typeof(Flora));
        window.Show();
    }

    private void OnGUI() {
        if(GUILayout.Button("Reset")) {
            ReleaseData();
            InitData();
        }
        if(GUILayout.Button("Scatter")) {
            Simulate();
        }

        m_HeightMapTex = (Texture2D)EditorGUILayout.ObjectField("HeightMap", m_HeightMapTex, typeof(Texture2D));
        m_FoliageDensityTex = (Texture2D)EditorGUILayout.ObjectField("Foliage Density", m_FoliageDensityTex, typeof(Texture2D));

        EditorGUI.DrawPreviewTexture(new Rect(0, 150, 256, 256), m_SeedDensityRT);
        EditorGUI.DrawPreviewTexture(new Rect(0, 150 + 256, 256, 256), m_FoliageDensityRT);
    }
    #endregion

    #region alloc / dealloc
    ComputeShader GetFloraComputeShader() {
        if (m_FloraCompute == null) {
            m_FloraCompute = (ComputeShader)Resources.Load("Flora");
        }
        return m_FloraCompute;
    }

    void InitData() {
        if (m_Init == false) {
            m_SeedDensityRT = new RenderTexture(256, 256, 0);
            m_SeedDensityRT.format = RenderTextureFormat.RFloat;
            m_SeedDensityRT.enableRandomWrite = true;
            m_SeedDensityRT.Create();

            m_FoliageDensityRT = new RenderTexture(256, 256, 0);
            m_FoliageDensityRT.format = RenderTextureFormat.RFloat;
            m_FoliageDensityRT.enableRandomWrite = true;
            m_FoliageDensityRT.Create();

            m_HeightMapRT = new RenderTexture(256, 256, 0);
            m_HeightMapRT.format = RenderTextureFormat.RFloat;
            m_HeightMapRT.enableRandomWrite = true;
            m_HeightMapRT.Create();

            if(m_HeightMapTex != null) {
                Graphics.Blit(m_HeightMapTex, m_HeightMapRT);
            }

            if(m_FoliageDensityTex != null) {
                Graphics.Blit(m_FoliageDensityTex, m_FoliageDensityRT);
            }

            m_FloraParticles = new FloraParticle[m_NumFloraParticles];

            for (int i = 0; i < m_NumFloraParticles; i++) {
                m_FloraParticles[i]._pos = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                m_FloraParticles[i]._vel = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
                m_FloraParticles[i]._age = -1.0f;
            }

            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FloraParticle));
            m_FloraParticleBuffer = new ComputeBuffer(m_NumFloraParticles, size);
            m_FloraParticleBuffer.SetData(m_FloraParticles);

            m_FloraCompute = GetFloraComputeShader();

            m_Init = true;
        }
    }

    void ReleaseData() {
        if (m_Init == true) {
            m_FloraParticleBuffer.Release();
            m_SeedDensityRT.Release();

            m_Init = false;
        }
    }
    #endregion

    #region sim
    //run the compute shader
    void Simulate() {
        InitData();
        if(m_FloraCompute != null) {
            for (int i = 0; i < m_NumIterations; i++) {
                float rSeed = Random.Range(0.0f, 9999999.0f);
                int kFloraParticles = m_FloraCompute.FindKernel("FloraSimParticles");
                int kFloraTextures = m_FloraCompute.FindKernel("FloraSimTextures");

                Vector3Int particleThreadGroups = new Vector3Int(m_NumFloraParticles / 16, 1, 1);
                Vector3Int textureThreadGroups = new Vector3Int(m_SeedDensityRT.width / 8, m_SeedDensityRT.height / 8, 1);

                //set compute shader buffers, etc
                m_FloraCompute.SetBuffer(kFloraParticles, "FloraParticles", m_FloraParticleBuffer);
                m_FloraCompute.SetTexture(kFloraParticles, "SeedDensity", m_SeedDensityRT);
                m_FloraCompute.SetTexture(kFloraParticles, "FoliageDensity", m_FoliageDensityRT);
                m_FloraCompute.SetTexture(kFloraParticles, "HeightMap", m_HeightMapRT);
                
                m_FloraCompute.SetFloat("RandomSeed", rSeed);

                m_FloraCompute.Dispatch(kFloraParticles, particleThreadGroups.x, particleThreadGroups.y, particleThreadGroups.z);

                //update density texture
                m_FloraCompute.SetInt("NumParticles", m_NumFloraParticles);
                m_FloraCompute.SetTexture(kFloraTextures, "SeedDensity", m_SeedDensityRT);
                m_FloraCompute.SetTexture(kFloraTextures, "FoliageDensity", m_FoliageDensityRT);
                m_FloraCompute.SetTexture(kFloraTextures, "HeightMap", m_HeightMapRT);
                m_FloraCompute.SetBuffer(kFloraTextures, "FloraParticles", m_FloraParticleBuffer);
                m_FloraCompute.Dispatch(kFloraTextures, textureThreadGroups.x, textureThreadGroups.y, textureThreadGroups.z);
            }
        }
    }
    #endregion

}
