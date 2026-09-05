namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 행동이 왜 안 됐나 (TASK-WM-408). 순수 enum (DomainSDK).
    /// 「그냥 실패」로 뭉뚱그리지 않는 이유: 표현층이 「기운이 없다」와 「씨앗이 없다」를
    /// 다르게 말해 줘야 하고, 그 판단을 표현층이 다시 추측하게 두면 두 자리가 어긋난다.
    /// </summary>
    public enum ActRejection
    {
        None = 0,
        Need = 1,
        Resource = 2,
    }
}
