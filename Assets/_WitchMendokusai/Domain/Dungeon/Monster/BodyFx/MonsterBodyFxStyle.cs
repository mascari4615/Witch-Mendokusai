using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸에 늘 붙는 효과 한 종류. 번짐 (카메라를 보는 둥근 빛), 바닥 (발밑 원), 조각 (깜빡이는 작은 그림)
	// 알파나 수가 0 인 것은 안 만듦. 여러 몬스터 자산이 같이 씀 (Monster.BodyFx)
	[CreateAssetMenu(fileName = "MBF_0", menuName = "WM/Variable/" + nameof(MonsterBodyFxStyle))]
	public class MonsterBodyFxStyle : ScriptableObject
	{
		[field: SerializeField, Tooltip("번짐과 바닥 재질 (WM/GroundPuff 셰이더)")] public Material DiscMaterial { get; private set; }

		[field: Header("번짐")]
		[field: SerializeField, Tooltip("알파 0 이면 없음")] public Color GlowColor { get; private set; } = new(1f, 1f, 1f, 0f);
		[field: SerializeField, Min(0f), Tooltip("반지름 (m)")] public float GlowRadius { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("발밑에서 중심까지 높이 (m)")] public float GlowHeight { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f), Tooltip("밝기 맥박, 초당 횟수")] public float GlowPulseRate { get; private set; } = 0.6f;
		[field: SerializeField, Range(0f, 1f), Tooltip("맥박 깊이")] public float GlowPulseDepth { get; private set; } = 0.3f;

		[field: Header("바닥")]
		[field: SerializeField, Tooltip("알파 0 이면 없음")] public Color GroundColor { get; private set; } = new(1f, 1f, 1f, 0f);
		[field: SerializeField, Min(0f), Tooltip("반지름 (m)")] public float GroundRadius { get; private set; } = 1.5f;
		[field: SerializeField, Min(0f), Tooltip("바닥에서 띄우는 높이 (m)")] public float GroundLift { get; private set; } = 0.03f;

		[field: Header("번짐과 바닥 공통")]
		[field: SerializeField, Range(0.01f, 1f), Tooltip("가장자리 부드러움. 1 이면 중심부터 흐려짐")] public float Softness { get; private set; } = 1f;

		[field: Header("조각")]
		[field: SerializeField] public Sprite PieceSprite { get; private set; }
		[field: SerializeField, Tooltip("카메라를 보는 재질")] public Material PieceMaterial { get; private set; }
		[field: SerializeField] public Color PieceColor { get; private set; } = Color.white;
		[field: SerializeField, Min(0), Tooltip("동시에 도는 수. 0 이면 없음")] public int PieceCount { get; private set; }
		[field: SerializeField, Min(0f), Tooltip("한 변 크기 (m)")] public float PieceSize { get; private set; } = 0.2f;
		[field: SerializeField, Min(0f), Tooltip("조각을 카메라 쪽으로 당기는 거리 (m). 몸 앞에 그려지게")] public float PieceDepth { get; private set; } = 0.2f;
		[field: SerializeField, Tooltip("나타나는 자리 폭과 높이 (m). 발밑 가운데 기준")] public Vector2 PieceArea { get; private set; } = new(0.8f, 1f);
		[field: SerializeField, Min(0.01f), Tooltip("조각 하나가 초당 나타나는 횟수")] public float PieceRate { get; private set; } = 1f;
		[field: SerializeField, Range(0.05f, 1f), Tooltip("한 주기 가운데 보이는 비율. 작으면 번쩍")] public float PieceVisible { get; private set; } = 0.5f;

		public bool HasGlow => GlowColor.a > 0f && GlowRadius > 0f;
		public bool HasGround => GroundColor.a > 0f && GroundRadius > 0f;
		public bool HasPieces => PieceCount > 0 && PieceSprite != null;

		public void Validate(List<string> errors)
		{
			if ((HasGlow || HasGround) && DiscMaterial == null)
				errors.Add($"{name}: 번짐이나 바닥이 있는데 {nameof(DiscMaterial)} 비어 있음");
			if (PieceCount > 0 && (PieceSprite == null || PieceMaterial == null))
				errors.Add($"{name}: 조각이 있는데 그림이나 재질이 비어 있음");
		}

		// 조각 하나의 주기 번호와 진행도. 조각마다 엇갈림
		public (int Cycle, float Progress) PieceCycle(int index, float time, float seed)
		{
			float phase = time * PieceRate + seed + (float)index / Mathf.Max(1, PieceCount);
			int cycle = Mathf.FloorToInt(phase);
			return (cycle, phase - cycle);
		}

		// 진행도의 진하기. 보이는 구간에서 올랐다 내려감, 밖이면 0
		public float PieceAlpha(float progress) => progress >= PieceVisible ? 0f : Mathf.Sin(progress / PieceVisible * Mathf.PI);
	}
}
