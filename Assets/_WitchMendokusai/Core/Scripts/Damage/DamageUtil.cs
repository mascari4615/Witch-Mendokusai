namespace WitchMendokusai
{
	public static class DamageUtil
	{
		public static TextType DamageTypeToTextType(DamageType damageType)
		{
			return damageType switch
			{
				DamageType.Normal => TextType.Normal,
				DamageType.Critical => TextType.Critical,
				_ => TextType.Normal,
			};
		}
	}
}