using UnityEngine;

namespace WitchMendokusai
{
	// 이번 판의 숙련 막대. 판 시작에 GameManager 가 인물 자료로 새로 걸고, 숙련 없는 인물이면 비움
	// 트리 판정 (SkillTreeNode.IsUnlocked) 과 조각 효과 (MonsterSpawner) 가 여기를 읽음
	public static class DollMasteryRun
	{
		private static DollMastery active;

		public static DollMasteryData Data { get; private set; }
		public static DollMastery Active => active;

		public static bool AcceptsShards => active != null && active.AcceptsShards;

		public static void Begin(DollMasteryData data)
		{
			if (data != null)
				data.ThrowIfInvalid();
			Data = data;
			active = data != null ? new DollMastery(data.Thresholds()) : null;
		}

		public static bool IsTrackReady(int track) => active != null && track < active.TrackCount && active.IsReady(track);

		// 막대가 이번에 찼으면 true
		public static bool AddShard(int track, int amount)
		{
			if (active == null)
				return false;
			bool becameReady = active.AddShard(track, amount);
			if (becameReady)
				Debug.Log($"[Mastery] 막대 {track} 참 ({active.Progress(track)}/{active.Threshold(track)})");
			return becameReady;
		}

		// 형태 자료 (ConfirmMasteryTrack) 와 인물 숙련 자료가 어긋나면 여기서 멈춤
		public static void Confirm(int track)
		{
			if (active == null)
				throw new System.InvalidOperationException($"숙련 없는 인물에서 막대 {track} 확정. 형태의 ConfirmMasteryTrack 이나 인물의 Mastery 자료 확인");
			active.Confirm(track);
			Debug.Log($"[Mastery] 막대 {track} 로 확정");
		}

		public static void SetActiveForTest(DollMastery mastery)
		{
			Data = null;
			active = mastery;
		}
	}
}
