using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	/// <summary>
	/// 카메라 → 마우스 ray로 블록을 부수거나 설치한다.
	/// Default 모드에서만 동작 (Build/기타 모드는 자체 인터랙션을 갖는다).
	/// </summary>
	public class VoxelInteraction : MonoBehaviour
	{
		[SerializeField] private ChunkManager chunkManager;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private float reachDistance = 50f;

		private bool inputRegistered;

		private GameModeManager gameModeManager;
		private InputManager inputManager;
		// HotbarView 는 UI 도메인 (메인 scene) 이고 VoxelInteraction 은 stage pool-spawn prefab 자식 →
		// InjectGameObject 가 HotbarView 못 찾아 VContainer throw. 사용 시점 lazy resolve 로 분리. init-order-ok.
		private HotbarView hotbarView;
		// 복셀 땅 위의 밭 (TASK-WM-410). 스테이지 스코프라 사용 시점 lazy resolve. init-order-ok.
		private FarmGroundObject farmGround;
		private GameLogic gameLogic;
		private UIManager uiManager;

		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager, GameLogic gameLogic, UIManager uiManager)
		{
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
			this.gameLogic = gameLogic;
			this.uiManager = uiManager;
		}

		private HotbarView EnsureHotbarView()
		{
			if (hotbarView != null)
				return hotbarView;
			hotbarView = FindAnyObjectByType<HotbarView>(FindObjectsInactive.Include);
			return hotbarView;
		}

		private FarmGroundObject EnsureFarmGround()
		{
			if (farmGround != null)
				return farmGround;
			farmGround = FindAnyObjectByType<FarmGroundObject>(FindObjectsInactive.Include);
			return farmGround;
		}

		private void Start()
		{
			if (mainCamera == null)
				mainCamera = Camera.main;

			gameModeManager.OnModeChanged += OnGameModeChanged;
			OnGameModeChanged(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			UnregisterInput();

			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
		}

		private void OnGameModeChanged(GameMode mode)
		{
			if (mode == GameMode.Default)
				RegisterInput();
			else
				UnregisterInput();
		}

		private void RegisterInput()
		{
			if (inputRegistered)
				return;
			// TASK-WM-181 — 복셀 블록 편집(부수기/설치)은 빌드모드(BuildManager) 단일 핸들러로 통일.
			// 일반(Default) 모드는 씨앗 심기만 (우클릭). 좌클릭 부수기·블록 설치 제거 → 플레이 중 실수 지형 편집 차단.
			inputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Performed, OnPlaceSeed);
			inputRegistered = true;
		}

		private void UnregisterInput()
		{
			if (inputRegistered == false)
				return;
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Performed, OnPlaceSeed);
			inputRegistered = false;
		}

		// 일반 모드 우클릭 = 핫바 씨앗(SeedItemData)을 hit 위치에 심기. 씨앗 아니면 아무것도 X (블록 설치 폐기).
		private void OnPlaceSeed()
		{
			if (chunkManager == null || mainCamera == null)
				return;
			if (inputManager.IsMouseAvailable == false)
				return;
			if (inputManager.IsPointerOverUI())
				return;

			Ray ray = mainCamera.ScreenPointToRay(inputManager.MouseScreenPosition);
			if (Physics.Raycast(ray, out RaycastHit hit, reachDistance) == false)
				return;

			if (TryFarmAt(hit))
				return;

			TryPlantFromHotbar(hit);
		}


		/// <summary>
		/// 밭 한 칸을 매만진다 (TASK-WM-410) — 개화했으면 거두고, 갈렸으면 심고, 굳었으면 간다.
		/// true = 밭이 처리함(옛 경로 스킵).
		///
		/// ★ 도구가 아직 없어서 「씨앗을 든 손」이 갈기까지 한다. 갈기와 심기는 <b>따로 걸리는 두 행동</b>이라
		///   대가(시간·기운)는 각각 정확히 문다 — 나중에 괭이가 생기면 갈기만 떼어 가면 된다.
		/// </summary>
		private bool TryFarmAt(RaycastHit hit)
		{
			FarmGroundObject farm = EnsureFarmGround();
			if (farm == null)
				return false;

			// 맞은 면의 <b>안쪽</b> 블록이 그 땅이다(면 바깥은 허공).
			Vector3 inside = hit.point - hit.normal * 0.5f;
			FarmCoord soil = FarmCoord.FromWorld(inside.x, inside.y, inside.z);

			if (farm.TryHarvest(soil, out HarvestResult harvest, out _))
			{
				OnFarmHarvested(harvest, hit.point);
				return true;
			}

			SeedItemData seed = SelectedSeed();
			if (seed == null || seed.Plant == null)
				return false;

			if (farm.TryPlant(soil, seed, out ActOutcome planted))
				return true;

			if (planted.Rejection != ActRejection.None)
			{
				ShowRejection(planted);
				return true;
			}

			// 아직 굳은 땅이면 먼저 간다 — 이 클릭은 여기까지, 심기는 다음 클릭.
			if (farm.TryTill(soil, out ActOutcome tilled))
				return true;

			if (tilled.Rejection != ActRejection.None)
			{
				ShowRejection(tilled);
				return true;
			}

			return false;
		}

		// 거둔 것 = 작물의 수확물 표에 따라 바닥에 떨어뜨린다(기존 밭과 같은 길 — GameLogic.SpawnLootItem).
		// 누가 가장 돌봤나(변이)는 작물 SO 가 판정한다(WitchPlantSO.ResolveCarerVariant) — 여기서 흉내내지 않는다.
		private void OnFarmHarvested(HarvestResult harvest, Vector3 position)
		{
			WitchPlantSO plant = SOHelper.Get<WitchPlantSO>(harvest.PlantDataId);
			if (plant == null || gameLogic == null)
				return;

			ItemData variant = WitchPlantSO.ResolveCarerVariant(plant.CarerLoots, harvest.HasDominantCarer, harvest.DominantCarerId);
			if (variant != null)
			{
				gameLogic.SpawnLootItem(new System.Collections.Generic.List<DataSOWithPercentage>
				{
					new() { DataSO = variant, Percentage = 100f },
				}, position);
				return;
			}

			gameLogic.SpawnLootItem(plant.HarvestLoots, position);
		}

		// 왜 안 됐는지는 그 자리에서 말해 준다 — 조용한 실패는 「고장」으로 읽힌다.
		private void ShowRejection(ActOutcome outcome)
		{
			if (uiManager == null)
				return;

			string reason = outcome.Rejection == ActRejection.Resource ? "씨앗이 없다..." : "기운이 없다...";
			uiManager.SpeechBubble.Show(transform, reason);
		}

		private SeedItemData SelectedSeed()
		{
			HotbarView view = EnsureHotbarView();
			if (view == null)
				return null;

			Item selectedItem = view.SelectedItem;
			if (selectedItem == null || selectedItem.IsEmpty)
				return null;

			return selectedItem.Data as SeedItemData;
		}

		/// <summary>
		/// 핫바 selectedItem 이 EntityData wire 된 SeedItemData면 hit 위치에 entity 심음.
		/// true = plant 처리됨 (블록 설치 분기 스킵), false = 일반 블록 설치 진행.
		/// </summary>
		private bool TryPlantFromHotbar(RaycastHit hit)
		{
			SeedItemData seed = SelectedSeed();
			if (seed == null || seed.PlantedEntity == null)
				return false;

			// 심기 위치 = hit 면 바깥쪽 0.001 보정 (블록 안 박힘 방지).
			// hit.normal 이 위쪽(상면) = surface — XZ는 자유 위치, Y는 hit point 그대로 (entity prefab pivot = 발 정합).
			Vector3 plantPos = hit.point + hit.normal * 0.001f;
			bool planted = chunkManager.PlantEntityAt(plantPos, seed.PlantedEntity);

			if (planted == false)
				Debug.LogError($"[VoxelInteraction] PlantEntityAt 실패 — chunk 비활성: {plantPos}.");

			return true;
		}
	}
}
