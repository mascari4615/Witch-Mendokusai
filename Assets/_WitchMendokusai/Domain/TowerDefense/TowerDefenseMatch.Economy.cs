using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	// TowerDefenseMatch 의 자원과 값 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		public int Resource => core != null ? core.Resource : 0;
		public int Essence => core != null ? core.Essence : 0;

		/// <summary> index 번 포탑의 건설 비용 — 종류가 없으면 스테이지 기본값. </summary>
		/// <summary>
		/// 그 종류를 *지금* 세우는 데 드는 값 — 화면과 규칙이 같은 창구에 묻는다.
		///
		/// ★ 왜 하나로 모으나: 핫바는 스테이지 원값을 보여주고 배치는 할인값을 뗐다.
		///   건설 할인 카드를 고른 순간 **화면은 40 이라 말하고 지갑에선 34 가 빠졌다** — 화면이 거짓말한다.
		/// ★ 게다가 할인이 경로마다 다르게 걸려 있었다(포탑·채집·발전만, 함정·벽은 안 걸림).
		///   카드에는 「건설 비용 할인」이라 적혀 있는데 절반한테만 걸리면 그건 규칙이 아니라 사고다.
		/// 정수로 사는 것(전초기지·연구)은 자원 할인과 다른 통장이라 여기서 갈라 답한다.
		/// </summary>
		public int CostOf(TowerDefensePlaceableKind kind, int towerIndex = 0)
		{
			if (stage == null)
				return 0;

			switch (kind)
			{
				case TowerDefensePlaceableKind.Tower:
					return Discounted(TowerCostAt(towerIndex));
				case TowerDefensePlaceableKind.Harvester:
					return Discounted(stage.HarvesterCost);
				case TowerDefensePlaceableKind.Wall:
					return Discounted(stage.WallCost);
				case TowerDefensePlaceableKind.Trap:
					return Discounted(stage.TrapCost);
				case TowerDefensePlaceableKind.Generator:
					return Discounted(stage.GeneratorCost);
				// 정수로 산다 — 자원 할인은 안 걸린다(다른 통장).
				case TowerDefensePlaceableKind.Outpost:
					return stage.OutpostEssenceCost;
				// 영웅은 짓는 게 아니라 보내는 것 — 값이 없다.
				default:
					return 0;
			}
		}

		/// <summary> 카드 할인이 걸린 실제 값 — 화면의 값과 실제 차감이 같은 곳을 읽는다. </summary>
		public int Discounted(int cost) => Mathf.Max(1, Mathf.RoundToInt(cost * boons.CostMultiplier));

		/// <summary> 남은 목숨(유출제 아니면 0). </summary>
		public int Lives => core != null ? core.Lives : 0;

		/// <summary> 이 판이 유출제인가 — 화면이 목숨을 보여줄지 결정한다. </summary>
		public bool UsesLives => core != null && core.UsesLives;

		/// <summary>
		/// 웨이브 정산 내역을 *번 자리에* 띄운다 — 코어에 기본 수입, 채집 인형 각자 머리 위에 자기 몫.
		/// 총액만 HUD 숫자로 올리면 「채집 인형이 무슨 역할인지」가 영원히 안 읽힌다(사용자 실증).
		/// </summary>
		private void ShowIncomeBreakdown()
		{
			if (core == null || stage == null)
				return;

			if (coreCombatant != null && stage.Rules.BaseWaveIncome > 0)
				PopWorldText("+" + stage.Rules.BaseWaveIncome, coreCombatant.Position, TextType.Heal);

			if (stage.Rules.IncomePerHarvester <= 0)
				return;

			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				Transform harvester = harvesterTransforms[index];
				if (harvester == null)
				{
					harvesterTransforms.RemoveAt(index);
					continue;
				}
				// ★ 그 인형이 *실제로 번 만큼*을 띄운다.
				//   예전엔 전부 같은 숫자(정액)를 띄웠다 — 그러면 두 가지가 동시에 거짓말이 된다:
				//   ① 먼 큰 광맥에 세운 인형이 훨씬 많이 버는데 화면은 옆 인형과 같은 수를 보여준다
				//      (「자리를 잘 잡았다」를 배울 유일한 피드백인데 그게 안 보인다)
				//   ② 보급이 끊겼거나 전기가 없어 *한 푼도 못 번* 인형 위에도 숫자가 떴다.
				TowerDefenseDollLabel harvesterLabel = FindDollLabel(harvester);
				if (harvesterLabel != null && (harvesterLabel.Disconnected || harvesterLabel.Unpowered))
					continue; // 멈춘 채집은 아무것도 안 벌었다 — 아무 숫자도 띄우지 않는다.

				int earned = Mathf.RoundToInt(
					stage.Rules.IncomePerHarvester * HarvesterMultiplierOf(harvester) * core.IncomeMultiplier);
				if (earned <= 0)
					continue;

				PopWorldText("+" + earned, harvester.position.ToSim(), TextType.Heal);

				// ★ 바깥 채집은 정수를 낸다 — 그게 「멀리 나간」 보상인데 들어와도 화면이 한 마디도 안 했다.
				//   보이지 않는 보상은 배울 수가 없다(왜 위험을 무릅쓰는지가 안 남는다).
				if (harvesterIsOuter.TryGetValue(harvester, out bool outerNode) == false || outerNode == false)
					continue;

				// 규칙이 쓰는 것과 같은 식으로 — 정수는 자원과 달리 정산 배수가 아니라 채집 가중치만 탄다.
				int essence = Mathf.RoundToInt(
					stage.Rules.EssencePerHarvester * HarvesterMultiplierOf(harvester));
				if (essence > 0)
					PopWorldText("정수 +" + essence, harvester.position.ToSim(), TextType.Exp);
			}
		}
	}
}
