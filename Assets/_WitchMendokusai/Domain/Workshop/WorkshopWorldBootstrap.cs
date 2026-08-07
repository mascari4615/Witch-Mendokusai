using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-170 — 공방 감독을 *실제 게임*(World 씬)에 런타임으로 얹는 부트스트랩.
	///
	/// ★ 왜 씬을 직접 안 고치나: World 씬은 여러 세션이 같이 쓰는 파일이라 손대면 충돌이 난다.
	///   자율 삶 층(<see cref="LifeWorldBootstrap"/>)이 같은 이유로 이미 이 방식을 쓴다 — 그 선례를 따른다.
	///
	/// ★ 왜 이게 필요한가: 앞선 배선으로 낮밤 교대·밤 장사가 <b>돌 준비</b>는 됐는데,
	///   그 부품이 어느 씬에도 없어 <b>한 번도 안 돌았다.</b> 「만들었지만 안 도는 것」은
	///   이 TASK 가 고치려던 바로 그 상태다. 여기서 실제로 얹는다.
	///
	/// 상품 에셋이 하나도 없으면 얹혀도 <b>아무 일도 안 일어난다</b> — 단계만 조용히 바뀐다.
	/// </summary>
	public static class WorkshopWorldBootstrap
	{
		// 공방을 얹을 게임 씬 이름. 이 씬일 때만 얹는다(Boot/로딩 씬엔 안 얹음).
		private const string WORLD_SCENE = "World";
		private const string WORKSHOP_ROOT_NAME = "[Workshop] 공방 감독";

		/// <summary>false 면 안 얹는다. 씬에 직접 배치하기로 바꾸면 여기부터 끄면 된다.</summary>
		public static bool Enabled { get; set; } = true;

		// Play 진입 시 구독 + 즉시 검사. World 로 바로 Play 를 시작하면 그 sceneLoaded 가 이 훅 *전*에
		// 끝나 놓치므로, 이미 떠 있는 씬도 그 자리에서 한 번 본다(LifeWorldBootstrap 과 같은 이유).
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Hook()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded; // 도메인 재로드 잔여 중복 구독 방지.
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

			if (GameObject.Find(WORKSHOP_ROOT_NAME) != null)
			{
				return; // 이미 얹힘(재진입·중복 경로) — 1회만.
			}

			GameObject root = new GameObject(WORKSHOP_ROOT_NAME);
			SceneManager.MoveGameObjectToScene(root, scene); // 씬 언로드 시 같이 정리되게.

			// 감독이 자기 Start 에서 세계 시계를 찾아 붙는다. 시계가 없으면 조용히 쉰다.
			root.AddComponent<WorkshopDirector>();
		}
	}
}
