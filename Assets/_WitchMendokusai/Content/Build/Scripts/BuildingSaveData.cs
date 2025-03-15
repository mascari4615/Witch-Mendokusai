using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct BuildingSaveData
	{
		public Guid? Guid;
		public RuntimeBuildingState State;

		public int SO_ID;
	}
}