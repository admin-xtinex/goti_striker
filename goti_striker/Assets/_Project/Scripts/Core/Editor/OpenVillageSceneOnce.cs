using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class OpenVillageSceneOnce
{
    const string Flag = "PitStriker.OpenVillageScene.v1";
    const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";

    static OpenVillageSceneOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(Flag, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!System.IO.File.Exists(ScenePath)) return;
            SessionState.SetBool(Flag, true);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[OpenVillageSceneOnce] Opened {ScenePath}, roots={scene.rootCount}");
        };
    }
}
