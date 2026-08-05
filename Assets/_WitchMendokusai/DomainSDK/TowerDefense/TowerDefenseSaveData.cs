using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 저장된 건물 한 채 — 무엇을, 어디에, 얼마나 자란 채로. </summary>
	[Serializable]
	public struct TowerDefenseBuildingSave
	{
		public int Kind;        // TowerDefensePlaceableKind
		public int Variant;     // 포탑 종류 번호(그 외엔 0)
		public Vector3 Position;
		public int Level;       // 승급 단계
		public int Experience;
		public int PendingChoices;
		public List<int> Perks; // TowerDefenseBuildingPerk 들
	}

	/// <summary>
	/// 판 도중 저장(TASK-WM-194) — 껐다 켜면 처음부터였다.
	///
	/// ★ 왜 「장면 통째」가 아니라 *다시 지을 수 있는 최소 정보*인가: 유닛·물리·풀 상태를 통째로 담으면
	///   프리팹이 조금만 바뀌어도 옛 저장이 통째로 깨진다. 판은 씨앗에서 다시 태어날 수 있고
	///   (경계 없는 지형 = 좌표에서 파생), 내가 한 일은 「무엇을 어디에 세웠나」로 전부 적힌다.
	///   그러면 저장 파일이 작고, 프리팹이 바뀌어도 살아남는다.
	/// ★ 마수는 저장하지 않는다 — 지금 걷고 있는 마수를 되살리는 것보다 *다시 몰려오게* 두는 편이
	///   규칙이 단순하고, 불러온 직후 잠깐의 숨돌릴 틈이 오히려 자연스럽다.
	///
	/// 순수 데이터 — 씬 0.
	/// </summary>
	[Serializable]
	public class TowerDefenseSaveData
	{
		/// <summary> 저장 형식 번호 — 올릴 때마다 옛 저장을 조용히 버릴지 옮길지 정한다. </summary>
		public const int CURRENT_VERSION = 1;

		public int Version = CURRENT_VERSION;
		public string StageId;

		// 판을 다시 만드는 데 필요한 것 — 씨앗 하나면 지형이 통째로 되살아난다.
		public int MapSeed;
		public int MapWidth;
		public int MapLength;

		public int Difficulty;
		public float ElapsedSeconds;
		public int WaveIndex;
		public int Resource;
		public int Essence;
		public int Lives;

		public int CoreLevel;
		public int CoreExperience;
		public int CorePendingChoices;
		public int ResearchLevel;
		public List<int> TakenBoons = new();

		// ★ 연구 성좌에서 찍은 마디 번호들. 이걸 안 적으면 이어할 때 연구가 통째로 사라진다 —
		//   판을 오래 굴릴수록 잃는 게 커지므로, 「잠깐 접어둔다」가 사실상 「버린다」가 된다.
		//   번호만 적는다(효과·값은 이 판의 규칙에서 다시 나온다 — 같은 규칙 = 같은 값).
		public List<int> TakenResearch = new();

		public int NestsDestroyed;
		public List<Vector3> DestroyedNestPositions = new();

		public List<TowerDefenseBuildingSave> Buildings = new();

		/// <summary> 이 저장이 지금 형식과 맞는가 — 안 맞으면 조용히 버린다(깨진 판을 되살리는 것이 더 나쁘다). </summary>
		public bool IsCompatible => Version == CURRENT_VERSION && string.IsNullOrEmpty(StageId) == false;

		/// <summary> 이어할 만한 저장인가 — 끝난 판을 되살리면 「이어하기」가 거짓말이 된다. </summary>
		public bool IsResumable => IsCompatible && Lives > 0 && Buildings != null;

		/// <summary> 화면이 「몇 분짜리 판이 저장돼 있다」를 말할 때 쓴다. </summary>
		public string Describe()
		{
			int minutes = Mathf.FloorToInt(ElapsedSeconds / 60f);
			int seconds = Mathf.FloorToInt(ElapsedSeconds % 60f);
			return minutes + "분 " + seconds + "초  ·  건물 " + (Buildings != null ? Buildings.Count : 0) + "채"
				+ "  ·  목숨 " + Lives;
		}
	}
}
