using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
#endif

namespace Match3
{
    [CreateAssetMenu]
    public class LevelList : ScriptableObject
    {
#if UNITY_EDITOR
        public SceneAsset[] Scenes;
#endif

        [HideInInspector] public int[] SceneList;

        public int SceneCount
        {
            get
            {
#if UNITY_EDITOR
                return Scenes.Length;
#else
            return SceneList.Length;
#endif
            }
        }

        public void LoadLevel(int levelNumber)
        {
            if (levelNumber < 0 || levelNumber >= SceneCount)
            {
                Debug.LogError($"Attempted to load invalid level number: {levelNumber}. Valid range is 0 to {SceneCount - 1}");
                return;
            }
            
#if UNITY_EDITOR
            //in editor we directly load the scene
            EditorSceneManager.LoadSceneInPlayMode(AssetDatabase.GetAssetPath(Scenes[levelNumber]),
                new LoadSceneParameters(LoadSceneMode.Single));
#else
    //in build we load through the normal scene manager as the pre build script will have filled the build setting properly
            SceneManager.LoadScene(SceneList[levelNumber], LoadSceneMode.Single);
#endif
        }
    }

#if UNITY_EDITOR
    class BuildLevelList : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                var levelListAssets = AssetDatabase.FindAssets("t:LevelList");

                if (levelListAssets.Length == 0)
                {
                    throw new BuildFailedException("Couldn't find a level list, aborting the build");
                }

                var levelList =
                    AssetDatabase.LoadAssetAtPath<LevelList>(AssetDatabase.GUIDToAssetPath(levelListAssets[0]));

                if (levelList.Scenes.Length == 0)
                {
                    throw new BuildFailedException("Level list scenes array is empty, aborting the build");
                }

                var buildLevels = EditorBuildSettings.scenes;
                var levels = new int[levelList.Scenes.Length];

                bool buildListChange = false;

                for (int i = 0; i < levelList.Scenes.Length; ++i)
                {
                    var sceneAsset = levelList.Scenes[i];
                    var scenePath = AssetDatabase.GetAssetPath(sceneAsset);

                    if (sceneAsset == null)
                    {
                        throw new BuildFailedException("The level list contains a null scene, fix before rebuilding");
                    }

                    var idx = Array.FindIndex(buildLevels, scene => scene.path == scenePath);

                    if (idx == -1)
                    {
                        idx = buildLevels.Length - 1;
                        ArrayUtility.Add(ref buildLevels, new EditorBuildSettingsScene(scenePath, true));
                        buildListChange = true;
                    }
                    else if (!buildLevels[idx].enabled)
                    {
                        buildLevels[idx].enabled = true;
                        buildListChange = true;
                    }

                    levels[i] = idx;
                }

                bool levelListChanged = false;
                for (int i = 0; i < levels.Length; ++i)
                {
                    if (i >= levelList.SceneList.Length || levels[i] != levelList.SceneList[i])
                    {
                        levelListChanged = true;
                        break;
                    }
                }

                if (levelListChanged)
                {
                    levelList.SceneList = levels;
                    EditorUtility.SetDirty(levelList);
                    AssetDatabase.SaveAssetIfDirty(levelList);
                }

                if (levelListChanged || buildListChange)
                {
                    EditorBuildSettings.scenes = buildLevels;
                    EditorUtility.DisplayDialog("Build Stopped",
                        "The scene list from the build had to be changed to match the list in the LevelList assets.\n" +
                        "the scene list have now been fixed, Please restart the build.", "OK");

                    throw new BuildFailedException("Level List had to be rebuilt, restart the build");
                }
            }
            catch (Exception e)
            {
                throw new BuildFailedException($"Exception during prebuild {e.Message}");
            }
        }
    }
#endif
}