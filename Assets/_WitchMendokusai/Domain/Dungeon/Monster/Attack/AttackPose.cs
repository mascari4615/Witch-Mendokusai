using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격 하나의 몸동작. 예고 동안 웅크리고, 때리는 순간 뻗고, 숨 고르기에 처짐. 그림을 비우면 원래 그림에 크기만
	[Serializable]
	public class AttackPose
	{
		[field: SerializeField, Tooltip("예고 동안 그림. 비우면 원래 그림")] public Sprite Windup { get; private set; }
		[field: SerializeField, Tooltip("때리는 순간 그림")] public Sprite Strike { get; private set; }
		[field: SerializeField, Tooltip("숨 고르기 그림")] public Sprite Rest { get; private set; }
		[field: SerializeField, Tooltip("예고 끝에 세로로 줄어드는 비율. 음수면 늘어남"), Range(-0.4f, 0.4f)] public float Crouch { get; private set; }
		[field: SerializeField, Tooltip("때릴 때 세로로 늘어나는 비율. 음수면 납작"), Range(-0.5f, 0.5f)] public float Stretch { get; private set; }
		[field: SerializeField, Tooltip("때린 자세를 유지하는 초"), Min(0f)] public float StrikeSeconds { get; private set; } = 0.3f;
		[field: SerializeField, Tooltip("예고 끝 떨림 폭 (m)"), Min(0f)] public float Tremble { get; private set; }
		[field: SerializeField, Tooltip("숨 고르기 동안 숨 쉬는 비율"), Range(0f, 0.2f)] public float Breath { get; private set; }

		public bool HasSprite => Windup != null || Strike != null || Rest != null;
		public bool IsEmpty => HasSprite == false && Crouch == 0f && Stretch == 0f && Tremble == 0f && Breath == 0f;
	}
}
