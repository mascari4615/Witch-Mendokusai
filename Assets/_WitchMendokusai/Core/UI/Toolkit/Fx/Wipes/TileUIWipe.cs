using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>칸 모양. 값은 에셋에 정수로 저장되므로 끝에만 추가</summary>
	public enum TileShape
	{
		Square = 0,
		Diamond = 1,
		Circle = 2,
		Row = 3,
	}

	/// <summary>칸이 덮이는 순서. 값은 에셋에 정수로 저장되므로 끝에만 추가</summary>
	public enum TileOrder
	{
		Diagonal = 0,
		CenterOut = 1,
		Scattered = 2,
		TopDown = 3,
	}

	/// <summary>요소 격자가 순서대로 커지며 덮음</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Tile", menuName = "WM/UI/Wipe/Tile")]
	public sealed class TileUIWipe : SingleUIWipe
	{
		[Tooltip("Square 는 칸이 한 번에 켜지고 꺼짐 (줄어드는 틈이 격자선처럼 보여서)")]
		public TileShape shape = TileShape.Diamond;
		public TileOrder order = TileOrder.Diagonal;
		[Tooltip("자랄 때 살짝 넘쳤다 돌아옴 (Square 는 무시)")]
		public bool overshoot = true;
		[Tooltip("한 칸 크기, 기준 해상도 픽셀")]
		public float cellSize = 72f;
		[Tooltip("마지막 칸이 첫 칸보다 늦게 시작하는 비율. 0 이면 동시")]
		[Range(0f, 3f)] public float spread = 1.2f;
		[Tooltip("다 자란 배율 (Diamond, Circle 만). 루트 2 (1.414) 이상이어야 칸 사이 틈이 안 보임")]
		public float growth = 1.5f;

		public override IWipeRenderer CreateRenderer() => new TileWipe(this);
	}
}
