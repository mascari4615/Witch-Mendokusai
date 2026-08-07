using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-171 — 돌봄·배웅을 실제 게임(World 씬)에 얹는다.
	///
	/// ★ 씬 파일은 안 고친다: 여러 세션이 같이 쓰는 파일이라 손대면 충돌한다.
	///   자율 삶 층·공방·레이라인이 이미 쓰는 방식(놀이 시작할 때 코드로 얹기)을 그대로 따른다.
	/// </summary>
	public static class CareWorldBootstrap
	{
		private const string WORLD_SCENE = "World";
		private const string CARE_ROOT_NAME = "[Care] 돌봄과 배웅";

		/// <summary>false 면 안 얹는다.</summary>
		public static bool Enabled { get; set; } = true;

		/// <summary>false 면 소원을 하나도 안 건 채로 얹는다 — 진짜 소원이 생기면 여기부터 끈다.</summary>
		public static bool DemoEnabled { get; set; } = true;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Hook()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.isLoaded)
				{
					TryAttach(scene);
				}
			}
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach(scene);

		private static void TryAttach(Scene scene)
		{
			if (Enabled == false || scene.name != WORLD_SCENE)
			{
				return;
			}

			if (GameObject.Find(CARE_ROOT_NAME) != null)
			{
				return; // 이미 얹힘 — 1회만.
			}

			GameObject root = new GameObject(CARE_ROOT_NAME);
			SceneManager.MoveGameObjectToScene(root, scene);

			root.AddComponent<WishKeeper>();

			if (DemoEnabled == true)
			{
				root.AddComponent<CareDemoWish>();
			}
		}
	}
}
