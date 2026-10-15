using UnityEditor;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ContentStageView : SceneView
    {
        public void ShowPreview(Scene scene)
        {
            customScene = scene;
        }
    }
}
