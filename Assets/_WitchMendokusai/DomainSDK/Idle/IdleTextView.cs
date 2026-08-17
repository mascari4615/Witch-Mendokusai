using System.Globalization;
using System.Text;
using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 판을 <b>한 줄 글자로</b> 찍는다 — 진단·자동검증 전용 표현 (TASK-WM-406).
    ///
    /// ★ 플레이어는 이걸 절대 안 본다. 사람이 보는 화면은 UI 표현이 그린다.
    ///   그래서 <b>예쁠 이유가 없고, 기계가 읽기 좋아야 한다</b> — 한 줄, 고정 키, 불변 문화권 숫자.
    ///
    /// ★ 왜 있어야 하나 — 시험이 화면을 읽을 수 있게 된다.
    ///   지금까지 「8시간 뒤에 막혔나」는 사람이 창을 열어야 알았다. 이게 있으면
    ///   EditMode 가 세션을 8시간 돌리고 그 결과를 문자열로 받아 판정한다.
    ///   사람은 「재밌나」만 보고, 「망가졌나」는 기계가 본다.
    ///
    /// ★ 그리고 이 파일이 표현 계약이 진짜라는 증거다 — Unity 를 import 하지 않는다.
    ///   둘째 표현이 코어를 한 줄도 안 건드리고 붙었을 때 비로소 계약이 산 것이다.
    ///
    /// 형식(고정): <c>stage=4/3:10 res=1240 ips=8.30 kills=124 hp=0.60 pp=… tier&lt;=… drops=… pot=…
    ///   heroes=3/5 stones=4 pity=43 canpull=1 bag=12/40 worn=2,0,3,0 surge=1:12.3 dmg=… spd=…</c>
    ///   앞머리는 <c>단계/이번단계처치:필요처치</c>, <c>pp</c> 는 <c>모은점수/지금환생하면/배수</c>.
    ///   <c>heroes</c> 는 <c>모은수/★합</c>, <c>pity</c> 는 천장까지 남은 횟수, <c>canpull</c> 은 지금 뽑을 수 있나(1|0),
    ///   <c>worn</c> 은 부위 순서대로 등급(0 = 빈 자리), <c>surge</c> 는 <c>종류:남은초</c>.
    ///   축은 <c>L레벨/현재값/다음비용/살수있나(1|0)</c>. 상한이면 비용 자리에 <c>max</c>.
    /// </summary>
    public sealed class IdleTextView : IGameView<IdleSnapshot>
    {
        private readonly StringBuilder buffer = new StringBuilder();

        /// <summary>모은 ★ 의 합 — 도감이 얼마나 깊은지 한 수로.</summary>
        private static int StarsOf(IdleSnapshot snapshot)
        {
            int stars = 0;

            for (int index = 0; index < snapshot.Heroes.Length; index++)
            {
                stars += snapshot.Heroes[index].Stars;
            }

            return stars;
        }

        /// <summary>마지막으로 찍은 줄. 시험·로그가 이걸 읽는다.</summary>
        public string Line { get; private set; } = string.Empty;

        public PresentationKind Kind => PresentationKind.Text;

        public void Render(IdleSnapshot snapshot)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;

            buffer.Clear();
            buffer.AppendFormat(culture, "stage={0}/{1}:{2} res={3:0.##} ips={4:0.##} kills={5} hp={6:0.00}",
                snapshot.Stage, snapshot.KillsInStage, snapshot.KillsPerStage,
                snapshot.Resource, snapshot.IncomePerSecond, snapshot.Kills, snapshot.TargetHealthRatio);

            buffer.AppendFormat(culture, " pp={0}/{1}/x{2:0.##}",
                snapshot.PrestigePoints, snapshot.PrestigeAward, snapshot.PrestigeMultiplier);

            buffer.AppendFormat(culture, " tier<={0}/{1} drops=", snapshot.MaxTierNow, snapshot.TierCeiling);
            for (int i = 0; i < snapshot.DroppedByTier.Length; i++)
            {
                if (i > 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(snapshot.DroppedByTier[i]);
            }

            buffer.AppendFormat(culture, " pot={0}/{1:0.###}", snapshot.BestPotentialGrade, snapshot.BestPotentialValue);

            // ⚠ 여기가 <b>가챠 이전 상태로 멈춰 있었다</b> (2026-08-17). 영웅·환생석·가방·폭주가
            //   통째로 안 보여서, 「기계가 화면을 읽는다」고 적어 놓고 정작 판의 절반을
            //   못 읽고 있었다. 시스템을 넣을 때 이 줄도 같이 늘려야 한다.
            buffer.AppendFormat(culture, " heroes={0}/{1} stones={2} pity={3} canpull={4}",
                snapshot.Heroes.Length, StarsOf(snapshot), snapshot.Stones,
                snapshot.PullsToPity, snapshot.CanPull ? 1 : 0);

            buffer.AppendFormat(culture, " bag={0}/{1} worn=", snapshot.Bag.Length, snapshot.BagCapacity);
            for (int slot = 0; slot < snapshot.Worn.Length; slot++)
            {
                if (slot > 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(snapshot.Worn[slot].Tier);
            }

            buffer.AppendFormat(culture, " surge={0}:{1:0.#}",
                (int)snapshot.SurgeKind, snapshot.SurgeSecondsLeft);

            AppendAxis(culture, "dmg", snapshot.Damage);
            AppendAxis(culture, "spd", snapshot.AttackSpeed);

            Line = buffer.ToString();
        }

        private void AppendAxis(CultureInfo culture, string key, IdleUpgradeView view)
        {
            buffer.AppendFormat(culture, " {0}=L{1}/{2:0.##}/", key, view.Level, view.CurrentValue);

            if (view.IsMaxed)
            {
                buffer.Append("max/0");
                return;
            }

            buffer.AppendFormat(culture, "{0:0.##}/{1}", view.NextCost, view.CanAfford ? 1 : 0);
        }
    }
}
