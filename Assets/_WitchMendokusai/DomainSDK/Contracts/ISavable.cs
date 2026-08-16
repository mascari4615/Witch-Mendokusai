namespace WitchMendokusai
{
	public interface ISavable<T>
	{
		void Load(T saveData);
		T Save();
	}
}