using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 단일 블록 정의. namespaced Identifier가 영구 고유 ID.
	/// RuntimeId는 부팅 시 BlockRegistry가 할당 — 직렬화 안 함 (부팅마다 달라질 수 있음).
	/// 세이브 시 청크 palette로 Identifier ↔ RuntimeId 변환 (TASK-027-E).
	///
	/// 텍스쳐: 6면을 top/side/bottom 3개로 분리. side 만 채우면 6면 동일 (마인크래프트식 단순 블록).
	/// top/bottom 이 null 이면 side 사용 (잔디 = top: grass_top, side: grass_side, bottom: dirt).
	/// Texture2D 가 null 이면 atlas 미할당 → mesher 가 (-1,-1) UV 센티널 emit → 셰이더 fallback.
	/// UV rect 는 BlockAtlasBuilder 가 atlas 빌드 시 직접 채움 (직렬화).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(BlockData), menuName = "WM/Voxel/" + nameof(BlockData))]
	public class BlockData : ScriptableObject
	{
		[SerializeField] private string identifier = "wm:block";
		[SerializeField] private string blockName = "Block";
		[SerializeField] private Color color = Color.gray;
		[SerializeField] private bool isSolid = true;
		[SerializeField] private bool isOpaque = true;
		[Tooltip("식물성 블록 (잔디·잎 등) 만 true. mesher 가 column biome.PreviewColor 을 vertex color 에 곱 → atlas/sentinel 모두 색조 변화. 돌·모래 등 무관 블록은 false.")]
		[SerializeField] private bool acceptsBiomeTint = false;

		[Header("Atlas Faces (drag Texture2D — null = atlas 미할당, vertex color fallback)")]
		[SerializeField] private Texture2D sideTexture;
		[SerializeField] private Texture2D topTexture;
		[SerializeField] private Texture2D bottomTexture;

		[Header("UV Rect (BlockAtlasBuilder 결과 — 수동 편집 X)")]
		[SerializeField] private Rect sideUVRect;
		[SerializeField] private Rect topUVRect;
		[SerializeField] private Rect bottomUVRect;

		[System.NonSerialized] private ushort runtimeId;

		public string Identifier => identifier;
		public string BlockName => blockName;
		public Color Color => color;
		public bool IsSolid => isSolid;
		public bool IsOpaque => isOpaque;
		public bool AcceptsBiomeTint => acceptsBiomeTint;
		public ushort RuntimeId => runtimeId;

		public Texture2D SideTexture => sideTexture;
		/// <summary>윗면 텍스쳐. null 이면 side 사용 (마인크래프트식 단순 블록).</summary>
		public Texture2D TopTexture => topTexture != null ? topTexture : sideTexture;
		/// <summary>아랫면 텍스쳐. null 이면 side 사용.</summary>
		public Texture2D BottomTexture => bottomTexture != null ? bottomTexture : sideTexture;

		public Rect SideUVRect => sideUVRect;
		/// <summary>윗면 UV. Builder 가 면별로 할당 안 한 경우 width==0 → side UV fallback.
		/// **background thread 안전** — Unity Object null 검사 안 함 (`topTexture != null` 은 main thread 만 가능).</summary>
		public Rect TopUVRect => topUVRect.width > 0f ? topUVRect : sideUVRect;
		/// <summary>아랫면 UV. width==0 이면 side UV fallback. background thread 안전.</summary>
		public Rect BottomUVRect => bottomUVRect.width > 0f ? bottomUVRect : sideUVRect;

		public bool IsAir => identifier == VoxelConstants.AIR_IDENTIFIER;

		public void SetIdentifier(string value) => identifier = value;
		public void SetBlockName(string value) => blockName = value;
		public void SetColor(Color value) => color = value;
		public void SetIsSolid(bool value) => isSolid = value;
		public void SetIsOpaque(bool value) => isOpaque = value;
		public void SetAcceptsBiomeTint(bool value) => acceptsBiomeTint = value;
		public void SetSideTexture(Texture2D value) => sideTexture = value;
		public void SetTopTexture(Texture2D value) => topTexture = value;
		public void SetBottomTexture(Texture2D value) => bottomTexture = value;
		public void SetSideUVRect(Rect value) => sideUVRect = value;
		public void SetTopUVRect(Rect value) => topUVRect = value;
		public void SetBottomUVRect(Rect value) => bottomUVRect = value;

		/// <summary>BlockRegistry 전용. 외부 호출 금지.</summary>
		public void AssignRuntimeId(ushort id) => runtimeId = id;
	}
}
