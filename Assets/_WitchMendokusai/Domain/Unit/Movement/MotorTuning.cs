using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// KCC <see cref="Motor"/> 의 조작감 수치 (TASK-WM-199).
	///
	/// 왜 꺼냈나 — 「수치 노출」 룰의 근거가 「욘(개발자)이 자기 게임을 *놀이처럼* 손볼 수 있는 구조」인데,
	/// 그 근거가 가장 세게 걸리는 자리가 **캐릭터가 움직이는 감각**이다. 그런데 이 값들이 전부 코드 안에
	/// 잠겨 있어서 「턱을 좀 더 잘 넘게 하고 싶다」가 코드 수정 + 재컴파일 + 재부팅이었다.
	///
	/// 여기 없는 것 = 알고리즘 파라미터다. 반복 횟수(`MAX_*_ITERATIONS`), 쿼리용 캡슐 축소율
	/// (`CAPSULE_SHRINK`), 수치 안정용 하한(`MIN_*`) 은 디자인 조절값이 아니라 구현 상수라
	/// <see cref="Motor"/> 안에 const 로 남겼다. 전부 꺼내는 게 아니라 *손댈 만한 것만* 꺼낸다.
	///
	/// ★ 각도로 받는 이유: 원래 코드는 `normal.y >= 0.5` 로 경사를 판정했다. 0.5 는 cos(60°) 인데,
	///   「0.5」를 보고 60도를 떠올릴 사람은 없다. 각도로 받아서 안에서 cos 로 바꾼다 — 같은 동작,
	///   읽을 수 있는 손잡이.
	/// </summary>
	[Serializable]
	public class MotorTuning
	{
		[Header("경사")]
		[Tooltip("걸어 올라갈 수 있는 최대 경사(도). 이보다 가파르면 미끄러진다.")]
		[SerializeField, Range(1f, 89f)] private float maxWalkableSlopeDegrees = 60f;

		[Tooltip("이 각도보다 서 있는 면은 '벽'으로 친다(타고 오르지 않고 미끄러진다).")]
		[SerializeField, Range(1f, 89f)] private float wallSlopeDegrees = 60f;

		[Header("턱 · 계단")]
		[Tooltip("걸어서 그냥 올라갈 수 있는 턱 높이(m). 지형 제작의 암묵 규칙이 되는 값이다.")]
		[SerializeField, Min(0f)] private float stepOffsetHeight = 0.15f;

		[Tooltip("서 있다가 걸어 내려갈 때 발이 따라 붙는 최대 거리(m). 짧으면 계단이 툭툭 끊기고, 길면 낭떠러지에서도 들러붙는다.")]
		[SerializeField, Min(0f)] private float groundSnapDistance = 0.3f;

		[Header("접지 판정")]
		[Tooltip("공중에 있다가 접지로 인정하는 발끝 거리(m). 크면 착지가 공중에서 일어난다.")]
		[SerializeField, Min(0.001f)] private float groundTouchDistance = 0.04f;

		[Tooltip("절벽 끝에서 '아직 딛고 있다'로 볼 발 밑 거리(m). 클수록 절벽 끝에서 더 버틴다.")]
		[SerializeField, Min(0.001f)] private float stabilityProbeDistance = 0.2f;

		[Tooltip("표면과 띄워 두는 여유(m). 너무 작으면 지면에 끼고, 너무 크면 떠 보인다.")]
		[SerializeField, Min(0.001f)] private float skinWidth = 0.02f;

		// 매 사용 시 read — 캐싱하면 인스펙터에서 바꿔도 런타임에 안 먹는다(수치 노출 룰).
		public float StepOffsetHeight => stepOffsetHeight;
		public float GroundSnapDistance => groundSnapDistance;
		public float GroundTouchDistance => groundTouchDistance;
		public float StabilityProbeDistance => stabilityProbeDistance;
		public float SkinWidth => skinWidth;

		/// <summary>걸을 수 있는 바닥의 normal.y 하한. 60° → 0.5.</summary>
		public float GroundNormalYMin => Mathf.Cos(maxWalkableSlopeDegrees * Mathf.Deg2Rad);

		/// <summary>벽으로 칠 |normal.y| 상한. 60° → 0.5.</summary>
		public float WallNormalYMax => Mathf.Cos(wallSlopeDegrees * Mathf.Deg2Rad);
	}
}
