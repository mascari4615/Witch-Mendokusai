using UnityEngine;

namespace WitchMendokusai
{
	// 이번 판의 숙련 막대. 판 시작에 GameManager 가 인물 자료로 새로 걸고, 숙련 없는 인물이면 비움
	// 트리 판정 (SkillTreeNode.IsUnlocked) 과 조각 효과 (MonsterSpawner) 가 여기를 읽음
	public static class DollMasteryRun
	{
		private static DollMastery active;
		// 막대별로 이번 판에 조각 스탯으로 준 양 (상한 판정)
		private static int[] grantedPickup = new int[0];

		public static DollMasteryData Data { get; private set; }
		public static DollMastery Active => active;

		public static bool AcceptsShards => active != null && active.AcceptsShards;

		public static void Begin(DollMasteryData data)
		{
			if (data != null)
				data.ThrowIfInvalid();
			Data = data;
			active = data != null ? new DollMastery(data.Thresholds()) : null;
			grantedPickup = new int[data != null ? data.Tracks.Count : 0];
		}

		// 조각 하나를 먹었을 때 막대 스탯에 더할 양. 상한을 넘는 몫은 버림
		public static int TakePickupGrant(int track, int shards)
		{
			DollMasteryTrackData trackData = Data.Tracks[track];
			int grant = DollMastery.PickupGrant(grantedPickup[track], trackData.PickupAmount * shards, trackData.PickupCap);
			grantedPickup[track] += grant;
			return grant;
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
