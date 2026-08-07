using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 웨이브에 붙는 성격. None 이면 평범한 웨이브. </summary>
	public enum TowerDefenseWaveEventKind
	{
		None = 0,
		Swarm = 1, // 떼거리 — 수가 폭증하고 약하다. 광역·함정이 답.
		Elite = 2, // 정예 — 적고 단단하다. 관통·집중이 답.
		Rush = 3,  // 돌진 — 전부 빠르다. 둔화가 답.
		Gloom = 4, // 어스름 — 시야가 좁아진다. 「보이는 만큼만 쏜다」가 아프게 걸린다.
	}

	/// <summary>
	/// 이벤트 웨이브(TASK-WM-194) — 웨이브가 *수만 느는* 것에서 **성격이 변하는** 것으로.
	///
	/// ★ 왜 필요한가: 지금 판 중반이 평평하다. 웨이브가 escalation 을 수치로만 하면 5파와 15파가
	///   「같은 일을 더 오래」다. 몇 파마다 성격이 바뀌면 그때마다 판단이 새로 필요해진다.
	/// ★ 결정론: 웨이브 번호만으로 정해진다 — 예고에 띄울 수 있고, 대비가 운에 좌우되지 않는다
	///   (무작위면 준비가 무효화되고 예고가 거짓말이 된다. 구성 계산과 같은 원칙).
	///
	/// 순수 정적 — Unity 씬·RNG 0.
	/// </summary>
	public static class TowerDefenseWaveEvent
	{
		/// <summary> waveIndex(0-based) 파의 성격. every 파마다 한 번씩, 종류는 순환. </summary>
		public static TowerDefenseWaveEventKind For(int waveIndex, int every)
		{
			if (every <= 0 || waveIndex <= 0)
				return TowerDefenseWaveEventKind.None;
			if ((waveIndex + 1) % every != 0)
				return TowerDefenseWaveEventKind.None;

			int cycle = (waveIndex + 1) / every - 1;
			return (TowerDefenseWaveEventKind)(cycle % 4 + 1);
		}

		/// <summary> 마수 수 배수. 떼거리는 많이, 정예는 적게. </summary>
		public static float CountScale(TowerDefenseWaveEventKind kind)
		{
			return kind switch
			{
				TowerDefenseWaveEventKind.Swarm => 2f,
				TowerDefenseWaveEventKind.Elite => 0.5f,
				_ => 1f,
			};
		}

		/// <summary> 체력 배수. 정예는 단단하고 떼거리는 물렁하다. </summary>
		public static float HealthScale(TowerDefenseWaveEventKind kind)
		{
			return kind switch
			{
				TowerDefenseWaveEventKind.Swarm => 0.6f,
				TowerDefenseWaveEventKind.Elite => 3f,
				_ => 1f,
			};
		}

		/// <summary> 속도 배수. 돌진 웨이브는 전부 빠르다. </summary>
		public static float SpeedScale(TowerDefenseWaveEventKind kind)
		{
			return kind == TowerDefenseWaveEventKind.Rush ? 1.6f : 1f;
		}

		/// <summary> 시야 배수. 어스름 웨이브엔 보이는 범위가 줄어든다. </summary>
		public static float VisionScale(TowerDefenseWaveEventKind kind)
		{
			return kind == TowerDefenseWaveEventKind.Gloom ? 0.6f : 1f;
		}

		/// <summary> 화면에 띄울 이름 — 예고가 「무엇이 오는가」를 말해야 대비가 성립한다. </summary>
		public static string DisplayName(TowerDefenseWaveEventKind kind)
		{
			return kind switch
			{
				TowerDefenseWaveEventKind.Swarm => "떼거리",
				TowerDefenseWaveEventKind.Elite => "정예",
				TowerDefenseWaveEventKind.Rush => "돌진",
				TowerDefenseWaveEventKind.Gloom => "어스름",
				_ => string.Empty,
			};
		}

		/// <summary>
		/// 「떼거리가」/「돌진이」 — 이름 뒤에 붙는 조사까지 붙여 준다.
		///
		/// ★ 예고는 한 문장으로 읽혀야 한다("떼거리가 북동에서 온다"). 조사를 안 맞추면
		///   「돌진가 온다」처럼 어색해서, 읽는 사람이 문장이 아니라 *버그*를 먼저 본다.
		/// ★ 판정은 한글 받침 유무 하나면 된다(유니코드 한글 음절은 11172자가 규칙적으로 배열돼 있어
		///   (코드 - 0xAC00) % 28 == 0 이면 받침이 없다).
		/// </summary>
		public static string SubjectPhrase(TowerDefenseWaveEventKind kind)
		{
			string name = DisplayName(kind);
			if (string.IsNullOrEmpty(name))
				return string.Empty;

			char last = name[name.Length - 1];
			bool hasFinalConsonant = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
			return name + (hasFinalConsonant ? "이 " : "가 ");
		}

		/// <summary> 그 성격의 색 — 예고 글자에 입혀 한눈에 갈리게. </summary>
		public static Color DisplayColor(TowerDefenseWaveEventKind kind)
		{
			return kind switch
			{
				TowerDefenseWaveEventKind.Swarm => new Color(1f, 0.75f, 0.3f, 1f),
				TowerDefenseWaveEventKind.Elite => new Color(0.8f, 0.45f, 1f, 1f),
				TowerDefenseWaveEventKind.Rush => new Color(1f, 0.4f, 0.4f, 1f),
				TowerDefenseWaveEventKind.Gloom => new Color(0.55f, 0.7f, 0.95f, 1f),
				_ => new Color(1f, 0.72f, 0.45f, 1f),
			};
		}
	}
}
