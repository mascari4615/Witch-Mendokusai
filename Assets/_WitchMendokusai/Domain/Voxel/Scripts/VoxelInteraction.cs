using UnityEngine;
using VContainer;

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

		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager)
		{
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
		}

		private HotbarView EnsureHotbarView()
		{
			if (hotbarView != null)
				return hotbarView;
			hotbarView = FindAnyObjectByType<HotbarView>(FindObjectsInactive.Include);
			return hotbarView;
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

			TryPlantFromHotbar(hit);
		}

		/// <summary>
		/// 핫바 selectedItem 이 EntityData wire 된 SeedItemData면 hit 위치에 entity 심음.
		/// true = plant 처리됨 (블록 설치 분기 스킵), false = 일반 블록 설치 진행.
		/// </summary>
		private bool TryPlantFromHotbar(RaycastHit hit)
		{
			HotbarView view = EnsureHotbarView();
			if (view == null)
				return false;

			Item selectedItem = view.SelectedItem;
			if (selectedItem == null || selectedItem.IsEmpty)
				return false;

			SeedItemData seed = selectedItem.Data as SeedItemData;
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
