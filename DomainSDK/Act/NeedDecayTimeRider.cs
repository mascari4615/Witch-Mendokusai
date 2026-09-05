using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 몸을 시간에 태운다 (TASK-WM-408) — 흐른 분만큼 배가 고파지고 기운이 준다.
    /// 얇은 어댑터 (DomainSDK): 감소 규칙은 전부 <see cref="NeedModel.Step"/> 것이다.
    ///
    /// ★ 이 자리가 따로 있어야 하는 이유: 자연 감소를 <see cref="ActLedger"/> 가 걸면
    ///   행동이 선언한 소모와 겹쳐 <b>같은 감소가 두 번</b> 걸린다. 선언은 원장이, 흐름은 여기가.
    /// </summary>
    public sealed class NeedDecayTimeRider : IActTimeRider
    {
        private readonly NeedState state;
        private readonly NeedProfile profile;

        public NeedDecayTimeRider(NeedState state, NeedProfile profile)
        {
            this.state = state;
            this.profile = profile;
        }

        public void RideMinutes(int minutes, bool dayChanged)
        {
            if (state == null || profile == null || minutes <= 0)
            {
                return;
            }

            NeedModel.Step(state, profile, minutes);
        }
    }
}
