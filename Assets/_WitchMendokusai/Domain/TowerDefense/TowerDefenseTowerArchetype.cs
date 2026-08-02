using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 포탑 한 종류(TASK-WM-194) — 마수 3종에 맞물리는 방어 선택지.
	///
	/// ★ 왜 데이터인가: 「어디에 지을까」만으로는 판단의 반쪽이다. 예고에 빠른 마수가 잔뜩이면 둔화를,
	///   단단한 놈이 오면 관통을 고르는 것 — 예고→배치 고리가 닫혀야 매 웨이브가 새 결정이 된다.
	///
	/// 사거리·연사·피해·관통·광역·둔화가 전부 여기 숫자 하나씩이라, 새 포탑을 만드는 데 코드가 필요 없다.
	/// </summary>
	[Serializable]
	public class TowerDefenseTowerArchetype
	{
		[Tooltip("화면에 보일 이름 — 핫바·범례가 그대로 쓴다.")]
		[SerializeField] private string displayName = "포탑 인형";

		[Tooltip("한 줄 설명 — 「무엇에 강한가」를 말로 준다. 숫자만 보면 뭘 골라야 할지 모른다.")]
		[SerializeField] private string note = "적을 쏜다";

		[Tooltip("건설 비용.")]
		[SerializeField, Min(0)] private int cost = 40;

		[Tooltip("색 = 정체. 종류끼리 확실히 멀게 잡는다(아트 이전 단계에선 색이 곧 이름).")]
		[SerializeField] private Color tint = new Color(0.45f, 0.72f, 1f, 1f);

		[Tooltip("사거리 — 화면의 원이 이 값을 그대로 그린다.")]
		[SerializeField, Min(0.5f)] private float range = 7f;

		[Tooltip("이 포탑이 밝히는 시야 반경. 보통 사거리보다 조금 넓어야 「보이는데 아직 못 쏘는」 구간이 생긴다.")]
		[SerializeField, Min(0f)] private float visionRadius = 9f;

		[Tooltip("발사 간격(초). 작을수록 속사.")]
		[SerializeField, Min(0.05f)] private float cooldown = 0.6f;

		[Tooltip("한 발 피해.")]
		[SerializeField, Min(1)] private int damage = 6;

		[Tooltip("한 발이 꿰뚫는 마수 수(1 = 관통 없음). 줄지어 오는 무리에 강해진다.")]
		[SerializeField, Min(1)] private int pierce = 1;

		[Tooltip("착탄 지점 주변에도 같은 피해를 주는 반경(0 = 광역 없음). 뭉쳐 오는 무리에 강해진다.")]
		[SerializeField, Min(0f)] private float splashRadius;

		[Tooltip("맞은 마수의 이동 속도를 이 비율로 낮춘다(0 = 둔화 없음, 0.5 = 절반). 빠른 놈을 붙잡는다.")]
		[SerializeField, Range(0f, 0.95f)] private float slowFactor;

		[Tooltip("둔화에 걸린 마수를 때릴 때 추가 피해 비율(0.5 = +50%). 포탑끼리의 *조합*이 여기서 생긴다 — 둔화가 밑밥, 나머지가 마무리.")]
		[SerializeField, Min(0f)] private float slowedTargetBonus;

		[Tooltip("승급 1단계마다 피해·사거리 증가 비율(0.35 = 단계당 +35%).")]
		[SerializeField, Min(0f)] private float upgradeGrowth = 0.35f;

		[Tooltip("승급 최대 단계(1 = 승급 없음).")]
		[SerializeField, Min(1)] private int maxLevel = 3;

		[Tooltip("둔화 지속(초).")]
		[SerializeField, Min(0f)] private float slowSeconds = 1.5f;

		public string DisplayName => displayName;
		public string Note => note;
		public int Cost => cost;
		public Color Tint => tint;
		public float Range => range;
		public float VisionRadius => visionRadius;
		public float Cooldown => cooldown;
		public int Damage => damage;
		public int Pierce => pierce;
		public float SplashRadius => splashRadius;
		public float SlowFactor => slowFactor;
		public float SlowSeconds => slowSeconds;
		public float SlowedTargetBonus => slowedTargetBonus;
		public float UpgradeGrowth => upgradeGrowth;
		public int MaxLevel => maxLevel;
	}
}
