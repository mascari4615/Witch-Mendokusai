using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Voxel 텍스쳐 빌드 설정. Inspector 에서 해상도·필터를 조정한 뒤
	/// **WM/Voxel/Build Voxel Texture Array** 를 재실행하면 새 설정으로 Texture2DArray 가 다시 빌드된다.
	/// 수치 노출 룰 정본 (하드코딩 금지) — VoxelTextureArrayBuilder 가 빌드 시 이 SO 를 read.
	/// Editor 전용 도구 설정이라 Editor asmdef 에 위치 (런타임 빌드 미포함).
	///
	/// Atlas (구) 대비: 각 블록 텍스쳐가 독립 array layer → 임의 고해상도 + mipmap + 하드웨어 Repeat wrap
	/// (worldspace seamless, frac 핵 불요). 모든 layer 는 같은 해상도여야 함 (Texture2DArray 제약) →
	/// resolution 이 통일 크기. 작은 소스는 upscale, 큰 소스는 downscale.
	/// </summary>
	public class VoxelTextureConfig : ScriptableObject
	{
		[Header("해상도 — 모든 블록 텍스쳐가 이 크기로 통일")]
		[Tooltip("Texture2DArray 한 layer 의 픽셀 크기. 모든 블록 텍스쳐가 이 크기로 리샘플됨 (Texture2DArray 제약: 전 layer 동일 크기).\n소스보다 크면 upscale(흐려짐), 작으면 downscale. 소스 해상도에 맞추는 게 best. 2의 거듭제곱.")]
		[SerializeField] private int resolution = 512;

		[Header("필터 / mipmap — 원거리 품질")]
		[Tooltip("mipmap 생성 여부. ON = 원거리 블록이 작은 mip 샘플 → 알리아싱(지직거림) 제거 + 대역폭↓. 끄면 atlas 시절처럼 원거리 노이즈.")]
		[SerializeField] private bool generateMipmaps = true;

		[Tooltip("Point = 픽셀아트 또렷. Bilinear/Trilinear = 매끈 (고해상도 사실적 텍스쳐용). Trilinear = mip 경계도 부드럽게.")]
		[SerializeField] private FilterMode filterMode = FilterMode.Trilinear;

		[Tooltip("비등방 필터 레벨 (1~16). 비스듬히 보는 면(바닥 등)의 선명도. 1 = off, 4~8 = 권장.")]
		[SerializeField, Range(1, 16)] private int anisoLevel = 4;

		public int Resolution => resolution;
		public bool GenerateMipmaps => generateMipmaps;
		public FilterMode Filter => filterMode;
		public int AnisoLevel => anisoLevel;

		private void OnValidate()
		{
			resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 8, 2048);
		}
	}
}
