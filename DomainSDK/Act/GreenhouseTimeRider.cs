using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 온실을 시간에 태운다 (TASK-WM-408) — 흐른 분만큼 인형들이 돌보고 작물이 자란다.
    /// 얇은 어댑터 (DomainSDK): 성장·시듦·돌봄 판정은 전부 <see cref="Greenhouse"/> 것이고,
    /// 여기는 「시간이 흘렀다」를 온실의 말로 옮기기만 한다.
    ///
    /// 돌보는 인형 목록은 바뀔 수 있으므로(사역마가 늘거나 자리를 비움) 그때그때 물어본다.
    /// </summary>
    public sealed class GreenhouseTimeRider : IActTimeRider
    {
        private static readonly int[] NO_CARERS = new int[0];

        private readonly Greenhouse greenhouse;
        private readonly IReadOnlyList<int> carerIds;

        public GreenhouseTimeRider(Greenhouse greenhouse, IReadOnlyList<int> carerIds = null)
        {
            this.greenhouse = greenhouse;
            this.carerIds = carerIds ?? NO_CARERS;
        }

        public void RideMinutes(int minutes, bool dayChanged)
        {
            if (greenhouse == null || minutes <= 0)
            {
                return;
            }

            greenhouse.TickWithCarers(carerIds, minutes);
        }
    }
}
