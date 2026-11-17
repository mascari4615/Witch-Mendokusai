using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 칸마다 덮일 순서를 계산하고 진행에 따라 경계를 밂. 경계에 빛. 그리기는 MapWipe
	/// 새 무늬는 이것을 상속해 Order 하나와 그 무늬의 수치 칸만 (예: StarPatternWipe)
	/// </summary>
	public abstract class PatternUIWipe : SingleUIWipe
	{
		[Tooltip("계산 해상도 가로 칸 수. 클수록 경계가 또렷하고 무거움")]
		public int mapWidth = 256;
		[Tooltip("경계 번짐 폭, 진행 비율")]
		[Range(0.01f, 0.5f)] public float edgeSoftness = 0.07f;
		public Color edgeColor = new(1f, 0.82f, 0.5f, 1f);
		[Range(0f, 1f)] public float edgeGlow = 0.85f;

		/// <summary>(x, y) 는 화면 비율 0~1 (위가 0), aspect 는 가로/세로. 작을수록 먼저 덮이고 먼저 걷힘. 크기는 MapWipe 가 0~1 로 맞추므로 순서만 의미</summary>
		public abstract float Order(float x, float y, float aspect);

		public override IWipeRenderer CreateRenderer() => new MapWipe(this);
	}
}
