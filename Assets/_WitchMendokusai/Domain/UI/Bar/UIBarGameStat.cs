using UnityEngine;

namespace WitchMendokusai
{
	public class UIBarGameStat : UIBarStat<GameStatType>
	{
		private void Start()
		{
			BindStat(DataManager.Instance.GameStat);
		}
	}
}
