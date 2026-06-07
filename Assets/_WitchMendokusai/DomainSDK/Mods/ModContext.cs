namespace WitchMendokusai
{
	public class ModContext : IModContext
	{
		public IModContentRegistry ContentRegistry { get; }

		public ModContext(IModContentRegistry contentRegistry)
		{
			ContentRegistry = contentRegistry;
		}
	}
}
