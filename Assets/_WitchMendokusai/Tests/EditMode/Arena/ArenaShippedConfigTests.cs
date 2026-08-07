using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 게임에 **실제로 들어 있는** 투기장 설정(`ArenaMatchConfig` 에셋)이 매치를 시작할 수 있는지 잠근다.
	///
	/// ★ 왜 필요한가: `ArenaMatch.Begin` 은 로스터가 맵과 안 맞으면 <b>LogError 만 남기고 조용히 돌아온다.</b>
	///   그런데 그 시점엔 모드·카메라·입력이 이미 투기장으로 바뀐 뒤라, 플레이어에게는
	///   <b>「관전 시점인데 아무도 없는 빈 판」</b>으로 보인다 — 화면이 고장난 줄 안다.
	///   기존 시험들은 <i>가짜</i> 로스터로 규칙만 확인한다(`ArenaDryMatchTests`). 정작 배포되는
	///   에셋이 그 규칙을 지키는지는 **아무도 안 보고 있었다** — 에셋은 사람이 인스펙터에서 만지는
	///   물건이라 코드 리뷰에도 안 걸린다.
	///
	/// 여기서 세는 조건은 `ArenaMatch.ValidateRoster` 와 같은 것들이다. 같은 판정을 두 벌 쓰는 게
	/// 아니라, <b>런타임이 거절할 에셋을 커밋 전에 먼저 잡는 것</b>이다.
	/// </summary>
	public class ArenaShippedConfigTests
	{
		private static List<ArenaMatchConfig> LoadShippedConfigs()
		{
			List<ArenaMatchConfig> configs = new();
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ArenaMatchConfig)))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				ArenaMatchConfig config = AssetDatabase.LoadAssetAtPath<ArenaMatchConfig>(path);
				if (config != null)
					configs.Add(config);
			}
			return configs;
		}

		[Test]
		public void 투기장_설정이_적어도_하나는_있다()
		{
			// 0개면 아래 시험들이 「아무것도 안 보고 통과」가 된다 — 그건 통과가 아니다.
			Assert.IsNotEmpty(LoadShippedConfigs(), "투기장 설정 에셋이 하나도 없다 — 아래 검사들이 빈손으로 통과하게 된다");
		}

		[Test]
		public void 모든_투기장_설정이_맵과_모드를_갖는다()
		{
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				Assert.IsNotNull(config.Map, $"{config.name}: 맵이 비었다 — 진입하면 빈 판이 뜬다");
				Assert.IsNotNull(config.Mode, $"{config.name}: 모드가 비었다 — 승패 규칙이 없어 판이 안 끝난다");
			}
		}

		[Test]
		public void 로스터의_모든_줄이_유닛과_프리팹을_갖는다()
		{
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				for (int entryIndex = 0; entryIndex < config.Roster.Count; entryIndex++)
				{
					ArenaMatchConfig.ArenaUnitEntry entry = config.Roster[entryIndex];

					Assert.IsNotNull(entry.UnitData, $"{config.name}: 로스터 {entryIndex} 번의 유닛이 비었다 — 그 줄은 인원에서 조용히 빠진다");
					Assert.IsNotNull(entry.UnitData.Prefab,
						$"{config.name}: 로스터 {entryIndex} 번({entry.UnitData.name})의 프리팹이 비었다 — "
						+ "3v3 인 줄 알고 3v2 로 시작하거나, 더 빠지면 「팀이 1개」로 거절된다(증상만 보이고 원인은 안 보인다)");
				}
			}
		}

		[Test]
		public void 로스터의_모든_줄이_전술을_한_줄이라도_갖는다()
		{
			// 빈 전술은 시작을 막지 않는다 — 그 유닛만 **한 판 내내 가만히 서 있는다.**
			// 로그도 예외도 안 뜨고 화면에서 「쟤는 왜 안 움직이지」로만 보인다(트랩#2 와 같은 얼굴).
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				for (int entryIndex = 0; entryIndex < config.Roster.Count; entryIndex++)
				{
					ArenaMatchConfig.ArenaUnitEntry entry = config.Roster[entryIndex];
					string who = entry.UnitData != null ? entry.UnitData.name : $"{entryIndex} 번";

					Assert.IsNotNull(entry.Tactic, $"{config.name}: {who} 의 전술이 비었다 — 그 유닛은 판 내내 가만히 서 있는다");
					Assert.IsNotEmpty(entry.Tactic.Rules, $"{config.name}: {who} 의 전술에 규칙이 한 줄도 없다 — 그 유닛은 판 내내 가만히 서 있는다");
				}
			}
		}

		[Test]
		public void 모든_전술의_마지막_줄은_무조건_참이다()
		{
			// 전술은 위에서부터 조건을 보고 **처음 맞는 줄 하나**만 실행한다. 그래서 맨 아랫줄이
			// 조건부면, 그 조건들이 다 어긋나는 순간 그 유닛은 **그 틱에 아무것도 안 한다** —
			// 사거리 밖이면 다가가지도 않고 그 자리에 선다. 화면에선 「쟤 왜 멈춰 있지」로만 보인다.
			// 그래서 맨 아래는 「무조건 참」인 줄(조건 없음 또는 Always)이어야 한다.
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
				{
					if (entry.Tactic == null || entry.Tactic.Rules.Count == 0)
						continue; // 위 시험들이 잡는다.

					string who = entry.UnitData != null ? entry.UnitData.name : "이름 없는 줄";
					TacticRule last = entry.Tactic.Rules[entry.Tactic.Rules.Count - 1];

					bool alwaysTrue = last.Conditions == null || last.Conditions.Count == 0;
					if (alwaysTrue == false)
					{
						alwaysTrue = true;
						foreach (TacticCondition condition in last.Conditions)
						{
							if (condition.Kind != ConditionKind.Always)
							{
								alwaysTrue = false;
								break;
							}
						}
					}

					Assert.IsTrue(alwaysTrue,
						$"{config.name}: {who} 의 전술 맨 아랫줄이 조건부다 — 그 조건이 어긋나는 틱마다 "
						+ "이 유닛은 아무것도 안 하고 제자리에 선다. 맨 아래에는 조건 없는 줄을 둬라");
				}
			}
		}

		[Test]
		public void 전술이_그_유닛에게_없는_스킬_칸을_가리키지_않는다()
		{
			// 스킬 칸은 유닛의 기본 스킬 목록 **순번**이다(0번부터). 목록에 없는 번호를 시전하면
			// 조용히 false 만 돌아온다 — 로그도 예외도 없다. 즉 그 유닛은 **한 판 내내 스킬을 안 쓴다.**
			// 「2번 슬롯을 쓴다」는 전술을 스킬 2개짜리 유닛에게 주는 순간 이 일이 벌어진다.
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
				{
					if (entry.UnitData == null || entry.Tactic == null)
						continue; // 위 시험들이 잡는다.

					int skillCount = entry.UnitData.DefaultSkills != null ? entry.UnitData.DefaultSkills.Length : 0;

					foreach (TacticRule rule in entry.Tactic.Rules)
					{
						if (rule.Action.Kind != ActionKind.UseSkill)
							continue;

						Assert.Less(rule.Action.SkillSlot, skillCount,
							$"{config.name}: {entry.UnitData.name} 의 전술이 {rule.Action.SkillSlot} 번 스킬 칸을 쓰는데 "
							+ $"이 유닛의 스킬은 {skillCount} 개뿐이다 — 그 줄은 조용히 실패해서 판 내내 스킬을 안 쓴다");
						Assert.GreaterOrEqual(rule.Action.SkillSlot, 0,
							$"{config.name}: {entry.UnitData.name} 의 전술 스킬 칸이 음수다");
					}
				}
			}
		}

		[Test]
		public void 체력_조건이_죽어야_참인_채로_남아있지_않다()
		{
			// ★ 실제로 났던 사고(WM-165): 편집기가 한동안 비교칸을 안 그려서 체력 조건이 전부
			//   기본값 「== 0」 으로 굳었다. 「체력이 정확히 0일 때」 = **죽어야 참**이라 그 줄은
			//   영영 발동하지 않는다. 화면에선 「저 규칙이 왜 안 먹지」로만 보인다.
			//
			// 아군 수는 일부러 뺐다 — 「아군 0명 = 혼자 남았을 때」는 멀쩡한 전술이다.
			// 틀린 경고를 한 번 내면 다음부터 아무도 안 읽는다.
			ConditionKind[] hpKinds = { ConditionKind.SelfHp, ConditionKind.SelfHpRatio, ConditionKind.TargetHpRatio };

			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
				{
					if (entry.Tactic == null)
						continue; // 위 시험이 잡는다.

					string who = entry.UnitData != null ? entry.UnitData.name : "이름 없는 줄";
					foreach (TacticRule rule in entry.Tactic.Rules)
					{
						foreach (TacticCondition condition in rule.Conditions)
						{
							bool isHpKind = System.Array.IndexOf(hpKinds, condition.Kind) >= 0;
							bool neverFires = condition.Operator == ComparisonOperator.Equal && Mathf.Approximately(condition.Value, 0f);

							Assert.IsFalse(isHpKind && neverFires,
								$"{config.name}: {who} 의 「{condition.Kind}」 조건이 「== 0」 이다 — 죽어야 참이라 그 줄은 영영 안 발동한다");
						}
					}
				}
			}
		}

		[Test]
		public void 로스터가_맵이_감당할_수_있는_인원이다()
		{
			foreach (ArenaMatchConfig config in LoadShippedConfigs())
			{
				if (config.Map == null)
					continue; // 위 시험이 이미 잡는다 — 여기서 또 죽으면 원인이 흐려진다.

				Dictionary<int, int> perTeam = new();
				foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
				{
					if (entry.UnitData == null || entry.UnitData.Prefab == null)
						continue; // 런타임도 이 줄은 인원에서 뺀다 — 같은 셈법을 쓴다.

					Assert.GreaterOrEqual(entry.TeamId, 0, $"{config.name}: 팀 번호 {entry.TeamId} 가 음수다 — 시작 불가");
					Assert.Less(entry.TeamId, config.Map.TeamCount,
						$"{config.name}: 팀 번호 {entry.TeamId} 가 맵이 가진 팀 수({config.Map.TeamCount}) 밖이다 — 시작 불가");

					perTeam[entry.TeamId] = (perTeam.TryGetValue(entry.TeamId, out int count) ? count : 0) + 1;
				}

				Assert.GreaterOrEqual(perTeam.Count, 2, $"{config.name}: 실제로 설 수 있는 팀이 {perTeam.Count} 개 — 한타는 최소 2팀이다");

				foreach (KeyValuePair<int, int> team in perTeam)
				{
					Assert.LessOrEqual(team.Value, config.Map.SpawnsPerTeam,
						$"{config.name}: 팀 {team.Key} 인원 {team.Value} 명이 맵의 팀당 자리 {config.Map.SpawnsPerTeam} 개를 넘는다 — 겹쳐 서거나 시작이 거절된다");
				}
			}
		}
	}
}
