using UnityEngine;
using UnityEngine.InputSystem;

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

		[Header("Place Settings")]
		[SerializeField] private string placeBlockIdentifier = "wm:stone";

		private bool inputRegistered;

		private void Start()
		{
			if (mainCamera == null)
				mainCamera = Camera.main;

			GameModeManager.Instance.OnModeChanged += OnGameModeChanged;
			OnGameModeChanged(GameModeManager.Instance.CurrentMode);
		}

		private void OnDestroy()
		{
			UnregisterInput();

			if (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager))
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
			InputManager.Instance.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Performed, OnBreakBlock);
			InputManager.Instance.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Performed, OnPlaceBlock);
			inputRegistered = true;
		}

		private void UnregisterInput()
		{
			if (inputRegistered == false)
				return;
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
			{
				inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Performed, OnBreakBlock);
				inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Performed, OnPlaceBlock);
			}
			inputRegistered = false;
		}

		private void OnBreakBlock() => HandleClick(true);
		private void OnPlaceBlock() => HandleClick(false);

		private void HandleClick(bool isBreak)
		{
			if (chunkManager == null || mainCamera == null)
				return;
			if (Mouse.current == null)
				return;
			if (InputManager.Instance.IsPointerOverUI())
				return;

			Vector2 mousePos = InputManager.Instance.MouseScreenPosition;
			Ray ray = mainCamera.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out RaycastHit hit, reachDistance) == false)
				return;

			Vector3 targetPos = isBreak
				? hit.point - hit.normal * 0.1f
				: hit.point + hit.normal * 0.1f;

			float yOffset = VoxelConstants.CHUNK_SIZE_Y / 2f;

			int voxelX = Mathf.FloorToInt(targetPos.x);
			int voxelY = Mathf.FloorToInt(targetPos.y + yOffset);
			int voxelZ = Mathf.FloorToInt(targetPos.z);

			ushort newBlockId;
			if (isBreak)
			{
				newBlockId = VoxelConstants.AIR_RUNTIME_ID;
			}
			else
			{
				BlockData placeBlock = BlockRegistry.GetByIdentifier(placeBlockIdentifier);
				if (placeBlock == null)
				{
					Debug.LogError($"[VoxelInteraction] Place block not registered: {placeBlockIdentifier}");
					return;
				}
				newBlockId = placeBlock.RuntimeId;
			}

			chunkManager.SetBlock(voxelX, voxelY, voxelZ, newBlockId);
		}
	}
}
