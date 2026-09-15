#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Phase 2 P1 - place distant houses + banana/bank plants under Phase2_P1_Dressing.
    /// Does not touch gameplay kit / pits / fairway / Phase2_P0.
    /// </summary>
    [InitializeOnLoad]
    public static class Phase2P1Integrate
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string AutoFlag = "Library/Phase2P1Integrate.autorun";
        const string ResultFlag = "Library/Phase2P1Integrate.result";
        const string DressingName = "Phase2_P1_Dressing";

        const string HouseA = "Assets/_Project/Art/Models/Phase2_P1/Bldg_KeralaHouse_B_Distant.fbx";
        const string HouseB = "Assets/_Project/Art/Models/Phase2_P1/Bldg_KeralaHouse_B_Distant_B.fbx";
        const string HouseC = "Assets/_Project/Art/Models/Phase2_P1/Bldg_KeralaHouse_B_Distant_C.fbx";
        const string Banana = "Assets/_Project/Art/Models/Phase2_P1/Veg_BananaClump.fbx";
        const string Bank = "Assets/_Project/Art/Models/Phase2_P1/Veg_BankPlant_Low.fbx";

        static Phase2P1Integrate()
        {
            EditorApplication.update += TryAutorun;
        }

        [MenuItem("Pit Striker/Phase2 P1 Integrate Dressing")]
        public static void RunMenu()
        {
            Run();
        }

        static void TryAutorun()
        {
            if (!File.Exists(AutoFlag) || EditorApplication.isCompiling || EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < 4f)
                return;
            File.Delete(AutoFlag);
            Run();
        }

        public static void Run()
        {
            string result;
            try
            {
                result = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + result);
                Debug.Log("<color=#00FF88>[P2 P1 Integrate]</color> " + result);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                Debug.LogException(e);
                throw;
            }
        }

        static string Apply()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Remove prior dressing if re-run
            var existing = GameObject.Find(DressingName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var dressing = new GameObject(DressingName);
            int count = 0;

            // Distant houses - behind / beside lane, clear of kit pits near origin
            count += Place(HouseA, dressing.transform, "P2_KeralaHouse_B", new Vector3(-14.0f, 0f, 28.0f), 90f);
            count += Place(HouseB, dressing.transform, "P2_KeralaHouse_B_B", new Vector3(13.5f, 0f, 34.0f), -90f);
            count += Place(HouseC, dressing.transform, "P2_KeralaHouse_B_C", new Vector3(-13.0f, 0f, 44.0f), 80f);

            // Banana clumps - mid/far sides near paddies
            var bananas = new[]
            {
                new Vector3(-10.5f, 0f, 12.0f),
                new Vector3(-11.5f, 0f, 20.0f),
                new Vector3(10.0f, 0f, 16.0f),
                new Vector3(11.0f, 0f, 26.0f),
                new Vector3(-9.5f, 0f, 36.0f),
                new Vector3(9.5f, 0f, 40.0f),
            };
            for (int i = 0; i < bananas.Length; i++)
                count += Place(Banana, dressing.transform, "P2_Banana_" + i, bananas[i], i * 35f);

            // Bank plants along channel lines (~+/-4.8) and a few canal edges
            var banks = new List<Vector3>();
            for (float z = -1f; z <= 40f; z += 3.5f)
            {
                banks.Add(new Vector3(-5.1f, 0f, z));
                banks.Add(new Vector3(5.1f, 0f, z + 1.2f));
            }
            // denser near house A left bank
            banks.Add(new Vector3(-6.2f, 0f, 7.0f));
            banks.Add(new Vector3(-6.0f, 0f, 10.0f));
            banks.Add(new Vector3(6.0f, 0f, 9.0f));

            for (int i = 0; i < banks.Count; i++)
                count += Place(Bank, dressing.transform, "P2_Bank_" + i, banks[i], (i % 8) * 45f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Placed " + count + " instances under " + DressingName + " (houses 3, bananas " + bananas.Length + ", banks " + banks.Count + "). Kit untouched.";
        }

        static int Place(string assetPath, Transform parent, string name, Vector3 pos, float yawDeg)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                throw new System.Exception("Missing asset: " + assetPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = Vector3.one;
            return 1;
        }
    }
}
#endif