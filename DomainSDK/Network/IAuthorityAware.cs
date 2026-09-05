namespace WitchMendokusai
{
    public interface IAuthorityAware
    {
        Authority RequiredAuthority { get; }
    }
}
