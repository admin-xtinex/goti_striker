#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PitStriker.GameplayKit.UI;

namespace PitStriker.GameplayKit.EditorTools
{
    public static class BuildShotControlUIPrefab
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/UI/Prefabs/UI_ShotControl.prefab";
        const string TexRoot = "Assets/_Project/GameplayKit/UI/Textures/";

        [MenuItem("Pit Striker/GameplayKit/Build Shot Control UI Prefab")]
        public static void Run()
        {
            string[] sprites = {
                "UI_ShotToggle_Track.png","UI_ShotToggle_PillActive.png","UI_ShotToggle_PillInactive.png",
                "UI_PowerArea_Frame.png","UI_PowerBar_Track.png","UI_PowerBar_Fill.png",
                "UI_Finger_Tutorial.png","UI_Finger_Trail.png"
            };
            foreach (var s in sprites) EnsureSpriteImport(TexRoot + s);
            AssetDatabase.Refresh();

            var root = new GameObject("UI_ShotControl", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;

            var toggle = CreateUIObject("ShotModeToggle", root.transform);
            SetOffset(toggle, new Vector2(0,0), new Vector2(0,0), new Vector2(24,24), new Vector2(304,96));
            CreateImage("Track", toggle.transform, TexRoot + "UI_ShotToggle_Track.png", true);

            var ground = CreateImage("Pill_Ground", toggle.transform, TexRoot + "UI_ShotToggle_PillActive.png", false);
            SetSize(ground.GetComponent<RectTransform>(), new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(12,0), new Vector2(136,56));
            CreateLabel(ground.transform, "GROUND", Color.white);
            ground.gameObject.AddComponent<Button>();

            var loft = CreateImage("Pill_Loft", toggle.transform, TexRoot + "UI_ShotToggle_PillInactive.png", false);
            SetSize(loft.GetComponent<RectTransform>(), new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(-12,0), new Vector2(136,56));
            CreateLabel(loft.transform, "LOFT", new Color(1,1,1,0.55f));
            loft.gameObject.AddComponent<Button>();

            var power = CreateUIObject("PowerArea", root.transform);
            var prt = power.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.78f, 0.08f); prt.anchorMax = new Vector2(0.98f, 0.92f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            var frame = CreateImage("Frame", power.transform, TexRoot + "UI_PowerArea_Frame.png", true);
            StretchFull(frame.GetComponent<RectTransform>());

            var track = CreateImage("PowerTrack", power.transform, TexRoot + "UI_PowerBar_Track.png", false);
            SetSize(track.GetComponent<RectTransform>(), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), Vector2.zero, new Vector2(28,360));
            var fill = CreateImage("PowerFill", track.transform, TexRoot + "UI_PowerBar_Fill.png", false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            StretchFull(fill.GetComponent<RectTransform>());

            var hint = CreateLabel(power.transform, "SWIPE", new Color(0.6f,0.85f,1f,0.85f));
            hint.fontSize = 16;
            SetSize(hint.GetComponent<RectTransform>(), new Vector2(0.5f,1f), new Vector2(0.5f,1f), new Vector2(0.5f,1f), new Vector2(0,-24), new Vector2(160,28));

            var tutorial = CreateUIObject("FingerTutorial", power.transform);
            StretchFull(tutorial.GetComponent<RectTransform>());
            var trail = CreateImage("Trail", tutorial.transform, TexRoot + "UI_Finger_Trail.png", false);
            SetSize(trail.GetComponent<RectTransform>(), new Vector2(0.5f,0.55f), new Vector2(0.5f,0.55f), new Vector2(0.5f,0.5f), Vector2.zero, new Vector2(48,160));
            var finger = CreateImage("Finger", tutorial.transform, TexRoot + "UI_Finger_Tutorial.png", false);
            SetSize(finger.GetComponent<RectTransform>(), new Vector2(0.5f,0.62f), new Vector2(0.5f,0.62f), new Vector2(0.5f,0.5f), Vector2.zero, new Vector2(96,120));
            tutorial.AddComponent<FingerTutorialMover>();

            Directory.CreateDirectory("Assets/_Project/GameplayKit/UI/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/BuildShotControlUIPrefab.result", "PASS\n" + PrefabPath);
            Debug.Log("<color=#00FF88>[ShotUI]</color> Built " + PrefabPath);
        }

        public static void RunBatch() { Run(); }

        static void EnsureSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image CreateImage(string name, Transform parent, string spritePath, bool raycast)
        {
            var go = CreateUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            img.raycastTarget = raycast;
            img.color = Color.white;
            return img;
        }

        static Text CreateLabel(Transform parent, string msg, Color color)
        {
            var go = CreateUIObject("Label", parent);
            var t = go.AddComponent<Text>();
            t.text = msg; t.fontSize = 22; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            StretchFull(go.GetComponent<RectTransform>());
            return t;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void SetOffset(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        static void SetSize(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
    }
}
#endif