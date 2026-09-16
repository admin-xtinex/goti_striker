using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerwusStudio
{
    /// <summary>
    /// The only touchpoint this pack has after import. One window, two moments:
    ///   first import       -> what this is, the demo scene, the rest of the series
    ///   AskAfterDays later -> the review ask, once, never again
    /// Still having the pack installed days later is the engagement signal, so no
    /// runtime scripts are needed and the package stays script-free in builds.
    /// The Editor/ folder convention keeps this and its art out of player builds -
    /// no asmdef required.
    /// Menu: Tools > Serwus Studio > Cursed Cozy Village Free
    /// </summary>
    public class CCVFree_Welcome : EditorWindow
    {
        // ---- per-pack config, the only block to edit ------------------------
        // Both links are direct, so they can only be changed by shipping a new
        // version of the package: keep the Discord invite set to never expire, and
        // when this pack goes up on Fab, point RateUrl at whichever store that
        // build is for.
        const string PackName = "Cursed Cozy Village — Free Starter Pack";
        const string Version = "1.2.0";
        const string RateUrl =
            "https://assetstore.unity.com/packages/3d/environments/cursed-cozy-village-free-pack-382598#reviews";
        const string DiscordUrl = "https://discord.gg/Cu2HF9xyVm";
        const string DemoScene =
            "Assets/SerwusStudio/Cursed Cozy Village/Free Starter Pack/Demo/Demo_Inter.unity";

        // An empty url renders greyed out as "coming soon" instead of a dead link.
        static readonly (string label, string url)[] Packs =
        {
            ("Cursed Cozy Shop Alley — Market Shopfront Kit",
                "https://assetstore.unity.com/packages/3d/environments/cursed-cozy-shop-alley-enivro-kit-system-390220"),
            ("Cursed Cozy Alchemists Corner", ""),
            ("Cursed Cozy Gothic Interiors", ""),
            ("Cursed Cozy Graveyard & Ritual", ""),
            ("Cursed Cozy Vegetation", ""),
        };
        // ---------------------------------------------------------------------

        const int AskAfterDays = 3;
        const string KeyFirstSeen = "Serwus.CCVFree.firstSeen";
        const string KeyShown = "Serwus.CCVFree." + Version + ".shown";
        const string KeyNeverShow = "Serwus.CCVFree.neverShow";
        const string KeyRateAsked = "Serwus.CCVFree.rateAsked";

        static bool pendingRateMode;
        bool rateMode;

        [InitializeOnLoadMethod]
        static void Boot()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Decide; // let the import finish first
        }

        static void Decide()
        {
            if (EditorPrefs.GetBool(KeyNeverShow, false)) return;

            var firstSeen = EditorPrefs.GetString(KeyFirstSeen, string.Empty);
            if (string.IsNullOrEmpty(firstSeen))
                EditorPrefs.SetString(KeyFirstSeen, firstSeen = DateTime.UtcNow.ToString("o"));

            if (!EditorPrefs.GetBool(KeyShown, false))
            {
                EditorPrefs.SetBool(KeyShown, true); // once per version, even if closed by X
                Open(false);
                return;
            }

            if (EditorPrefs.GetBool(KeyRateAsked, false)) return;
            if (!DateTime.TryParse(firstSeen, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var t)) return;
            if ((DateTime.UtcNow - t).TotalDays >= AskAfterDays) Open(true);
        }

        static void Open(bool rate)
        {
            pendingRateMode = rate;
            var w = GetWindow<CCVFree_Welcome>(true, rate ? "Cursed Cozy Village" : PackName, true);
            w.rateMode = rate;
            w.minSize = w.maxSize = rate ? new Vector2(470f, 250f) : new Vector2(560f, 470f);
            w.Build();
        }

        [MenuItem("Tools/Serwus Studio/Cursed Cozy Village Free/Welcome")]
        static void MenuWelcome() => Open(false);

        [MenuItem("Tools/Serwus Studio/Cursed Cozy Village Free/Rate the pack")]
        static void MenuRate() => Application.OpenURL(RateUrl);

        void CreateGUI()
        {
            rateMode = pendingRateMode;
            Build();
        }

        void Build()
        {
            var root = rootVisualElement;
            root.Clear();

            var uxml = Find<VisualTreeAsset>("CCVFree_Welcome");
            var uss = Find<StyleSheet>("CCVFree_Welcome");
            if (uxml == null || uss == null)
            {
                root.Add(new Label("CCVFree_Welcome.uxml / .uss not found next to this script."));
                return;
            }

            root.styleSheets.Add(uss);
            var tree = uxml.Instantiate();
            tree.style.flexGrow = 1f;
            root.Add(tree);
            root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Close(); });

            tree.Q("welcome").style.display = rateMode ? DisplayStyle.None : DisplayStyle.Flex;
            tree.Q("rate").style.display = rateMode ? DisplayStyle.Flex : DisplayStyle.None;
            tree.Q<Label>("lbl_pack").text = PackName;

            if (rateMode)
            {
                tree.Q<Button>("btn_rate_go").clicked += () => { Application.OpenURL(RateUrl); Close(); };
                tree.Q<Button>("btn_rate_later").clicked += Close;
                return;
            }

            tree.Q<Button>("btn_demo").clicked += OpenDemo;
            tree.Q<Button>("btn_close").clicked += Close;
            tree.Q<Button>("btn_dontshow").clicked += () =>
            {
                EditorPrefs.SetBool(KeyNeverShow, true); // silences the review ask too, on purpose
                Close();
            };
            Link(tree.Q<Label>("btn_discord"), DiscordUrl);
            Link(tree.Q<Label>("btn_rate"), RateUrl);

            // /01, /02 ... mirrors the section numbering on serwusgamestudio.pl
            var list = tree.Q("pack_links");
            for (var i = 0; i < Packs.Length; i++)
            {
                var pack = Packs[i];
                var released = !string.IsNullOrEmpty(pack.url);
                var label = new Label(string.Format("/{0:00}  {1}{2}", i + 1, pack.label,
                    released ? "  ↗" : "   — soon"));
                label.AddToClassList(released ? "hyperlink" : "soon");
                if (released) Link(label, pack.url);
                list.Add(label);
            }
        }

        void OnDestroy()
        {
            // Closing the ask by any route counts as asked. A second prompt is the
            // fastest way to turn a would-be review into a one-star one.
            if (rateMode) EditorPrefs.SetBool(KeyRateAsked, true);
        }

        static void Link(Label label, string url) =>
            label.AddManipulator(new Clickable(() => Application.OpenURL(url)));

        static void OpenDemo()
        {
            if (!File.Exists(DemoScene))
            {
                EditorUtility.DisplayDialog("Demo scene not found",
                    "Expected it at:\n" + DemoScene + "\n\nIf you moved the pack, open Demo.unity manually.",
                    "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DemoScene);
        }

        /// <summary>Find by asset name so moving the pack folder does not break it.</summary>
        static T Find<T>(string name) where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }
    }
}
