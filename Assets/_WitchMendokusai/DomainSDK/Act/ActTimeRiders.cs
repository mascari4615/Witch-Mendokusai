using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 시간을 타는 것 여럿을 하나로 묶는다 (TASK-WM-408) — 온실도 자라고 몸도 배고파진다.
    /// 순수 POCO (DomainSDK). 등록 순서대로 태운다(결정성 — 같은 세계는 같은 순서로 늙는다).
    /// </summary>
    public sealed class ActTimeRiders : IActTimeRider
    {
        private readonly List<IActTimeRider> riders = new();

        public ActTimeRiders(params IActTimeRider[] initial)
        {
            if (initial == null)
            {
                return;
            }

            for (int i = 0; i < initial.Length; i++)
            {
                Add(initial[i]);
            }
        }

        public int Count => riders.Count;

        public ActTimeRiders Add(IActTimeRider rider)
        {
            if (rider != null)
            {
                riders.Add(rider);
            }

            return this;
        }

        public void RideMinutes(int minutes, bool dayChanged)
        {
            for (int i = 0; i < riders.Count; i++)
            {
                riders[i].RideMinutes(minutes, dayChanged);
            }
        }
    }
}
