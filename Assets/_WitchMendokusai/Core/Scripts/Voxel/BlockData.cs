using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 단일 블록 정의. namespaced Identifier가 영구 고유 ID.
	/// RuntimeId는 부팅 시 BlockRegistry가 할당 — 직렬화 안 함 (부팅마다 달라질 수 있음).
	/// 세이브 시 청크 palette로 Identifier ↔ RuntimeId 변환 (TASK-027-E).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(BlockData), menuName = "WM/Voxel/" + nameof(BlockData))]
	public class BlockData : ScriptableObject
	{
		[SerializeField] private string identifier = "wm:block";
		[SerializeField] private string blockName = "Block";
		[SerializeField] private Color color = Color.gray;
		[SerializeField] private bool isSolid = true;
		[SerializeField] private bool isOpaque = true;

		[System.NonSerialized] private ushort runtimeId;

		public string Identifier => identifier;
		public string BlockName => blockName;
		public Color Color => color;
		public bool IsSolid => isSolid;
		public bool IsOpaque => isOpaque;
		public ushort RuntimeId => runtimeId;

		public bool IsAir => identifier == VoxelConstants.AIR_IDENTIFIER;

		public void SetIdentifier(string value) => identifier = value;
		public void SetBlockName(string value) => blockName = value;
		public void SetColor(Color value) => color = value;
		public void SetIsSolid(bool value) => isSolid = value;
		public void SetIsOpaque(bool value) => isOpaque = value;

		/// <summary>BlockRegistry 전용. 외부 호출 금지.</summary>
		public void AssignRuntimeId(ushort id) => runtimeId = id;
	}
}
