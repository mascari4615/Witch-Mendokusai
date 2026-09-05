using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 적응 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static double adaptationProbeAt;
		private static bool adaptationArmed;
		private static bool adaptationSawEnemies;
		private static bool adaptationTargetsNest;

		/// <summary> 둔화 포탑을 실제로 세운 자리 — 마수가 사거리에 왔는지는 여기 기준으로 잰다. </summary>
		private static Vector3 adaptationAim;

		/// <summary> 탐침을 박은 그 판 — 판이 바뀌면 비교 자체가 무의미하다. </summary>
		private static TowerDefenseMatch adaptationMatch;

		/// <summary> 코어 둘레 5칸에 세운 둔화 포탑이 닿는 거리 — 이 안에 들어와야 「맞힐 기회가 있었다」. </summary>
		private const float ADAPTATION_REACH = 14f;

		/// <summary>
		/// 적응 규칙이 화면까지 오는가 — 이걸 안 재면 영영 못 본다.
		///
		/// ★ 왜 일부러 세우나: 적응은 *편중*으로만 생기는데, 하네스가 늘 짓던 기본 포탑은
		///   관통 1이라 아무 셈도 안 올린다(관통은 2번째 대상부터 센다). 그래서 여태 적응이
		///   0 이었고, 「규칙이 죽었나」와 「안 건드렸나」가 똑같이 빈 값으로 보였다.
		///   둔화 포탑만 몰아 세워 편중을 만든 뒤, 규칙과 *화면 글자*를 둘 다 읽는다.
		/// </summary>
		private static void ArmAdaptationProbe()
		{
			if (adaptationArmed || match == null || match.CoreCombatant == null)
				return;
			if (match.SurvivedSeconds < 6f || match.Adaptation.HasAny)
				return;

			int slowSlot = -1;
			for (int index = 0; index < match.TowerArchetypeCount; index++)
			{
				TowerDefenseTowerArchetype candidate = match.TowerArchetypeAt(index);
				if (candidate != null && candidate.SlowFactor > 0f)
				{
					slowSlot = index;
					break;
				}
			}

			if (slowSlot < 0)
			{
				adaptationArmed = true;
				Debug.Log(TAG + " 적응 — 못 쟀다: 이 판에 둔화 포탑이 아예 없다(편중을 만들 수단 X).");
				return;
			}

			adaptationArmed = true;

			// ★ 코어 둘레에만 세우면 *마수가 와 주기를 기다리는* 검사가 된다 — 두 번 연속 「60초 동안
			//   아무도 사거리에 안 왔다」로 끝났다. 둥지는 제자리에 있고 포탑이 쏘는 대상이므로,
			//   둥지 옆에 세우면 맞힐 것이 반드시 있다. 운에 기대지 않는 유일한 자리다.
			Vector3 core = match.CoreCombatant.Position;
			Vector3 target = core;
			float nearest = float.MaxValue;
			foreach (TowerDefenseMatch.LairMarker marker in match.LairMarkers)
			{
				float distance = Vector3.Distance(marker.Position, core);
				if (distance >= nearest)
					continue;
				nearest = distance;
				target = marker.Position;
			}

			int placed = 0;
			int outposts = 0;
			string rejected = string.Empty;
			if (nearest < float.MaxValue)
			{
				// ★ 둥지는 늘 보급 밖(실측 40~50)이라 그 옆엔 바로 못 짓는다. 게임이 준 답이
				//   전초기지(보급 원점)이므로, 코어에서 둥지 쪽으로 징검다리를 놓아 보급을 끌고 간다.
				//   검사가 잔고 때문에 실패하면 *기능이 아니라 지갑*을 재는 꼴이라 먼저 채워 준다.
				match.GrantForVerification(4000, 400);

				Vector3 toNest = (target - core).normalized;
				for (float distance = OUTPOST_STEP; distance < nearest - 6f; distance += OUTPOST_STEP)
				{
					if (match.TryPlaceOutpost(core + toNest * distance))
						outposts++;
					else
						break; // 더 못 뻗으면 거기까지가 내 보급이다.
				}

				// 둥지에서 코어 쪽으로 조금 물러난 자리 — 사거리 안이면서 보급이 닿을 가능성이 높다.
				Vector3 toCore = -toNest;
				for (int step = 0; step < 4; step++)
				{
					Vector3 spot = target + toCore * (4f + step * 2f);
					if (match.TryPlaceTower(spot, slowSlot))
						placed++;
				}
				if (placed == 0)
					rejected = " · 전초기지 " + outposts + "기를 놓고도 둥지 옆엔 한 기도 못 세웠다";
			}

			// 둥지 옆이 막혔으면 코어 둘레라도 세운다 — 아무것도 안 세우는 것보단 잴 확률이 있다.
			if (placed == 0)
			{
				for (int ring = 0; ring < 4; ring++)
				{
					float angle = ring * 90f * Mathf.Deg2Rad;
					Vector3 spot = core + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5f;
					if (match.TryPlaceTower(spot, slowSlot))
						placed++;
				}
			}

			adaptationMatch = match;
			adaptationTargetsNest = placed > 0 && rejected.Length == 0 && nearest < float.MaxValue;
			// 재는 자리 = 포탑이 실제로 선 자리. 둥지 옆에 섰으면 둥지 옆, 물러났으면 코어 둘레.
			adaptationAim = adaptationTargetsNest ? target : core;
			Debug.Log(TAG + " 적응 — 가장 가까운 둥지까지 " + (nearest < float.MaxValue ? nearest.ToString("F1") : "없음")
				+ rejected + " · 전초기지 " + outposts + "기 · 겨눈 곳 " + (adaptationTargetsNest ? "둥지 옆" : "코어 둘레"));

			adaptationProbeAt = EditorApplication.timeSinceStartup + 60.0;
			Debug.Log(TAG + " 적응 — 둔화 포탑 " + placed + "기 세움(칸 " + slowSlot + ") · 60초 동안 화면을 지켜본다.");
		}

		/// <summary>
		/// 둔화만 썼으면 마수가 「둔화에 익숙해졌다」고 *화면에* 말해야 한다.
		///
		/// ★ 한 번만 보고 판정하면 안 된다 — 이 말은 알림으로 뜨고 알림은 몇 초 뒤 사라진다.
		///   정해진 시각에 딱 한 번 읽으면 *떴다 진 것*을 「안 떴다」로 잡는다(실제로 한 번 잡았다).
		///   그래서 창을 두고 매 틱 훑다가, 처음 보이는 순간 통과시킨다.
		/// ★ 찾는 말은 「익숙」이다. 규칙이 쓰는 말(「둔화에 익숙함」)과 화면이 쓰는 말
		///   (「마수가 둔화에 익숙해졌다」)이 서로 다르다 — 한쪽 말로 다른 쪽을 찾으면 영영 못 만난다.
		/// </summary>
		private static void PollAdaptation(double now)
		{
			if (adaptationProbeAt <= 0.0)
				return;

			// ★ 창이 열린 채 판이 새로 시작되면(재시작·이어하기) 예전엔 아무 말 없이 사라졌다 —
			//   「재봤는데 조용하다」와 「아예 못 쟀다」가 구별이 안 됐다. 판이 바뀌면 그렇다고 말한다.
			if (match == null || match != adaptationMatch)
			{
				adaptationProbeAt = 0.0;
				Debug.Log(TAG + " 적응 — 못 쟀다: 창이 도는 중에 판이 새로 시작됐다. 실패가 아니다.");
				return;
			}

			// ★ 둔화는 *맞혀야* 세어진다. 그런데 「마수가 살아 있다」로는 부족하다 — 테두리에서 막
			//   나온 놈은 코어에서 수십 칸 밖이라 내가 세운 포탑의 사거리 안에 영영 안 들어온다.
			//   실제로 「마수가 있었는데 편중 0」으로 한 번 틀리게 FAIL 을 찍었다. 재는 것은
			//   **포탑이 닿는 거리까지 왔는가**여야 한다.
			// ★ 「둥지를 겨눴으니 맞힐 것이 있었다」는 가정을 뺀다. 두 번 다 그 가정 때문에 거짓 FAIL 이 났다
			//   (실측 두 판 모두: 겨눈 자리에 둔화 포탑 2기가 살아 있고 **그 둘레 마수 0마리**인데 실패로 보고).
			//   자는 둥지는 식구가 안 나오고, 「어딘가 깨어난 둥지가 있다」는 *내가 겨눈 그 둥지*가 아니다.
			//   가정을 빼고 아래에서 *실제로 사거리 안에 있었는지*만 본다 — 없으면 「못 쟀다」다.
			//   재는 쪽이 실패와 못 쟀다를 헷갈리면, 진짜 실패가 났을 때 아무도 안 믿는다.

			if (adaptationSawEnemies == false)
			{
				// ★ 포탑을 둥지 옆(코어에서 35 밖)에 세워 놓고 *코어 주변*의 마수를 세고 있었다.
				//   그래서 「마수는 있었는데 편중 0」이라는 거짓 FAIL 이 났다 — 재는 자리는
				//   포탑이 선 자리여야 한다. 겨눈 곳을 기억해 두고 그 둘레를 본다.
				Vector3 core = adaptationAim;
				foreach (MatchCombatant enemy in match.WaveEnemies)
				{
					if (enemy == null || enemy.IsAlive == false)
						continue;
					if ((enemy.Position - core).sqrMagnitude <= ADAPTATION_REACH * ADAPTATION_REACH)
					{
						adaptationSawEnemies = true;
						break;
					}
				}
			}

			if (ScreenSaysAdapted())
			{
				adaptationProbeAt = 0.0;
				Debug.Log(TAG + " 적응 결과 — 화면이 말했다 · 둔화저항 "
					+ match.Adaptation.SlowResist.ToString("F2") + " · 규칙 「" + match.AdaptationNote + "」");
				return;
			}

			if (now < adaptationProbeAt)
				return;

			adaptationProbeAt = 0.0;
			ReportAdaptationMiss();
		}

		/// <summary> HUD 라벨 어딘가가 적응을 말하고 있는가 — 알림이든 어디든. </summary>
		private static bool ScreenSaysAdapted()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (hud == null)
				return false;

			foreach (Label label in hud.Query<Label>().ToList())
			{
				if (label != null && string.IsNullOrEmpty(label.text) == false && label.text.Contains("익숙"))
				{
					Debug.Log(TAG + " 적응 — 화면 글자: 「" + label.text + "」");
					return true;
				}
			}
			return false;
		}

		private static void ReportAdaptationMiss()
		{
			if (match == null)
			{
				Debug.Log(TAG + " 적응 — 못 쟀다: 판이 사라졌다.");
				return;
			}

			TowerDefenseAdaptationState state = match.Adaptation;
			string note = match.AdaptationNote;

			UIRoot adaptationUiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = adaptationUiRoot != null && adaptationUiRoot.ModeHudLayer != null
				? adaptationUiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (hud == null)
			{
				Debug.Log(TAG + " 적응 — 못 쟀다: HUD 를 못 찾았다 (규칙값 둔화저항 "
					+ state.SlowResist.ToString("F2") + " · 말 「" + note + "」).");
				return;
			}

			// ★ 「편중이 0」은 결론이 아니라 증상이다. 세운 포탑이 아직 서 있는지, 그 둘레에 맞힐
			//   것이 있었는지를 같이 찍어야 다음 사람이 어디를 볼지 안다(추측하지 말라는 그 규칙).
			int slowTowersAlive = 0;
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>())
			{
				if (weapon == null || weapon.SlowFactorForVerification <= 0f)
					continue;
				if ((weapon.transform.position.ToSim() - adaptationAim).sqrMagnitude <= ADAPTATION_REACH * ADAPTATION_REACH)
					slowTowersAlive++;
			}

			int nearAim = 0;
			foreach (MatchCombatant enemy in match.WaveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if ((enemy.Position - adaptationAim).sqrMagnitude <= ADAPTATION_REACH * ADAPTATION_REACH)
					nearAim++;
			}

			Debug.Log(TAG + " 적응 결과 — 둔화저항 " + state.SlowResist.ToString("F2")
				+ " · 규칙 「" + note + "」 · 창이 닫힐 때까지 화면에 한 번도 안 떴다"
				+ " · 겨눈 자리에 살아있는 둔화 포탑 " + slowTowersAlive + "기 · 그 둘레 마수 " + nearAim + "기"
				+ " · 쏜 것을 알린 횟수 " + match.ShotsReported + " · 가장 시끄러운 곳 " + match.LoudestNoise.ToString("F1"));

			// ★ 사격 소음이 도는지는 *포탑이 확실히 쏘는 자리*에서만 갈린다. 전투 없는 판에서 재면
			//   「배선이 죽었다」와 「쏠 상황이 없었다」가 똑같이 0 으로 보인다(두 판을 그렇게 날렸다).
			//   둔화 포탑을 둥지 옆에 세운 이 검사가 바로 그 자리다 — 여기서 0 이면 배선이 죽은 것이다.
			// ★ 「서 있다」 ≠ 「쏜다」. 사거리 안에 아무것도 없으면 포탑은 조용한 게 맞다 —
			//   그걸 배선 고장으로 부르면 멀쩡한 것을 고치러 간다(이 판에서 여러 번 밟은 함정).
			// ★ 여기서 실패를 단정하지 않는다. 「포탑이 서 있다 · 근처에 마수가 있다」로는 *그 포탑의
			//   사거리 안에 있었는지*를 못 가르고, 판마다 결과가 뒤집혔다(0 과 11 을 오갔다).
			//   배선이 살아 있다는 것은 이미 실측으로 확인했다(알린 횟수 11) — 뒤집히는 단정을
			//   남겨두면 다음 사람이 멀쩡한 것을 고치러 간다. 값만 남기고 판정은 안 한다.
			Debug.Log(TAG + " 소리 — 쏜 것을 알린 횟수 " + match.ShotsReported
				+ " (포탑 " + slowTowersAlive + "기 · 근처 마수 " + nearAim + "기)");

			if (note.Length == 0 && slowTowersAlive == 0)
			{
				Debug.Log(TAG + " 적응 — 못 쟀다: 세운 둔화 포탑이 창이 닫힐 때까지 하나도 안 남았다(부서졌다). 실패가 아니다.");
				return;
			}

			// ★ 적응은 총량이 아니라 *편중*으로 붙는다 — 한 수단이 전체의 1/3 을 넘어야 저항이 생긴다.
			//   그러니 「둔화를 썼는데 저항 0」은 결함일 수도, 규칙대로일 수도 있다. 쓴 횟수를 물어 가른다.
			(int slowUses, int splashHits, int pierceHits) = match.AdaptationUseCounts;
			int totalUses = slowUses + splashHits + pierceHits;

			if (note.Length == 0 && adaptationSawEnemies == false)
				Debug.Log(TAG + " 적응 — 못 쟀다: 창이 도는 60초 동안 마수가 포탑 사거리 안에 한 기도 안 들어왔다(맞힐 것이 없으면 편중도 없다). 실패가 아니다.");
			else if (note.Length == 0 && slowUses == 0)
				Debug.LogWarning(TAG + " 적응 FAIL — 사거리 안에 마수가 있었는데 둔화가 한 번도 안 걸렸다"
					+ " (쓴 횟수 둔화 0 · 광역 " + splashHits + " · 관통 " + pierceHits + ").");
			else if (note.Length == 0)
				Debug.Log(TAG + " 적응 — 못 쟀다: 둔화는 걸렸지만 편중이 안 생겼다"
					+ " (둔화 " + slowUses + " · 광역 " + splashHits + " · 관통 " + pierceHits
					+ " → 둔화 몫 " + (totalUses > 0 ? (slowUses * 100f / totalUses).ToString("F0") : "0")
					+ "%, 저항이 붙으려면 33% 를 넘어야 한다). 규칙대로다 — 실패가 아니다.");
			else
				Debug.LogWarning(TAG + " 적응 FAIL — 규칙은 「" + note + "」인데 화면 어디에도 안 떴다(안 보이는 규칙 = 없는 규칙).");
		}
	}
}
