using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 연구 단계마다 무엇이 열리는가 — **해금의 단일 정본** (TASK-WM-194 / WM-200).
	///
	/// ★ 왜 떼어냈나: 「지금 쓸 수 있는 칸」은 규칙층이 만들고, 「앞으로 무엇이 열리나」는 연구 창이
	///   보여줘야 한다. 둘을 각자 계산하면 *연구 창이 약속한 것과 실제로 열리는 것이 어긋난다* —
	///   그건 플레이어가 자원을 잘못 쓰게 만드는 거짓말이고, 화면을 통째로 못 믿게 된다.
	///   한 표를 만들어 규칙층은 「여기까지」를 잘라 쓰고, 화면은 표 전체를 그린다.
	///
	/// ★ 순수 계산 — 무대 데이터(SO)도 화면도 모른다. 숫자만 먹고 표를 낸다.
	/// </summary>
	public static class TowerDefenseUnlockSchedule
	{
		/// <summary>
		/// 전체 일정표 — 0단계(늘 열린 것)부터 마지막 해금까지. 무엇이 이미 열렸는지는
		/// 소비자가 <see cref="TowerDefenseUnlockEntry.Level"/> 을 지금 단계와 견줘 정한다.
		/// </summary>
		public static void Build(in TowerDefenseUnlockLevels levels, int towerArchetypeCount, List<TowerDefenseUnlockEntry> into)
		{
			if (into == null)
				return;
			into.Clear();

			// 먹고사는 길은 늘 열려 있다 — 이게 없으면 첫 수가 아예 없다.
			into.Add(new TowerDefenseUnlockEntry(0, TowerDefensePlaceableKind.Harvester));

			int archetypes = towerArchetypeCount > 0 ? towerArchetypeCount : 1;
			int step = levels.TowerVariantStep > 0 ? levels.TowerVariantStep : 1;
			for (int index = 0; index < archetypes; index++)
				into.Add(new TowerDefenseUnlockEntry(levels.Tower + index * step, TowerDefensePlaceableKind.Tower, index));

			into.Add(new TowerDefenseUnlockEntry(levels.Wall, TowerDefensePlaceableKind.Wall));
			into.Add(new TowerDefenseUnlockEntry(levels.Trap, TowerDefensePlaceableKind.Trap));
			into.Add(new TowerDefenseUnlockEntry(levels.Generator, TowerDefensePlaceableKind.Generator));
			into.Add(new TowerDefenseUnlockEntry(levels.Outpost, TowerDefensePlaceableKind.Outpost));

			into.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.TowerIndex.CompareTo(b.TowerIndex));
		}

		/// <summary> 지금 단계에서 쓸 수 있는 칸 — 위 표를 「여기까지」로 자른 것. </summary>
		public static void Available(in TowerDefenseUnlockLevels levels, int towerArchetypeCount, int researchLevel,
			List<TowerDefenseUnlockEntry> scratch, List<TowerDefenseSlot> into)
		{
			if (into == null)
				return;
			into.Clear();

			List<TowerDefenseUnlockEntry> entries = scratch ?? new List<TowerDefenseUnlockEntry>();
			Build(levels, towerArchetypeCount, entries);

			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Level > researchLevel)
					continue;
				into.Add(new TowerDefenseSlot(entries[i].Kind, entries[i].TowerIndex));
			}
		}
	}

	/// <summary> 표 한 줄 — 「몇 단계에 무엇이 열리는가」. </summary>
	public readonly struct TowerDefenseUnlockEntry
	{
		public TowerDefenseUnlockEntry(int level, TowerDefensePlaceableKind kind, int towerIndex = 0)
		{
			Level = level < 0 ? 0 : level;
			Kind = kind;
			TowerIndex = towerIndex;
		}

		/// <summary> 이 단계부터 열린다 (0 = 처음부터). </summary>
		public int Level { get; }

		public TowerDefensePlaceableKind Kind { get; }

		/// <summary> 포탑 종류 번호(포탑이 아니면 뜻 없음). </summary>
		public int TowerIndex { get; }
	}

	/// <summary>
	/// 해금 단계 수치 묶음 — 무대 데이터(SO)가 정본이고, 여기로 넘겨서 계산만 시킨다
	/// (수치 노출 룰: 여기에 기본값을 박지 않는다).
	/// </summary>
	public readonly struct TowerDefenseUnlockLevels
	{
		public TowerDefenseUnlockLevels(int tower, int wall, int trap, int generator, int outpost, int towerVariantStep)
		{
			Tower = tower;
			Wall = wall;
			Trap = trap;
			Generator = generator;
			Outpost = outpost;
			TowerVariantStep = towerVariantStep;
		}

		public int Tower { get; }
		public int Wall { get; }
		public int Trap { get; }
		public int Generator { get; }
		public int Outpost { get; }

		/// <summary> 포탑 종류가 하나씩 더 열리는 간격(단계). </summary>
		public int TowerVariantStep { get; }
	}
}
