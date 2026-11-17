using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>직접 그리는 전환. 막 색과 덮기, 걷기 시간은 공통</summary>
	public abstract class SingleUIWipe : UIWipe
	{
		[Tooltip("막 색")]
		public Color color = new(0.10f, 0.08f, 0.16f, 1f);
		public float coverSeconds = 0.55f;
		public float revealSeconds = 0.6f;

		public override SingleUIWipe Pick() => this;

		/// <summary>이 에셋의 무늬를 그리는 새 렌더러</summary>
		public abstract IWipeRenderer CreateRenderer();
	}
}
