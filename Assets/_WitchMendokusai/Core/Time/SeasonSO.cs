using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(SeasonSO), menuName = "WM/SeasonSO")]
	public class SeasonSO : ScriptableObject
	{
		[field: Header("_" + nameof(SeasonSO))]
		[field: SerializeField] public string DisplayName { get; private set; } = "";
		[field: SerializeField] public Color Tint { get; private set; } = Color.white;

		// sub-D (WeatherSystem) 진입 시 weather weight table / BGM clip 등 추가
	}
}
