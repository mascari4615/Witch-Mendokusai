namespace WitchMendokusai
{
	public interface IGameConditionBridge
	{
		bool this[GameConditionType conditionType] { get; }
		bool IsGameConditionAny(params GameConditionType[] conditions);
	}
}
