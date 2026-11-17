using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 평상시 요소 효과는 종류마다 에셋 하나. 쓰는 자리 (UIFxSettings 기본 칸, 패널, 창) 에 에셋을 끼워 바꿈
	/// - 요소 하나에 그리는 효과는 SingleIdleEffect, 여럿을 엮는 것은 WaveIdleEffect, MixIdleEffect, 끄기는 OffIdleEffect
	/// - 새 종류: SingleIdleEffect 상속 클래스 하나 (파일 이름과 클래스 이름이 같게) 에 PlayOn 하나
	/// - 같은 종류의 변형: 에셋 복제 뒤 수치만 바꿈
	/// - 그리기 도구는 UIIdleFx.Cover (대상 자리의 층), UIIdleFx.Animate (진행 0~1)
	/// - 이름 규격 IdleFx_{ID}_{이름}.asset
	/// </summary>
	public abstract class UIIdleEffect : ScriptableObject
	{
		/// <summary>이번에 쓸 실제 에셋. 묶음 에셋 (MixIdleEffect) 은 후보 중 하나, null 이면 이번엔 쉼</summary>
		public virtual UIIdleEffect Pick() => this;

		/// <summary>overlay 위에 target (또는 targets 중 일부) 효과를 그림. onDone 을 부를 횟수를 돌려줌</summary>
		public abstract int Play(VisualElement overlay, VisualElement target, List<VisualElement> targets, Action onDone);
	}
}
