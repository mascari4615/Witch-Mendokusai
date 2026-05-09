namespace WitchMendokusai
{
	public class PlayerRegistry : Singleton<PlayerRegistry>
	{
		public Player CurrentPlayer { get; private set; }

		public void Register(Player player)
		{
			CurrentPlayer = player;
		}

		public void Unregister(Player player)
		{
			if (CurrentPlayer == player)
				CurrentPlayer = null;
		}
	}
}
