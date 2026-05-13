using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class UIBarGameStat : UIBarStat<GameStatType>
	{
		private DataManager dataManager;

		[Inject]
		public void Construct(DataManager dataManager)
		{
			this.dataManager = dataManager;
		}

		private void Start()
		{
			BindStat(dataManager.GameStat);
		}
	}
}
