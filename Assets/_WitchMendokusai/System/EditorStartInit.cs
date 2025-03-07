#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WitchMendokusai
{

	// https://mentum.tistory.com/657
	[InitializeOnLoad]
	public class EditorStartInit
	{
		static EditorStartInit()
		{
			string pathOfFirstScene = EditorBuildSettings.scenes[0].path;
			SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathOfFirstScene);
			EditorSceneManager.playModeStartScene = sceneAsset;
		}
	}
}
#endif
