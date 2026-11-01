using UnityEngine;

namespace WitchMendokusai
{
	public abstract class BTRunner
	{
		public const float TICK = 0.1f;

		// 화면 가로 한복판. 유닛이 화면 어느 쪽 절반에 있는지를 가르는 기준.
		protected const float VIEWPORT_CENTER_X = 0.5f;

		private Node rootNode;
		// 파생 생성자의 대기 시간과 의존성 초기화 뒤 한 번만 행동트리 생성
		private Node RootNode => rootNode ??= MakeNode();
		protected UnitObject unitObject;
		protected PlayerProvider playerProvider;

		public bool CanChangeState => RootNode.State == BTState.Success || RootNode.State == BTState.Failure;

		public BTRunner(UnitObject unitObject)
		{
			this.unitObject = unitObject;
		}

		/// <summary>
		/// 유닛이 화면 오른쪽 절반에 있는가. 스프라이트 좌우 뒤집기 기준으로 6개 BT 서브클래스가
		/// 글자 하나 안 다른 사본을 각자 들고 있던 것을 여기로 모았다.
		/// </summary>
		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > VIEWPORT_CENTER_X;
		}

		protected abstract Node MakeNode();
		
		public BTState UpdateBT()
		{
			return RootNode.UpdateBT();
		}
	}
}
