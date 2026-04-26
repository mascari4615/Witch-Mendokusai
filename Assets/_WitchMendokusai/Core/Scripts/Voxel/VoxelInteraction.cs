using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	/// <summary>
	/// 플레이어/카메라에서 마우스 위치로 레이를 쏴서 블록을 부수거나 설치합니다.
	/// </summary>
	public class VoxelInteraction : MonoBehaviour
	{
		[SerializeField] private ChunkManager chunkManager;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private float reachDistance = 50f;
		
		[Header("Place Settings")]
		[SerializeField] private ushort placeBlockId = 1; // 1 = Stone

		private void Start()
		{
			if (mainCamera == null)
				mainCamera = Camera.main;
		}

		private void Update()
		{
			if (chunkManager == null || mainCamera == null)
				return;

			if (Mouse.current == null)
				return;

			// 좌클릭: 파괴, 우클릭: 설치
			bool breakInput = false;
			bool placeInput = false;

			if (Mouse.current != null)
			{
				breakInput = Mouse.current.leftButton.wasPressedThisFrame;
				placeInput = Mouse.current.rightButton.wasPressedThisFrame;
			}
			
			if (breakInput || placeInput)
			{
				Vector2 mousePos = Mouse.current.position.ReadValue();
				Ray ray = mainCamera.ScreenPointToRay(mousePos);

				if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
				{
					Debug.Log($"[VoxelInteraction] Raycast Hit: {hit.collider.gameObject.name} at {hit.point}");

					Vector3 targetPos = breakInput 
						? hit.point - hit.normal * 0.1f
						: hit.point + hit.normal * 0.1f;

					float yOffset = VoxelConstants.CHUNK_SIZE_Y / 2f;
					
					int voxelX = Mathf.FloorToInt(targetPos.x);
					int voxelY = Mathf.FloorToInt(targetPos.y + yOffset);
					int voxelZ = Mathf.FloorToInt(targetPos.z);

					Debug.Log($"[VoxelInteraction] Target Voxel: ({voxelX}, {voxelY}, {voxelZ})");

					ushort newBlockId = breakInput ? VoxelConstants.AIR_RUNTIME_ID : placeBlockId;
					chunkManager.SetBlock(voxelX, voxelY, voxelZ, newBlockId);
				}
				else
				{
					Debug.Log($"[VoxelInteraction] Raycast missed. Reach distance: {reachDistance}");
				}
			}
		}
	}
}
