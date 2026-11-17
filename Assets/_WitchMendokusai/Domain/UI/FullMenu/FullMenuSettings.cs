using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴 (ESC, 폰 뒤로가기) 의 모양과 수치. 색, 간격, 글자 크기는 FullMenu.uss
	/// </summary>
	[CreateAssetMenu(fileName = nameof(FullMenuSettings), menuName = "WM/UI/" + nameof(FullMenuSettings))]
	public class FullMenuSettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("재화 칸")] public Texture2D CoinIcon { get; private set; }
		[field: SerializeField] public Texture2D SettingIcon { get; private set; }
		[field: SerializeField] public Texture2D QuitIcon { get; private set; }
		[field: SerializeField, Tooltip("단축키 안내")] public Texture2D KeybindIcon { get; private set; }

		[field: SerializeField, Tooltip("게임 화면 흐림 출력 (CustomBlurFeature). 메뉴 배경")] public RenderTexture BlurOutput { get; private set; }

		[field: Header("배치")]
		[field: SerializeField, Tooltip("USS 기준 화면 높이. 화면 높이에 맞춰 전체를 키우고 줄임")] public float ReferenceHeight { get; private set; } = 1080f;
		[field: SerializeField, Tooltip("기준 화면 최소 너비. 이보다 좁은 화면은 너비에 맞춤")] public float MinimumWidth { get; private set; } = 1600f;

		[field: Header("칸 이동")]
		[field: SerializeField] public float NavOverlapTolerance { get; private set; } = 4f;
		[field: SerializeField] public float NavAcrossWeight { get; private set; } = 2f;

		[field: Header("가운데 코어")]
		[field: SerializeField, Tooltip("가운데에서 도는 모델. 비우면 코어 없이 열림")] public GameObject CorePrefab { get; private set; }
		[field: SerializeField] public int CoreTextureSize { get; private set; } = 768;
		[field: SerializeField, Tooltip("초당 회전 각도")] public float CoreRotateSpeed { get; private set; } = 16f;
		[field: SerializeField, Tooltip("위아래로 떠다니는 폭")] public float CoreBobHeight { get; private set; } = 0.035f;
		[field: SerializeField, Tooltip("한 번 오르내리는 시간 (초)")] public float CoreBobPeriod { get; private set; } = 4.5f;
		[field: SerializeField] public float CoreCameraDistance { get; private set; } = 2.6f;
		[field: SerializeField, Tooltip("카메라가 내려다보는 각도")] public float CoreCameraPitch { get; private set; } = 10f;
		[field: SerializeField] public float CoreCameraFieldOfView { get; private set; } = 26f;
		[field: SerializeField, Tooltip("빛나는 부분의 밝기가 한 번 오르내리는 시간 (초)")] public float CorePulsePeriod { get; private set; } = 3.2f;
	}
}
