using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Flora : EditorWindow {
    bool m_Init = false;

    struct FloraParticle {
        public Vector3 _pos;
        public Vector3 _vel;
    }

    RenderTexture m_ParticleDensityRT;

    int m_NumFloraParticles = 1024;
    FloraParticle[] m_FloraParticles;
    ComputeBuffer m_FloraParticleBuffer;

    ComputeShader m_FloraCompute;

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

        EditorGUI.DrawPreviewTexture(new Rect(0, 100, 256, 256), m_ParticleDensityRT);
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
            m_ParticleDensityRT = new RenderTexture(256, 256, 0);
            m_ParticleDensityRT.format = RenderTextureFormat.RFloat;
            m_ParticleDensityRT.enableRandomWrite = true;
            m_ParticleDensityRT.Create();

            m_FloraParticles = new FloraParticle[m_NumFloraParticles];

            for (int i = 0; i < m_NumFloraParticles; i++) {
                m_FloraParticles[i]._pos = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                m_FloraParticles[i]._vel = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
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
            m_ParticleDensityRT.Release();

            m_Init = false;
        }
    }
    #endregion

    #region sim
    //run the compute shader
    void Simulate() {
        InitData();
        if(m_FloraCompute != null) {
            int kFloraParticles = m_FloraCompute.FindKernel("FloraSimParticles");
            int kFloraTextures = m_FloraCompute.FindKernel("FloraSimTextures");

            Vector3Int particleThreadGroups = new Vector3Int(m_NumFloraParticles / 16, 1, 1);
            Vector3Int textureThreadGroups = new Vector3Int(m_ParticleDensityRT.width / 8, m_ParticleDensityRT.height / 8, 1);

            //set compute shader buffers, etc
            m_FloraCompute.SetBuffer(kFloraParticles, "FloraParticles", m_FloraParticleBuffer);

            m_FloraCompute.Dispatch(kFloraParticles, particleThreadGroups.x, particleThreadGroups.y, particleThreadGroups.z);


            //update density texture
            m_FloraCompute.SetInt("NumParticles", m_NumFloraParticles);
            m_FloraCompute.SetTexture(kFloraTextures, "ParticleDensity", m_ParticleDensityRT);
            m_FloraCompute.SetBuffer(kFloraTextures, "FloraParticles", m_FloraParticleBuffer);
            m_FloraCompute.Dispatch(kFloraTextures, textureThreadGroups.x, textureThreadGroups.y, textureThreadGroups.z);
        }
    }
    #endregion

}
