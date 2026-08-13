#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WaveManager))]
public class WaveDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        WaveManager manager = (WaveManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wave Manager Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Waves Automatically"))
        {
            manager.GenerateWaves();
            EditorUtility.SetDirty(manager);
        }

        //if (GUILayout.Button("Reset All Waves"))
        //{
        //    manager.ResetWaves();
        //    EditorUtility.SetDirty(manager);
        //}

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Total Waves: {manager.waves.Length}");

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField($"Current Wave: {manager.GetCurrentWave()}/{manager.GetTotalWaves()}");
            EditorGUILayout.LabelField($"Enemies Alive: {manager.GetEnemiesAlive()}");
            EditorGUILayout.LabelField($"Wave Active: {manager.IsWaveActive()}");
        }
    }
}
#endif