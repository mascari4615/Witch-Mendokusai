using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-173 — 레이라인을 실제 게임(World 씬)에 얹고, <b>돌아가는 걸 눈에 보이게</b> 한다.
	///
	/// ★ 씬 파일은 안 고친다: 여러 세션이 같이 쓰는 파일이라 손대면 충돌한다.
	///   자율 삶 층·공방이 이미 쓰는 방식(놀이 시작할 때 코드로 얹기)을 그대로 따른다.
	///
	/// ★ 더미 망은 <b>로어 아니다</b>: 마력이 어디서 나서 어디로 가는지는 사용자가 정할 문제다.
	///   여기 「샘 → 중계 → 공방」은 <b>메커니즘이 돈다는 걸 보여주는 자리표</b>다.
	///   진짜 거점이 생기면 토글 하나 끄면 통째로 사라진다.
	/// </summary>
	public static class LeylineWorldBootstrap
	{
		private const string WORLD_SCENE = "World";
		private const string LEYLINE_ROOT_NAME = "[Leyline] 마력의 강";

		// 더미 망(시연 손잡이). 진짜 거점이 생기면 통째로 사라질 값들.
		private const string DEMO_SOURCE = "샘";
		private const string DEMO_RELAY = "중계석";
		private const string DEMO_SINK = "공방";
		private const float DEMO_SOURCE_TO_RELAY = 6f;
		private const float DEMO_RELAY_TO_SINK = 9f;
		private const float DEMO_SEND_PER_HOUR = 20f;

		/// <summary>false 면 안 얹는다.</summary>
		public static bool Enabled { get; set; } = true;

		/// <summary>false 면 망을 비운 채로 얹는다 — 마을이 자기 거점을 얹기 시작하면 여기부터 끈다.</summary>
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

			if (GameObject.Find(LEYLINE_ROOT_NAME) != null)
			{
				return; // 이미 얹힘 — 1회만.
			}

			GameObject root = new GameObject(LEYLINE_ROOT_NAME);
			SceneManager.MoveGameObjectToScene(root, scene);

			LeylineDirector director = root.AddComponent<LeylineDirector>();

			if (DemoEnabled == true)
			{
				root.AddComponent<LeylineDemoPulse>().Configure(
					director, DEMO_SOURCE, DEMO_RELAY, DEMO_SINK,
					DEMO_SOURCE_TO_RELAY, DEMO_RELAY_TO_SINK, DEMO_SEND_PER_HOUR);
			}
		}
	}
}
