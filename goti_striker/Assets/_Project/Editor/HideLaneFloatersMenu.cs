using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class HideLaneFloatersMenu
{
    [MenuItem("Pit Striker/Hide Lane Floaters (House_Joinery)")]
    public static void Hide()
    {
        var go = GameObject.Find("House_Joinery");
        if (go == null)
        {
            // search inactive too
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name == "House_Joinery" && t.gameObject.scene.IsValid())
                {
                    go = t.gameObject;
                    break;
                }
            }
        }
        if (go == null)
        {
            Debug.LogError("[HideLaneFloaters] House_Joinery not found in open scenes.");
            return;
        }
        Undo.RecordObject(go, "Hide House_Joinery floaters");
        go.SetActive(false);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Debug.Log("[HideLaneFloaters] Deactivated " + GetPath(go.transform) + " and saved scene.");
    }

    static string GetPath(Transform t)
    {
        var p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
