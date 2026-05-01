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

		[Header("Atlas Tiles (BlockTextureAtlas tile name; empty = vertex color fallback)")]
		[SerializeField] private string sideTileName = "";
		[SerializeField] private string topTileName = "";
		[SerializeField] private string bottomTileName = "";

		[System.NonSerialized] private ushort runtimeId;

		public string Identifier => identifier;
		public string BlockName => blockName;
		public Color Color => color;
		public bool IsSolid => isSolid;
		public bool IsOpaque => isOpaque;
		public ushort RuntimeId => runtimeId;

		/// <summary>옆 4면 텍스쳐 이름. 빈 문자열이면 atlas 미할당 → mesher가 vertex color fallback.</summary>
		public string SideTileName => sideTileName;
		/// <summary>윗면 텍스쳐 이름. 빈 문자열이면 SideTileName 사용 (마인크래프트식 잔디 = top "grass_top", side "grass_side").</summary>
		public string TopTileName => string.IsNullOrEmpty(topTileName) ? sideTileName : topTileName;
		/// <summary>아랫면 텍스쳐 이름. 빈 문자열이면 SideTileName 사용.</summary>
		public string BottomTileName => string.IsNullOrEmpty(bottomTileName) ? sideTileName : bottomTileName;

		public bool IsAir => identifier == VoxelConstants.AIR_IDENTIFIER;

		public void SetIdentifier(string value) => identifier = value;
		public void SetBlockName(string value) => blockName = value;
		public void SetColor(Color value) => color = value;
		public void SetIsSolid(bool value) => isSolid = value;
		public void SetIsOpaque(bool value) => isOpaque = value;
		public void SetSideTileName(string value) => sideTileName = value ?? "";
		public void SetTopTileName(string value) => topTileName = value ?? "";
		public void SetBottomTileName(string value) => bottomTileName = value ?? "";

		/// <summary>BlockRegistry 전용. 외부 호출 금지.</summary>
		public void AssignRuntimeId(ushort id) => runtimeId = id;
	}
}
