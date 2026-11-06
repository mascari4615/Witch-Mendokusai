using UnityEngine;

namespace WitchMendokusai
{
	// 착탄과 폭발 조각. 값은 프리팹 직렬화
	public class SurvivalFeelBurst : MonoBehaviour
	{
		[SerializeField] private FeelMoment moment = new();

		private void OnEnable()
		{
			SurvivalFeel.Instance.OnWeaponBurst(transform.position, moment);
		}
	}
}
