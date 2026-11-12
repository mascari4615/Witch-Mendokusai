using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 무대 소품 배치 설정. TitleStageBuilder 입력
	[CreateAssetMenu(fileName = nameof(TitleStageRecipe), menuName = "WM/Title/TitleStageRecipe")]
	public class TitleStageRecipe : ScriptableObject
	{
		[Header("Seed")]
		public int Seed = 4615;

		[Header("Materials")]
		public Material GroundSource;
		public float GroundColorScale = 1.2f;
		public Material GrassSource;
		public Material GrassLit;

		[Header("Big tree")]
		public GameObject TreePrefab;
		public Vector2 BigTreePosition = new(7f, 3f);
		public float BigTreeScale = 3.2f;

		[Header("Character")]
		public GameObject CharacterModel;
		public RuntimeAnimatorController CharacterAnimator;
		[Tooltip("큰 나무 기준 위치")]
		public Vector2 CharacterOffset = new(-4.5f, -5f);
		public float CharacterScale = 1.5f;
		public float CharacterYaw = 200f;

		[Header("Forest")]
		public int ForestCount = 34;
		public Vector2 ForestRadius = new(55f, 150f);
		public Vector2 ForestScale = new(1.1f, 2.4f);
		[Tooltip("카메라 쪽 (음의 z) 으로 이 각도 범위는 비움")]
		public float ForestFrontGapDegrees = 70f;

		[Header("Shrub")]
		public GameObject ShrubPrefab;
		public int ShrubCount = 26;
		[Tooltip("이 z 보다 카메라 쪽에는 덤불을 두지 않음")]
		public float ShrubFrontLimit = -25f;
		public float ShrubRadius = 60f;
		public Vector2 ShrubScale = new(0.8f, 1.6f);

		[Header("Grass")]
		public GameObject GrassPrefab;
		public int GrassCount = 650;
		public float GrassRadius = 70f;
		public Vector2 GrassScale = new(0.9f, 1.8f);

		[Header("Gravel")]
		public GameObject GravelPrefab;
		public int GravelCount = 26;
		public float GravelRadius = 45f;

		[Header("Lobby install")]
		public SkyPresetSO SkyPreset;
		public Material SkyboxMaterial;
		[Range(0f, 24f)] public float Hour = 14.5f;
		[Tooltip("y 는 지면 위 높이")]
		public Vector3 CameraPosition = new(-8f, 1.6f, -92f);
		public Vector3 CameraEuler = new(-9f, 6f, 0f);
		public float CameraFieldOfView = 40f;

		[Header("Lobby UI")]
		public Vector2 TitleOffset = new(0f, -120f);
		public Vector2 TitleSize = new(1600f, 220f);
		public Vector2 MenuOffset = new(0f, 40f);
		public Vector2 MenuSize = new(1200f, 80f);
		public float MenuFontSize = 46f;
		public Vector2 FooterMargin = new(40f, 28f);
		public Vector2 FooterSize = new(600f, 50f);
	}
}
