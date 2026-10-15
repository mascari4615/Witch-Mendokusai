using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ContentStageView : SceneView
    {
        public void ShowPreview(Scene scene)
        {
            customScene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        }

        public void PreparePreviewCamera()
        {
            if (customScene.IsValid())
            {
                camera.scene = customScene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(customScene);
            }
        }
    }
}
