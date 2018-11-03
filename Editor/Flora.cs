using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Flora : EditorWindow {

    Texture2D m_HydrationMap;
    Texture2D m_HeightField;

    #region GUI
    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Flora")]
    static void Init() {
        Flora window = (Flora)EditorWindow.GetWindow(typeof(Flora));
        window.Show();
    }

    private void OnGUI() {
        if(GUILayout.Button("Scatter")) {
            Simulate();
        }
    }
    #endregion

    #region sim
    void Simulate() {

    }
    #endregion

}
