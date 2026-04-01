#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GreyboxArenaEditorBootstrap
{
    static GreyboxArenaEditorBootstrap()
    {
        EditorApplication.delayCall -= EnsureBootstrapExists;
        EditorApplication.delayCall += EnsureBootstrapExists;
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall -= EnsureBootstrapExists;
        EditorApplication.delayCall += EnsureBootstrapExists;
    }

    private static void EnsureBootstrapExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        GreyboxArenaBootstrap existing = Object.FindFirstObjectByType<GreyboxArenaBootstrap>();
        if (existing == null)
        {
            GameObject bootstrapObject = new GameObject("Greybox Arena Bootstrap");
            existing = bootstrapObject.AddComponent<GreyboxArenaBootstrap>();
        }

        existing.BuildArena();
        EditorSceneManager.MarkSceneDirty(activeScene);
    }

    [MenuItem("Tools/Greybox/Force Rebuild Arena")]
    private static void ForceRebuildArena()
    {
        GreyboxArenaBootstrap bootstrap = Object.FindFirstObjectByType<GreyboxArenaBootstrap>();
        if (bootstrap == null)
        {
            GameObject bootstrapObject = new GameObject("Greybox Arena Bootstrap");
            bootstrap = bootstrapObject.AddComponent<GreyboxArenaBootstrap>();
        }

        bootstrap.BuildArena();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = bootstrap.gameObject;
    }
}
#endif
