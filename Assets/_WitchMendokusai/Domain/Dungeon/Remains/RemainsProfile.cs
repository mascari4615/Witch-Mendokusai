using UnityEngine;

namespace WitchMendokusai
{
	// 조각 모양. 코드로 만든 작은 메시 (RemainsMeshes)
	public enum RemainsPieceShape
	{
		Chip,
		Blob,
		Shard,
		Tuft,
	}

	// 몬스터 한 종이 쓰러진 뒤 남기는 것. 슬라임은 점액 덩이와 젖은 얼룩, 멧돼지는 털 뭉치와 뼈 조각, 위스프는 빛 가루 얼룩만
	[CreateAssetMenu(fileName = "Remains_", menuName = "WM/Remains/Profile")]
	public class RemainsProfile : ScriptableObject
	{
		[Header("조각")]
		public RemainsPieceShape Shape = RemainsPieceShape.Chip;
		[Tooltip("조각 수 범위, 양 끝 포함")] public Vector2Int Count = new(3, 5);
		[Tooltip("조각 크기 범위, 미터")] public Vector2 Size = new(.12f, .22f);
		[Tooltip("조각 색. 여럿이면 조각마다 하나. 비우면 몸 색을 뽑아 씀")] public Color[] Palette = System.Array.Empty<Color>();
		[Tooltip("URP Lit 재질. 비우면 공용 (RemainsSettings.PieceMaterial)")] public Material Material;
		[Tooltip("옆으로 튀는 속도 범위")] public Vector2 Speed = new(1.5f, 3.5f);
		[Min(0f), Tooltip("위로 튀는 속도")] public float Lift = 3.5f;
		[Min(0f)] public float Gravity = 18f;
		[Range(0f, 1f), Tooltip("바닥에서 되튀는 비율")] public float Bounce = .25f;
		[Min(0f), Tooltip("회전 속도, 초당 도")] public float Spin = 420f;
		[Range(0f, 1f), Tooltip("맞은 반대쪽으로 쏠리는 정도")] public float AwayBias = .5f;

		[Header("얼룩")]
		[Min(0f), Tooltip("쓰러진 자리 얼룩 반지름, 미터. 0 이면 없음")] public float StainRadius = .6f;
		[Tooltip("얼룩 색. 알파가 진하기")] public Color StainColor = new(.2f, .3f, .15f, .5f);
		[Min(0f), Tooltip("조각이 누운 자리 작은 얼룩 반지름 (조각 크기 배수). 0 이면 없음")] public float PieceStainScale;

		[Header("웅덩이")]
		[Tooltip("쓰러진 자리에 쏟는 액체. 비우면 없음")] public GoopKind Spill;
		[Min(0f), Tooltip("웅덩이 반지름, 미터")] public float SpillRadius = .8f;
		[Range(0f, 1f), Tooltip("웅덩이를 남길 확률")] public float SpillChance = .35f;

		[Header("많이 쓰러질 때")]
		[Range(0f, 1f), Tooltip("조각을 남길 확률. 처치가 몰리면 RemainsSettings 가 더 낮춤")] public float PieceChance = 1f;
	}
}
