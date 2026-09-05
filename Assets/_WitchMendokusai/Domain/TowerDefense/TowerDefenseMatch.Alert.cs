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
	// TowerDefenseMatch 의 Alert 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 「지금 어디서 무슨 일이 났나」 — 화면 밖 사건을 가장자리 표식으로 알린다.
		private readonly TowerDefenseAlerts alerts = new();

		/// <summary>
		/// 알림을 하나 띄운다(검증 전용) — 화면 표식이 *실제로 뜨는지*는 사건이 나야만 잴 수 있는데,
		/// 서식지가 깨어나거나 건물이 부서지는 것을 하네스가 만들어내긴 어렵다. 알림 자체가 화면까지
		/// 도달하는지만 여기서 확인한다(사건 발생 경로는 그 위에 얹힌 별개 문제).
		/// </summary>
		public void RaiseAlertForVerification(string label)
		{
			if (stage == null || coreCombatant == null)
				return;
			alerts.Raise(label, coreCombatant.Position, Time.time, stage.AlertSeconds);
		}

		/// <summary> 화면이 읽는 알림 목록. </summary>
		public IReadOnlyList<TowerDefenseAlerts.Alert> Alerts => alerts.Active;

		/// <summary>
		/// 마수가 무엇에 익숙해졌는지 알린다.
		///
		/// ★ 적응은 *판을 바꾸는 규칙*이다(한 수단에만 기대면 그 수단이 덜 먹힌다). 그런데 그걸 그리던
		///   칸이 숨겨지면서 화면 어디에도 안 나오게 됐다 — 그러면 사람은 자기 포탑이 고장 났다고 여긴다
		///   (이 규칙을 처음 넣을 때 정본 주석에 적어 둔 그대로다: 「안 보이는 규칙은 없는 규칙이다」).
		/// ★ 숫자가 아니라 **말**로 알린다("광역에 익숙해졌다") — 판이 도는 중에 수치를 늘어놓지
		///   않기로 한 결정과 어긋나지 않는다.
		/// ★ 바뀔 때만 한 번 — 매 프레임 띄우면 다른 알림을 덮어 정작 급한 것을 가린다.
		/// </summary>
		private void AnnounceAdaptation()
		{
			if (stage == null || coreCombatant == null)
				return;

			string note = TowerDefenseAdaptation.Describe(Adaptation);
			if (note == lastAdaptationNote)
				return;

			lastAdaptationNote = note;
			if (string.IsNullOrEmpty(note))
				return;

			// "광역에 익숙함" → "마수가 광역에 익숙해졌다".
			string spoken = "마수가 " + note.Replace("에 익숙함", "에 익숙해졌다");
			alerts.Raise(spoken, coreCombatant.Position, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 적응 — {spoken}");
		}

		/// <summary>
		/// 시간이 올린 마수 강도를 알린다.
		///
		/// ★ 이것도 숨긴 칸에 얹혀 있다가 같이 묻힌 규칙이다(적응·파도 성격에 이어 셋째). 시간이 지나면
		///   같은 마수가 더 단단해지는데, 그걸 모르면 「내 포탑이 약해졌다」로 읽는다 — 원인을 정반대로 짚는다.
		/// ★ 숫자가 아니라 **말**로, 그리고 *오를 때만* 알린다. 매 프레임 수치를 띄우면 판이 도는 중에
		///   숫자를 안 늘어놓기로 한 결정을 되돌리는 셈이 된다.
		/// </summary>
		private void AnnouncePressure()
		{
			if (stage == null || stage.PressureAnnounceStep <= 0f || coreCombatant == null)
				return;

			int step = TowerDefenseAlerts.StepFor(Pressure, 1f, stage.PressureAnnounceStep);
			if (step <= lastPressureStep)
			{
				if (step < lastPressureStep)
					lastPressureStep = step; // 판이 새로 시작되면 되돌린다.
				return;
			}

			bool first = lastPressureStep < 0;
			lastPressureStep = step;
			if (first || step <= 0)
				return; // 판 시작의 기준선은 알릴 것이 아니다.

			alerts.Raise("마수가 더 단단해졌다", coreCombatant.Position, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 강도 상승 — 마수 강도 {Pressure:F2}");
		}

		/// <summary> 알림이 뜨는 강도 간격 — 위와 같은 이유로 밖에서 읽을 수 있어야 한다. </summary>
		public float PressureAnnounceStep => stage != null ? stage.PressureAnnounceStep : 0f;

		/// <summary>
		/// 연구 길 전체 — 「몇 단계에 무엇이 열리나」. 연구 창이 이걸 그린다.
		/// 지금 열린 것과 *같은 표*에서 나오므로 창이 약속한 것은 반드시 열린다.
		/// </summary>
		public void DescribeUnlockPath(System.Collections.Generic.List<TowerDefenseUnlockEntry> into)
		{
			if (stage == null)
			{
				into?.Clear();
				return;
			}
			TowerDefenseUnlockSchedule.Build(UnlockLevels, TowerArchetypeCount, into);
		}

		/// <summary>
		/// 마지막으로 배치가 거절된 이유 — 화면·로그가 같은 문장을 쓴다.
		///
		/// ★ 왜 필요한가 (사용자 실증: "전초지기랑 연구소 설치 안됨"): 거절이 전부 조용한 false 였다.
		///   정수가 없어서인지, 이미 뭐가 서 있어서인지, 암반 위라서인지 화면이 한 마디도 안 하면
		///   플레이어에게는 「그 칸이 고장났다」로 보인다. 못 짓는 것보다 *이유를 모르는 것*이 더 나쁘다.
		/// </summary>
		public string LastRejectReason { get; private set; } = string.Empty;

		// 거절 = 이유를 그 자리에 띄우고 false. 모든 거절 경로가 이 하나를 지난다(조용한 false 금지).
		private bool Reject(string reason, Vector3 worldPosition)
		{
			LastRejectReason = reason;
			PopWorldText(reason, worldPosition, TextType.Warning);
			// 로그에도 남긴다 — 화면 글자는 흘러가고, 「왜 안 지어졌나」는 나중에 되짚어야 할 때가 온다.
			Debug.Log($"{nameof(TowerDefenseMatch)}: 배치 거절 — {reason} @ {worldPosition}");
			return false;
		}

		/// <summary> 월드 좌표 위 뜨는 글자 — UI 매니저가 아직 없으면(부팅 전/헤드리스) 조용히 넘어간다. </summary>
		private static void PopWorldText(string message, Vector3 worldPosition, TextType textType)
		{
			if (UIManager.TryGetExistingInstance(out UIManager uiManager) == false)
				return;

			uiManager.PopText(message, textType, worldPosition.ToUnity());
		}
	}
}
