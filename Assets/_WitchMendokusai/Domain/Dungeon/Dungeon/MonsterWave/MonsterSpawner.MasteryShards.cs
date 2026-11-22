using UnityEngine;

namespace WitchMendokusai
{
	// 숙련 조각 배치. 인물 숙련 자료의 일정대로 주인공 둘레에 줍는 조각을 놓음 (사용자 2026-11-22 22:26 '경험치처럼 닿으면 획득')
	// 숙련 없는 인물 판에는 아무것도 안 함. 일정 정본 DollMasteryShardSchedule, 줍기 MasteryShardObject
	public partial class MonsterSpawner
	{
		private DollMasteryShardSchedule shardSchedule;
		private int nextShard;
		private System.Random shardRandom;

		private void InitMasteryShards()
		{
			DollMasteryData mastery = DollMasteryRun.Data;
			shardSchedule = mastery != null ? mastery.CreateSchedule() : null;
			nextShard = 0;
			shardRandom = mastery != null ? new System.Random(RunSeed.Derive(mastery.ShardSeedSalt)) : null;
		}

		private void StopMasteryShards() => shardSchedule = null;

		// 판 시간 기준. 자리를 못 찾으면 (지면 생성 전) 다음 틱에 다시
		private void UpdateMasteryShards()
		{
			if (shardSchedule == null)
				return;
			int due = shardSchedule.DueCount((float)dungeonManager.Context.ElapsedTime.TotalSeconds);
			while (nextShard < due)
			{
				if (TrySpawnMasteryShard(shardSchedule.TrackAt(nextShard)) == false)
					return;
				nextShard++;
			}
		}

		private bool TrySpawnMasteryShard(int track)
		{
			DollMasteryData mastery = DollMasteryRun.Data;
			Vector3 origin = playerProvider.Current.transform.position;
			double angle = shardRandom.NextDouble() * Mathf.PI * 2f;
			float distance = Mathf.Lerp(mastery.ShardDistance.x, mastery.ShardDistance.y, (float)shardRandom.NextDouble());
			Vector3 candidate = origin + new Vector3(Mathf.Cos((float)angle), 0f, Mathf.Sin((float)angle)) * distance;
			if (TryMapObjectGround(candidate, MapObjectSettings.Load(), out Vector3 ground) == false)
				return false;

			GameObject shard = objectPoolManager.Spawn(mastery.Tracks[track].ShardPrefab.gameObject, ground);
			shard.GetComponent<MasteryShardObject>().Init(track);
			shard.SetActive(true);
			return true;
		}
	}
}
