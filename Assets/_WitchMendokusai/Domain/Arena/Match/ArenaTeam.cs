using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 매치 안의 한 진영(≤3기). 승리 판정(ArenaModeSO.CheckVictory)의 입력 단위.
	/// Members = 전투 객체(ICombatant)만 — 전술(TacticProgram) 매핑은 ArenaMatch 가 별도 소유(item 8).
	/// 승리는 "누가 살아있나"만 보므로 ICombatant 리스트로 충분(전술 미참조 = write-only 데드필드 회피).
	/// </summary>
	public class ArenaTeam
	{
		public int TeamId { get; }
		public List<ICombatant> Members { get; }

		public ArenaTeam(int teamId, List<ICombatant> members)
		{
			TeamId = teamId;
			Members = members ?? new List<ICombatant>();
		}

		/// <summary> 생존 멤버 수. </summary>
		public int AliveCount()
		{
			int count = 0;
			foreach (ICombatant member in Members)
			{
				if (member != null && member.IsAlive)
					count++;
			}
			return count;
		}

		/// <summary> 한 명이라도 살아있으면 true (전멸 판정용). </summary>
		public bool AnyAlive()
		{
			foreach (ICombatant member in Members)
			{
				if (member != null && member.IsAlive)
					return true;
			}
			return false;
		}
	}
}
