using System;

namespace WitchMendokusai
{
	public static class JoystickBridge
	{
		public static Func<float> GetX = () => 0f;
		public static Func<float> GetY = () => 0f;
	}
}
