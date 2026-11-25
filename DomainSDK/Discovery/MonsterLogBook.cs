using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 몬스터 기록 장부. 몬스터 ID 마다 처치, 정예 처치, 쓰러진 횟수와 처음 잡은 날. 단계는 처치 수에서 매번 셈 (저장 안 함).
    /// 목표 표를 바꾸면 옛 세이브도 새 표로 다시 매겨짐. 엔진 없음
    /// </summary>
    public sealed class MonsterLogBook
    {
        public const int NO_DUNGEON = -1;

        private readonly Dictionary<int, MonsterLogRecord> records;

        /// <param name="records">세이브 조각이 들고 있는 사전. 복사하지 않고 그대로 씀</param>
        public MonsterLogBook(Dictionary<int, MonsterLogRecord> records)
        {
            this.records = records;
        }

        /// <summary>기록이 없으면 null</summary>
        public MonsterLogRecord Find(int monsterId) => records.TryGetValue(monsterId, out MonsterLogRecord record) ? record : null;

        public int KillsOf(int monsterId) => Find(monsterId)?.Kills ?? 0;

        /// <summary>잡았거나 쓰러진 적이 있는 종. 이름과 그림이 보임</summary>
        public bool IsMet(int monsterId)
        {
            MonsterLogRecord record = Find(monsterId);
            return record != null && (record.Kills > 0 || record.Defeats > 0);
        }

        public int StageOf(int monsterId, IReadOnlyList<int> targets) => DiscoveryStages.Resolve(KillsOf(monsterId), targets);

        /// <summary>확인 안 한 새 단계가 있는지</summary>
        public bool HasNews(int monsterId, IReadOnlyList<int> targets)
        {
            MonsterLogRecord record = Find(monsterId);
            return record != null && StageOf(monsterId, targets) > record.SeenStage;
        }

        /// <summary>한 마리 처치. 바뀌기 전 단계와 바뀐 뒤 단계를 돌려줌</summary>
        public (int Before, int After) AddKill(int monsterId, bool isElite, int dungeonId, string today, IReadOnlyList<int> targets)
        {
            MonsterLogRecord record = GetOrCreate(monsterId);
            int before = DiscoveryStages.Resolve(record.Kills, targets);
            record.Kills++;
            if (isElite)
            {
                record.EliteKills++;
            }
            if (record.FirstKilledOn == null)
            {
                record.FirstKilledOn = today;
                record.FirstDungeonId = dungeonId;
            }
            return (before, DiscoveryStages.Resolve(record.Kills, targets));
        }

        public void AddDefeat(int monsterId) => GetOrCreate(monsterId).Defeats++;

        public void MarkSeen(int monsterId, IReadOnlyList<int> targets)
        {
            MonsterLogRecord record = Find(monsterId);
            if (record != null)
            {
                record.SeenStage = DiscoveryStages.Resolve(record.Kills, targets);
            }
        }

        private MonsterLogRecord GetOrCreate(int monsterId)
        {
            if (records.TryGetValue(monsterId, out MonsterLogRecord record) == false)
            {
                record = new MonsterLogRecord();
                records[monsterId] = record;
            }
            return record;
        }
    }
}
