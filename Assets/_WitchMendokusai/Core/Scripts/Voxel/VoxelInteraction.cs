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
		[SerializeField] private float reachDistance = 10f;
		
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

			// 좌클릭: 파괴, 우클릭: 설치
			bool breakInput = Mouse.current.leftButton.wasPressedThisFrame;
			bool placeInput = Mouse.current.rightButton.wasPressedThisFrame;

			if (breakInput || placeInput)
			{
				Vector2 mousePos = Mouse.current.position.ReadValue();
				Ray ray = mainCamera.ScreenPointToRay(mousePos);

				if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
				{
					// 블록의 정중앙 좌표가 아니라 격자 공간을 구해야 하므로, 
					// normal 방향으로 살짝 이동하여 안쪽/바깥쪽 공간을 특정합니다.
					Vector3 targetPos = breakInput 
						? hit.point - hit.normal * 0.1f   // 파괴 시: 충돌면 안쪽으로 살짝 들어감
						: hit.point + hit.normal * 0.1f;  // 설치 시: 충돌면 바깥쪽으로 살짝 나옴

					// Unity 월드 좌표 -> Voxel 그리드 좌표 변환
					// (현재 청크 오브젝트가 Y=-32로 내려가 있으므로 보정해줌)
					float yOffset = VoxelConstants.CHUNK_SIZE_Y / 2f;
					
					int voxelX = Mathf.FloorToInt(targetPos.x);
					int voxelY = Mathf.FloorToInt(targetPos.y + yOffset);
					int voxelZ = Mathf.FloorToInt(targetPos.z);

					ushort newBlockId = breakInput ? VoxelConstants.AIR_RUNTIME_ID : placeBlockId;
					
					chunkManager.SetBlock(voxelX, voxelY, voxelZ, newBlockId);
				}
			}
		}
	}
}
