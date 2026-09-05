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
	// TowerDefensePlayVerify 의 화면 표시 세기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		/// <summary>
		/// 예고 표식과 경고 표식이 **화면에 실제로 떠 있는가**.
		///
		/// ★ 여태 이 둘은 「코드가 있다」까지만 확인됐다. 화면 층에 글자가 안 붙으면 규칙이 아무리
		///   맞아도 사람에겐 없는 기능이다(도달 불가). 뜬 개수를 직접 센다.
		/// ★ 경고는 사건이 나야 뜨므로 검증용으로 하나 띄우고, 그것이 화면까지 가는지만 본다.
		/// </summary>
		private static void VerifyOnScreenMarks()
		{
			if (match == null)
				return;

			match.RaiseAlertForVerification("검증 알림");

			// ★ 곁눈질용 미니맵은 마우스 설명을 안 단다(그 아래 땅을 눌러야 하므로). 설명이 붙는 것은
			//   펼친 지도뿐이라, 「서식지가 서식지로 읽히는가」는 지도를 열어야만 잴 수 있다.
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController mapOwner))
				mapOwner.OpenMapForVerification();

			// ★ 서식지 표시는 *밝힌 곳만* 뜬다 — 안 밝히면 이 검사는 영영 「못 쟀다」로 끝난다.
			//   한 곳만 밝혀서 「밝히면 서식지로 뜨는가」를 실제로 재게 만든다.
			foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
			{
				match.RevealForVerification(lair.Position, 6f);
				break;
			}
			// ★ 한 틱 미루는 것으로는 부족하다 — 에디터가 앞에 없으면 Play 루프가 느려져 *게임 프레임이
			//   한 장도 안 지난 채* 재게 된다(오늘 세 번째로 같은 실수를 했다). 실제 시간이 흐른 뒤에 센다.
			markCheckAt = EditorApplication.timeSinceStartup + 1.5;

			// ★ 강도는 *시간이* 올린다. 배속 버튼을 세 번 눌러 75초를 기다려도 1.02 까지밖에 안 올라
			//   알림 조건(한 칸 = 0.5)에 영영 안 닿았다 — 그래서 이 알림은 여태 한 번도 화면에 안 떴다.
			//   기다리는 검사는 안 닫힌다(적응에서 다섯 사이클을 그렇게 흘렸다). 시계를 직접 감는다.
			// ★ 감을 초는 **규칙에서 역산**한다 — 판 자산의 곡선이 바뀌면 이 검사도 같이 따라가야
			//   한다. 여기에 초를 박아 두면 곡선을 완만하게 바꾸는 순간 조용히 안 닿는 검사가 된다.
			pressureBefore = match.Pressure;
			float perMinute = match.PressurePerMinute;
			if (perMinute > 0f)
			{
				// 한 칸을 확실히 넘도록 1.5칸어치를 감는다.
				float wanted = match.PressureAnnounceStep * 1.5f;
				match.AdvanceClockForVerification(wanted / perMinute * 60f);
			}
			else
			{
				Debug.Log(TAG + " 강도 알림 — 못 쟀다: 이 판은 시간이 지나도 강도가 안 오른다(곡선 0). 실패가 아니다.");
			}

			for (int step = 0; step < 3; step++)
				match.CycleSpeed();
			pressureCheckAt = EditorApplication.timeSinceStartup + 3.0;
		}

		private static void CountOnScreenMarks()
		{
			{
				UIDocument document = Object.FindAnyObjectByType<UIDocument>();
				if (document == null || document.rootVisualElement == null)
				{
					Debug.LogError(TAG + " 표식 FAIL — 화면 문서가 없다.");
					return;
				}

				string invasionSentence = string.Empty;
				int invasionMarks = 0;
				int alertMarks = 0;
				int alertMarksHidden = 0;
				int alertSlots = 0;
				foreach (VisualElement element in document.rootVisualElement.Query<Label>().Build())
				{
					Label label = (Label)element;
					string text = label.text;
					bool hidden = element.resolvedStyle.display == DisplayStyle.None;

					// 알림 칸은 *만들어졌는지*와 *글자가 들어갔는지*와 *보이는지*가 각각 다른 문제다.
					if (label.name == "AlertMark" || (string.IsNullOrEmpty(text) == false && text.Contains("❗")))
						alertSlots++;

					if (string.IsNullOrEmpty(text))
						continue;
					if (text.Contains("❗"))
					{
						if (hidden)
							alertMarksHidden++;
						else
							alertMarks++;
					}
					if (hidden)
						continue;
					if (text.Contains("▼") || text.Contains("에서 온다"))
						invasionMarks++;
					if (text.Contains("에서 온다"))
						invasionSentence = text;
				}

				// ★ 「규칙에 있나 / 화면에 갔나」를 갈라 찍는다 — 0 하나만 보면 어디서 끊겼는지 모른다.
				int ruleAlerts = match != null ? match.Alerts.Count : -1;
				// 미니맵이 서식지를 「마수」가 아니라 *서식지*로 말하는지 — 말이 틀리면 판단이 틀린다.
				int lairDots = 0;
				int lyingEnemyDots = 0;
				int mapDots = 0;
				int mapDotsWithTip = 0;
				foreach (VisualElement element in document.rootVisualElement.Query<VisualElement>().Build())
				{
					if (element.name == "MapDot" && element.resolvedStyle.display != DisplayStyle.None)
						mapDots++;

					string tip = element.tooltip;
					if (string.IsNullOrEmpty(tip))
						continue;
					if (element.name == "MapDot")
						mapDotsWithTip++;
					if (tip.StartsWith("서식지"))
						lairDots++;
					else if (tip.StartsWith("마수"))
						lyingEnemyDots++;
				}
				// ★ 「지도 점이 있나 / 설명이 붙었나 / 무엇이라 부르나」를 갈라 찍는다.
				//   숫자 하나만 보면 「지도가 없다」와 「이름이 틀렸다」가 똑같이 0 으로 보인다.
				// 예고가 「무엇이 오는가」까지 말하는가 — 방향만으로는 어떤 대비를 할지 못 정한다.
				string expectedPhrase = match != null ? match.NextWaveEventPhrase() : string.Empty;
				Debug.Log($"{TAG} 예고 문장 — 「{invasionSentence}」 (성격 「{expectedPhrase}」)");
				if (string.IsNullOrEmpty(expectedPhrase) == false
					&& string.IsNullOrEmpty(invasionSentence) == false
					&& invasionSentence.Contains(expectedPhrase.Trim()) == false)
				{
					Debug.LogError(TAG + " 예고 FAIL — 다음 파도에 성격이 있는데 예고가 방향만 말한다.");
				}

				// ★ 서식지가 *만나지는 층인가* — 만들어놓고 도달 불가면 그 층은 통째로 죽은 것이다.
				//   보급이 닿는 거리와 가장 가까운 서식지 거리를 나란히 놓고 본다.
				if (match != null)
				{
					float nearest = float.MaxValue;
					int lairTotal = 0;
					foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
					{
						lairTotal++;
						float distance = Vector3.Distance(lair.Position, match.CoreCombatant != null
							? match.CoreCombatant.Position
							: Vector3.zero);
						if (distance < nearest)
							nearest = distance;
					}

					float reach = match.EffectiveSupplyReach;
					Debug.Log($"{TAG} 서식지 도달 — {lairTotal}곳 · 가장 가까운 것 {(lairTotal > 0 ? nearest : -1f):F1}"
						+ $" · 보급 거리 {reach:F1} · 깨우는 거리 안에 들려면 {(lairTotal > 0 ? nearest - reach : 0f):F1} 더 나가야 한다");

					if (lairTotal > 0 && nearest > reach * 4f)
					{
						Debug.LogWarning($"{TAG} 서식지 도달 — 가장 가까운 서식지가 보급 거리의 {nearest / Mathf.Max(1f, reach):F1}배다."
							+ " 한 판에 한 번도 안 만나면 이 층은 없는 것과 같다.");
					}
				}

				// ★ 풀이 남의 상태를 기억하면 재사용된 마수가 꺼진 채로 태어나 영영 안 움직인다.
				//   한 마리만 굳어도 파도가 안 끝난다 — 0 이 아니면 그 자리에서 실패다.
				int frozen = match != null ? match.FrozenEnemyCount : 0;
				Debug.Log($"{TAG} 굳은 마수 — 전술 꺼진 채 살아있는 마수 {frozen}기");
				if (frozen > 0)
					Debug.LogError($"{TAG} 굳음 FAIL — {frozen}기가 전술이 꺼진 채로 살아 있다(풀에 상태가 새어 나갔다).");

				// ★ 길찾기 상한이 판에 비해 모자라면 마수가 「갈 길이 있는데」 못 간다 — 증상은
				//   「몇 마리가 그냥 안 움직인다」로만 보여서 원인을 길찾기로 짚기가 어렵다.
				if (match != null)
				{
					Debug.Log($"{TAG} 길찾기 — 판 {match.MapCellCount}칸 · 한 번에 최대 {match.PathPeakCells}칸 펼침"
						+ $" · 상한에 걸려 포기 {match.PathCapHits}회");
					// ★ 파도와 상시 압박은 *다른 출구*를 써야 한다 — 테두리 침공을 넣으면서 둘 다 테두리로
					//   보내면 「둥지를 부수면 그 출구가 닫힌다」가 거짓말이 된다.
					Debug.Log($"{TAG} 출구 — 둥지 {match.NestCount}곳 · 이번 파도 토막 {match.InvasionFrontCount}점"
						+ $" · 테두리 침공 {match.IsBorderInvasion}");
					if (match.IsBorderInvasion && match.InvasionFrontCount == 0)
						Debug.LogError(TAG + " 출구 FAIL — 테두리 침공인데 파도 토막이 0점이다(파도가 어디서 오는지 없다).");

					// 「길 없음」은 그 자체로 실패가 아니다 — 벽을 부수러 붙는 중일 수 있다.
					// 다만 *얼마나 자주* 나는지는 알아야, 나중에 「판이 안 끝난다」가 나올 때 여기부터 본다.
					Debug.Log($"{TAG} 안내 — 길 없음 {match.NavigatorNoPathCount}회 (부수러 가는 중이면 정상)");

					if (match.PathCapHits > 0)
						Debug.LogError($"{TAG} 길찾기 FAIL — 상한에 걸려 {match.PathCapHits}회 포기했다(마수가 갈 길이 있는데 못 간다).");
				}

				// 강도는 시간이 올리는 규칙이다 — 「내 포탑이 약해졌다」로 오해하지 않으려면 보여야 한다.
				Debug.Log($"{TAG} 강도 — 마수 강도 {(match != null ? match.Pressure : 0f):F2}");

				// 적응은 판을 바꾸는 규칙인데 그리던 칸이 숨겨져 화면에서 사라졌었다 —
				// 「규칙이 말하는 것」과 「화면이 말하는 것」을 나란히 놓고 본다.
				string adaptation = match != null ? match.AdaptationNote : string.Empty;
				Debug.Log($"{TAG} 적응 — 규칙 「{adaptation}」 · 화면 경고 {alertMarks}개");
				if (string.IsNullOrEmpty(adaptation))
					Debug.Log(TAG + " 적응 — 못 쟀다(아직 아무것에도 안 익숙하다). 실패가 아니다.");

				bool mapOpen = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController mapView)
					&& mapView.IsMapOpenForVerification;
				// ★ 「안 그려졌다」와 「그릴 게 없다」는 다르다 — 서식지는 *밝힌 곳만* 그린다(시야 규칙).
				//   밝힌 서식지가 0 곳이면 이 검사는 실패가 아니라 **못 잰 것**이다.
				int exploredLairs = 0;
				if (match != null)
				{
					foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
					{
						if (match.IsExploredAt(lair.Position))
							exploredLairs++;
					}
				}

				Debug.Log($"{TAG} 지도 — 열림 {mapOpen} · 점 {mapDots}개 · 설명 붙은 점 {mapDotsWithTip}개"
					+ $" · 서식지로 읽힘 {lairDots}개 (밝힌 서식지 {exploredLairs}곳) · 마수로 읽힘 {lyingEnemyDots}개");

				if (exploredLairs == 0)
					Debug.Log(TAG + " 지도 — 서식지 표시는 못 쟀다(아직 밝힌 서식지가 0곳). 실패가 아니다.");
				else if (lairDots == 0)
					Debug.LogError(TAG + " 지도 FAIL — 밝힌 서식지가 있는데 지도에 서식지로 안 뜬다.");

				Debug.Log($"{TAG} 화면 표식 — 파도 예고 {invasionMarks}개 · 경고 {alertMarks}개"
					+ $" (규칙층 알림 {ruleAlerts}개 · 글자든 알림칸 {alertSlots}개 · 숨은 경고 {alertMarksHidden}개)");
				if (invasionMarks == 0)
					Debug.LogError(TAG + " 예고 FAIL — 다음 파도 표식이 화면에 하나도 없다. 규칙만 맞고 사람에겐 안 보인다.");
				if (alertMarks == 0)
					Debug.LogError(TAG + " 경고 FAIL — 띄운 알림이 화면까지 안 갔다.");
			}
		}

		private static Vector2 WorldToScreen(Camera camera, Vector3 worldPosition)
		{
			Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition.ToUnity()).ToSim();
			return new Vector2(screenPoint.x, screenPoint.y);
		}
	}
}
