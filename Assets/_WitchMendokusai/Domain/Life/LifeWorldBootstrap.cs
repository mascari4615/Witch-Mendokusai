using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-5d — 자율 삶 레이어를 *실제 게임*(World 씬)에 런타임 스폰하는 부트스트랩.
	/// World.unity 를 직접 편집하지 않는다(5세션 공유 씬 commit race 회피) — Play 시 코드로 더미 주민을 깐다.
	///
	/// 지금은 메커니즘 first-use 더미(캡슐 N체, 로어 미정 — 사용자 결정 2026-05-31). 색이 휙휙 = 자율 두뇌 가시 증거.
	/// 위치·개수·외형 = 시각(사용자 영역) → 수치 노출 + <see cref="Enabled"/> 토글로 쉽게 끄거나 옮긴다.
	/// 미래(INC-7): LifeProfileSO + UGC 입주가 이 하드코딩 더미를 대체.
	/// </summary>
	public static class LifeWorldBootstrap
	{
		// 자율 삶 더미를 깔 게임 씬 이름. 이 씬이 로드될 때만 스폰(Boot/로딩 씬엔 안 깖).
		private const string WORLD_SCENE = "World";
		// 스폰 더미 수(수치노출 — 시각 손잡이). 위상차로 같은 순간 다른 색.
		private const int DUMMY_COUNT = 3;
		// 광장 중심(월드 좌표) + 더미 간격(수치노출). 월드 레이아웃 모르는 dev 디폴트 — 사용자가 옮김.
		private static readonly Vector3 PLAZA_CENTER = new(0f, 1f, 6f);
		private const float DUMMY_SPACING = 2.5f;
		private const string VILLAGE_ROOT_NAME = "[Life] 더미 마을 (프리뷰)";

		/// <summary>false 면 게임에 더미를 안 깖(로어 캐릭터 배선 후 끄기 등). 코드 토글 — 수동 경로.</summary>
		public static bool Enabled { get; set; } = true;

		// Play 진입(AfterSceneLoad) 시 구독 + 즉시 검사. WM 부트는 Boot→World(나중 로드)지만, World 로 바로 Play 시작하면
		// 그 sceneLoaded 가 이 훅 *전*에 끝나 놓친다 → 이미 로드된 World 도 즉시 검사(둘 다 커버).
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
					TrySpawn(scene); // 훅 시점에 World 가 이미 떠 있으면 지금 스폰(이벤트 미발화 케이스).
				}
			}
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawn(scene);

		private static void TrySpawn(Scene scene)
		{
			if (Enabled == false || scene.name != WORLD_SCENE)
			{
				return;
			}

			if (GameObject.Find(VILLAGE_ROOT_NAME) != null)
			{
				return; // 이미 깔림(재진입·중복 경로) — 1회만.
			}

			SpawnVillage(scene);
		}

		private static void SpawnVillage(Scene worldScene)
		{
			GameObject root = new(VILLAGE_ROOT_NAME);
			SceneManager.MoveGameObjectToScene(root, worldScene); // World 씬 소속(씬 언로드 시 같이 정리).

			GameObject labelPrefab = Resources.Load<GameObject>("Life/LifeLabel"); // 한글 라벨(갈무리). 없으면 색+위치만.

			SpawnZones(root, labelPrefab);
			SpawnDummies(root, labelPrefab);

			// LifeDirector 가 같은 씬 LifeAgent·LifeZone 발견→프로필/위상/장소/시계 주입(자기 Start). 더미·존 *뒤* 부착.
			root.AddComponent<LifeDirector>();

			Debug.Log($"[Life] 더미 마을 스폰: 주민 {DUMMY_COUNT} + 장소 4(식당/침대/공방/수다터) @ {PLAZA_CENTER} (World). 끄기 = LifeWorldBootstrap.Enabled=false.");
		}

		// 활동 장소 4곳 — 활동 색 패드 + 한글 라벨. 캐릭터가 그 활동을 고르면 같은 색 패드로 걸어가 머문다.
		private static void SpawnZones(GameObject root, GameObject labelPrefab)
		{
			(WitchMendokusai.DomainSDK.Life.ActivityKind activity, Vector3 offset, string label)[] zones =
			{
				(WitchMendokusai.DomainSDK.Life.ActivityKind.Eat, new Vector3(-6f, 0f, 3f), "식당"),
				(WitchMendokusai.DomainSDK.Life.ActivityKind.Sleep, new Vector3(-6f, 0f, -3f), "침대"),
				(WitchMendokusai.DomainSDK.Life.ActivityKind.Hobby, new Vector3(6f, 0f, 3f), "공방"),
				(WitchMendokusai.DomainSDK.Life.ActivityKind.Socialize, new Vector3(6f, 0f, -3f), "수다터"),
			};

			foreach ((WitchMendokusai.DomainSDK.Life.ActivityKind activity, Vector3 offset, string label) zone in zones)
			{
				GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
				pad.name = $"장소 [{zone.label}]";
				pad.transform.SetParent(root.transform);
				pad.transform.localScale = new Vector3(3f, 0.1f, 3f); // 납작한 패드.
				pad.transform.position = PLAZA_CENTER + zone.offset + new Vector3(0f, -0.95f, 0f); // 바닥에 깔림.

				MeshRenderer renderer = pad.GetComponent<MeshRenderer>();
				if (renderer != null)
				{
					renderer.material.color = LifeAgent.ColorForActivity(zone.activity); // 캐릭터 색과 동일 = "이 색=이 장소".
				}

				LifeZone lifeZone = pad.AddComponent<LifeZone>();
				lifeZone.SetActivity(zone.activity);

				// 라벨은 root(스케일1) 자식으로 패드 위에 — 패드 비균일 스케일에 글자 찌그러짐 방지.
				AttachLabel(labelPrefab, root.transform, pad.transform.position + new Vector3(0f, 1.5f, 0f), zone.label);
			}
		}

		// 더미 주민 N체 — 광장 중앙 근처서 시작, 위상차로 첫 활동이 제각각. 머리 위 한글 상태 라벨.
		private static void SpawnDummies(GameObject root, GameObject labelPrefab)
		{
			float start = -(DUMMY_COUNT - 1) * DUMMY_SPACING * 0.5f;
			for (int index = 0; index < DUMMY_COUNT; index++)
			{
				GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
				dummy.name = $"더미 주민 {index + 1}"; // 루트에 이미 [Life] — 로그 `[Life] 더미 주민 N` 깔끔.
				dummy.transform.SetParent(root.transform);
				dummy.transform.position = PLAZA_CENTER + new Vector3(start + index * DUMMY_SPACING, 0f, 0f);

				LifeAgent agent = dummy.AddComponent<LifeAgent>();
				agent.LogActivityChanges = true; // 헤드리스 검증: 전환마다 `[Life]` 로그.

				AttachLabel(labelPrefab, dummy.transform, new Vector3(0f, 1.6f, 0f), null); // 머리 위 상태(자동: 부모 LifeAgent).
			}
		}

		// 라벨 프리팹 인스턴스를 부모에 붙임. staticText 있으면 장소 고정 라벨, null 이면 부모 LifeAgent 상태 자동.
		private static void AttachLabel(GameObject labelPrefab, Transform parent, Vector3 localOffset, string staticText)
		{
			if (labelPrefab == null)
			{
				return; // 프리팹 없음(미생성) → 라벨 생략, 색+위치만으로 동작.
			}

			GameObject label = Object.Instantiate(labelPrefab, parent);
			label.transform.localPosition = localOffset;
			if (staticText != null)
			{
				LifeLabel lifeLabel = label.GetComponent<LifeLabel>();
				if (lifeLabel != null)
				{
					lifeLabel.SetStaticText(staticText);
				}
			}
		}
	}
}
