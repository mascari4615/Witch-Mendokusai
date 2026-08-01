using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 마수 한 종류(TASK-WM-194) — 「빠른 놈 / 단단한 놈」을 *데이터로* 만든다.
	///
	/// ★ 새 유닛 에셋을 만들지 않는 이유: 신규 DataSO 는 AssetPrefixes 등록 + Addressable 라벨을
	///   둘 다 채워야 하고 하나만 빠져도 무음 텅빔이 된다(WM-167 실증). 종류의 *차이*는 체력·속도·
	///   덩치·색이면 충분히 전달되므로, 같은 기반 유닛에 배수를 씌우는 쪽이 근본에 가깝고 안전하다.
	///   (원거리 마수는 사거리가 다른 스킬 에셋이 필요해 이 방식으로 못 만든다 — 별도 증분.)
	///
	/// 색이 곧 정체다(아트 이전 단계) — 종류마다 색을 확실히 멀게 잡고 HUD 범례가 그대로 읽는다.
	/// </summary>
	[Serializable]
	public class TowerDefenseEnemyArchetype
	{
		[Tooltip("화면에 보일 이름 — HUD 범례·다음 웨이브 예고가 이 이름을 그대로 쓴다.")]
		[SerializeField] private string displayName = "마수";

		[Tooltip("이 종류가 처음 등장하는 웨이브(0-based). 0 = 처음부터. 뒤로 밀수록 「새 놈이 나왔다」가 된다.")]
		[SerializeField, Min(0)] private int unlockWave;

		[Tooltip("등장 비중 — 해금된 종류끼리의 상대 비율. 0 이하면 안 나온다.")]
		[SerializeField, Min(0)] private int weight = 1;

		[Tooltip("최대 체력 배수(기반 유닛 대비). 1 = 그대로.")]
		[SerializeField, Min(0.1f)] private float healthMultiplier = 1f;

		[Tooltip("이동 속도 배수(기반 유닛 대비). 1 = 그대로.")]
		[SerializeField, Min(0.1f)] private float speedMultiplier = 1f;

		[Tooltip("덩치 배수 — 눈으로 종류를 구분하는 두 번째 단서(색이 첫 번째).")]
		[SerializeField, Min(0.1f)] private float scaleMultiplier = 1f;

		[Tooltip("이 종류를 잡았을 때 보상. 단단할수록 크게 — 잡는 값어치가 위험에 비례해야 한다.")]
		[SerializeField, Min(0)] private int bounty = 6;

		[Tooltip("이 종류의 색. 종류가 색으로 안 갈리면 섞여 나와도 플레이어는 한 종류로 본다.")]
		[SerializeField] private Color tint = new Color(1f, 0.38f, 0.36f, 1f);

		public string DisplayName => displayName;
		public int UnlockWave => unlockWave;
		public int Weight => weight;
		public float HealthMultiplier => healthMultiplier;
		public float SpeedMultiplier => speedMultiplier;
		public float ScaleMultiplier => scaleMultiplier;
		public int Bounty => bounty;
		public Color Tint => tint;
	}
}
