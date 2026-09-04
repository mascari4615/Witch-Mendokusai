using System;
using UnityEngine;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleBattlePresentation", menuName = "WM/Idle/Battle Presentation")]
	public sealed class BattlePresentationSO : ScriptableObject
	{
		[SerializeField] private GameObject dollPrefab;
		[SerializeField] private GameObject foePrefab;
		[SerializeField] private GameObject bossPrefab;
		[SerializeField] private GameObject groundPrefab;
		[SerializeField] private float partyAnchorX = -2.5f;
		[SerializeField] private float followCatchUp = 4f;
		[SerializeField] private float snapJump = 3f;
		[SerializeField] private float lungeSeconds = 0.22f;
		[SerializeField] private float boltSeconds = 0.18f;
		[SerializeField] private float positionCatchUp = 14f;

		[Header("Effect presentation")]
		[SerializeField] private float boltSize = 0.16f;
		[SerializeField] private Vector3 boltMuzzleOffset = new Vector3(0.3f, -0.4f, 0f);
		[SerializeField] private Vector3 boltTargetOffset = new Vector3(0f, -0.6f, 0f);
		[SerializeField] private float boltMissDistance = 3f;
		[SerializeField] private float volleyShakeScale = 2f;
		[SerializeField] private Vector3 volleySecondImpactOffset = new Vector3(0.25f, 0.1f, 0.25f);
		[SerializeField] private float volleyTextLift = 1f;
		[SerializeField] private Vector3 supplyImpactOffset = new Vector3(0f, -0.5f, 0f);
		[SerializeField] private Vector3 appraiseImpactOffset = new Vector3(0f, -0.35f, 0f);
		[SerializeField] private Color appraiseImpactColor = new Color(1f, 0.46f, 0.61f);
		[SerializeField] private float impactGlow = 0.8f;
		[SerializeField] private Vector3 impactEulerStep = new Vector3(47f, 73f, 29f);
		[SerializeField] private float impactLift = 0.55f;
		[SerializeField] private float impactSpinDegrees = 420f;

		[Header("Scenery presentation")]
		[SerializeField] private float sceneryBaseSize = 0.2f;
		[SerializeField] private float sceneryStepSize = 0.12f;
		[SerializeField] private float scenerySpacing = 3.1f;
		[SerializeField] private float sceneryStartX = -6f;
		[SerializeField] private float sceneryLaneOffset = 3.4f;
		[SerializeField] private float sceneryLaneStep = 0.9f;
		[SerializeField] private Vector3 sceneryEulerStep = new Vector3(23f, 37f, 13f);
		[SerializeField] private float sceneryWrapMargin = 12f;
		[SerializeField, Range(0f, 1f)] private float supplyGlowShare = 0.35f;

		[Header("Ally presentation")]
		[SerializeField] private float allyHurtSeconds = 0.15f;
		[SerializeField] private float allyHeadHeight = 1.3f;
		[SerializeField] private float dollFallbackRadius = 0.5f;
		[SerializeField] private Vector3 dollBodyPosition = new Vector3(0f, 0.42f, 0f);
		[SerializeField] private Vector3 dollBodyScale = new Vector3(0.44f, 0.9f, 0.44f);
		[SerializeField] private Vector3 dollHeadPosition = new Vector3(0f, 1f, 0f);
		[SerializeField] private Vector3 dollHeadScale = new Vector3(0.5f, 0.5f, 0.5f);
		[SerializeField] private float allyBarHeight = 1.45f;
		[SerializeField] private float allyBarWidth = 0.9f;
		[SerializeField] private float allyBarThickness = 0.11f;
		[SerializeField] private Vector3 downedEuler = new Vector3(0f, 0f, -78f);
		[SerializeField] private Vector3 downedScale = new Vector3(1f, 0.75f, 1f);
		[SerializeField] private float allyWalkBobHeight = 0.1f;
		[SerializeField] private float allyLungeDistance = 0.3f;
		[SerializeField] private float allyHurtDistance = 0.08f;

		[Header("Foe presentation")]
		[SerializeField] private float foeEntranceDistance = 6f;
		[SerializeField] private float foeEntranceSpeed = 5f;
		[SerializeField] private float foeEntranceThreshold = 0.05f;
		[SerializeField] private float foeSpinDegrees = 12f;
		[SerializeField] private float foeBobHeight = 0.025f;
		[SerializeField] private float foeHeadHeight = 0.7f;
		[SerializeField] private float foePickRadius = 54f;
		[SerializeField] private float foeMinHealthScale = 0.82f;
		[SerializeField] private float foeBarHeight = 0.95f;
		[SerializeField] private float foeBarWidth = 0.8f;
		[SerializeField] private float foeBarThickness = 0.1f;
		[SerializeField] private float shakeSeconds = 0.08f;
		[SerializeField] private float shakeDistance = 0.025f;
		[SerializeField] private float impactSize = 0.36f;
		[SerializeField] private float impactSeconds = 0.34f;
		[SerializeField] private float impactSpeed = 3.4f;
		[SerializeField] private int impactCount = 7;
		[SerializeField, Range(0f, 1f)] private float impactWhiten = 0.55f;
		[SerializeField] private float foeFlashSeconds = 0.12f;
		[SerializeField, Range(0f, 1f)] private float foeFlashWhiten = 0.7f;
		[SerializeField] private float bossScale = 1.35f;
		[SerializeField] private float foeHeight = 0.62f;
		[SerializeField] private float foeRadius = 0.62f;
		[SerializeField] private int shapeStagesPerStep = 5;
		[SerializeField] private int sceneryCount = 14;
		[SerializeField] private int bossShardCount = 6;
		[SerializeField] private float bossShellRadius = 1.15f;
		[SerializeField] private float bossShellSpread = 0.6f;
		[SerializeField] private float bossShellSpinDegrees = -26f;
		[SerializeField] private float bossSpikeInset = 0.45f;
		[SerializeField] private float bossShardRadiusScale = 0.5f;
		[SerializeField] private float bossShardThickness = 0.06f;
		[SerializeField] private float bossShardLift = 0.3f;
		[SerializeField] private Vector3 bossShardEulerStep = new Vector3(37f, 61f, 23f);
		[SerializeField] private float bossShellCatchUpShare = 0.4f;
		[SerializeField] private float bossShardSpinShare = 0.5f;
		[SerializeField] private int bossSpikeFromStage = 21;
		[SerializeField] private int colorDepthStage = 40;
		[SerializeField] private float depthSaturation = 0.35f;
		[SerializeField] private float depthDarken = 0.18f;
		[SerializeField] private float bossGlow = 0.55f;
		[SerializeField] private float meleeHeightShare = 0.85f;
		[SerializeField] private float rangedHeightShare = 1.3f;
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color enemyColor = new Color(0.55f, 0.50f, 0.63f);
		[SerializeField] private Color rangedEnemyColor = new Color(0.66f, 0.54f, 0.50f);
		[SerializeField] private Color bossColor = new Color(0.30f, 0.26f, 0.38f);
		[SerializeField] private Color sceneryColor = new Color(0.50f, 0.60f, 0.46f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);
		[SerializeField] private Color myColor = new Color(0.93f, 0.89f, 0.82f);
		[SerializeField] private Color barBackColor = new Color(0.12f, 0.13f, 0.16f, 1f);
		[SerializeField] private Color allyBarColor = new Color(0.36f, 0.78f, 0.44f);
		[SerializeField] private Color reviveBarColor = new Color(0.62f, 0.66f, 0.74f);
		[SerializeField] private Color enemyBarColor = new Color(0.91f, 0.36f, 0.36f);
		[SerializeField] private Color[] gradeColors =
		{
			new Color(0.68f, 0.72f, 0.80f),
			new Color(0.46f, 0.80f, 0.72f),
			new Color(0.72f, 0.58f, 0.92f),
			new Color(0.95f, 0.72f, 0.36f),
		};
		[SerializeField] private float numberSeconds = 0.8f;
		[SerializeField] private float numberRise = 1.2f;
		[SerializeField] private float numberSize = 0.12f;
		[SerializeField] private Color numberColor = Color.white;
		[SerializeField] private Color hurtColor = new Color(1f, 0.45f, 0.4f);
		[SerializeField] private string volleyText = "일제 사격";
		[SerializeField] private string supplyText = "보급";
		[SerializeField] private string appraiseText = "감정";
		[SerializeField] private Color cameraBackgroundColor = new Color(0.75f, 0.88f, 0.96f);
		[SerializeField] private float cameraFieldOfView = 32f;
		[SerializeField] private Vector3 cameraEuler = new Vector3(42f, 30f, 0f);
		[SerializeField] private float cameraDistance = 17f;
		[SerializeField] private Vector3 cameraTargetOffset = new Vector3(1.2f, 0.4f, 0f);
		[SerializeField] private float lightIntensity = 1.15f;
		[SerializeField] private Vector3 lightEuler = new Vector3(52f, -28f, 0f);

		public float PartyAnchorX => partyAnchorX;
		public float FollowCatchUp => followCatchUp;
		public float SnapJump => snapJump;
		public int ShapeStagesPerStep => shapeStagesPerStep;
		public int SceneryCount => sceneryCount;
		public float SceneryBaseSize => sceneryBaseSize;
		public float SceneryStepSize => sceneryStepSize;
		public float ScenerySpacing => scenerySpacing;
		public float SceneryStartX => sceneryStartX;
		public float SceneryLaneOffset => sceneryLaneOffset;
		public float SceneryLaneStep => sceneryLaneStep;
		public Vector3 SceneryEulerStep => sceneryEulerStep;
		public float SceneryWrapMargin => sceneryWrapMargin;
		public float SupplyGlowShare => supplyGlowShare;
		public Color GroundColor => groundColor;
		public Color SceneryColor => sceneryColor;
		public Color BoltColor => boltColor;
		public Color CameraBackgroundColor => cameraBackgroundColor;
		public float CameraFieldOfView => cameraFieldOfView;
		public Vector3 CameraEuler => cameraEuler;
		public float CameraDistance => cameraDistance;
		public Vector3 CameraTargetOffset => cameraTargetOffset;
		public float LightIntensity => lightIntensity;
		public Vector3 LightEuler => lightEuler;

		internal GameObject GroundPrefab => groundPrefab;

		internal BattleFx.Settings CreateFxSettings()
		{
			return new BattleFx.Settings
			{
				BoltSeconds = boltSeconds,
				BoltSize = boltSize,
				BoltMuzzleOffset = boltMuzzleOffset,
				BoltTargetOffset = boltTargetOffset,
				BoltMissDistance = boltMissDistance,
				ShakeSeconds = shakeSeconds,
				VolleyShakeScale = volleyShakeScale,
				VolleySecondImpactOffset = volleySecondImpactOffset,
				VolleyTextLift = volleyTextLift,
				SupplyImpactOffset = supplyImpactOffset,
				AppraiseImpactOffset = appraiseImpactOffset,
				AppraiseImpactColor = appraiseImpactColor,
				ShakeDistance = shakeDistance,
				NumberSeconds = numberSeconds,
				NumberRise = numberRise,
				NumberSize = numberSize,
				ImpactSize = impactSize,
				ImpactSeconds = impactSeconds,
				ImpactSpeed = impactSpeed,
				ImpactCount = impactCount,
				ImpactWhiten = impactWhiten,
				ImpactGlow = impactGlow,
				ImpactEulerStep = impactEulerStep,
				ImpactLift = impactLift,
				ImpactSpinDegrees = impactSpinDegrees,
				BoltColor = boltColor,
				NumberColor = numberColor,
				HurtColor = hurtColor,
				VolleyText = volleyText,
				SupplyText = supplyText,
				AppraiseText = appraiseText,
			};
		}

		internal BattleEntityPresenter.Settings CreateEntitySettings()
		{
			return new BattleEntityPresenter.Settings
			{
				DollPrefab = dollPrefab,
				FoePrefab = foePrefab,
				BossPrefab = bossPrefab,
				LungeSeconds = lungeSeconds,
				HurtSeconds = allyHurtSeconds,
				PositionCatchUp = positionCatchUp,
				AllyHeadHeight = allyHeadHeight,
				DollFallbackRadius = dollFallbackRadius,
				DollBodyPosition = dollBodyPosition,
				DollBodyScale = dollBodyScale,
				DollHeadPosition = dollHeadPosition,
				DollHeadScale = dollHeadScale,
				AllyBarHeight = allyBarHeight,
				AllyBarWidth = allyBarWidth,
				AllyBarThickness = allyBarThickness,
				DownedEuler = downedEuler,
				DownedScale = downedScale,
				AllyWalkBobHeight = allyWalkBobHeight,
				AllyLungeDistance = allyLungeDistance,
				AllyHurtDistance = allyHurtDistance,
				FoeEntranceDistance = foeEntranceDistance,
				FoeEntranceSpeed = foeEntranceSpeed,
				FoeEntranceThreshold = foeEntranceThreshold,
				FoeSpinDegrees = foeSpinDegrees,
				FoeBobHeight = foeBobHeight,
				BossScale = bossScale,
				FoeHeight = foeHeight,
				FoeRadius = foeRadius,
				FoeHeadHeight = foeHeadHeight,
				FoePickRadius = foePickRadius,
				FoeMinHealthScale = foeMinHealthScale,
				FoeBarHeight = foeBarHeight,
				FoeBarWidth = foeBarWidth,
				FoeBarThickness = foeBarThickness,
				ShapeStagesPerStep = shapeStagesPerStep,
				BossShardCount = bossShardCount,
				BossShellRadius = bossShellRadius,
				BossShellSpread = bossShellSpread,
				BossShellSpinDegrees = bossShellSpinDegrees,
				BossSpikeInset = bossSpikeInset,
				BossShardRadiusScale = bossShardRadiusScale,
				BossShardThickness = bossShardThickness,
				BossShardLift = bossShardLift,
				BossShardEulerStep = bossShardEulerStep,
				BossShellCatchUpShare = bossShellCatchUpShare,
				BossShardSpinShare = bossShardSpinShare,
				BossSpikeFromStage = bossSpikeFromStage,
				ColorDepthStage = colorDepthStage,
				DepthSaturation = depthSaturation,
				DepthDarken = depthDarken,
				BossGlow = bossGlow,
				FoeFlashSeconds = foeFlashSeconds,
				FoeFlashWhiten = foeFlashWhiten,
				MeleeHeightShare = meleeHeightShare,
				RangedHeightShare = rangedHeightShare,
				MyColor = myColor,
				EnemyColor = enemyColor,
				RangedEnemyColor = rangedEnemyColor,
				BossColor = bossColor,
				BarBackColor = barBackColor,
				AllyBarColor = allyBarColor,
				ReviveBarColor = reviveBarColor,
				EnemyBarColor = enemyBarColor,
				GradeColors = gradeColors,
			};
		}

		public bool TryValidate(out string error)
		{
			if (shapeStagesPerStep <= 0 || sceneryCount < 0 || bossShardCount < 0 || colorDepthStage <= 0
				|| cameraFieldOfView <= 0f || cameraDistance <= 0f || lightIntensity < 0f
				|| boltSeconds <= 0f || shakeSeconds < 0f || numberSeconds <= 0f || impactSize <= 0f
				|| impactSeconds <= 0f || impactSpeed < 0f || impactCount <= 0 || foeFlashSeconds <= 0f
				|| allyHurtSeconds <= 0f || allyHeadHeight <= 0f || dollFallbackRadius <= 0f
				|| dollBodyScale.x <= 0f || dollBodyScale.y <= 0f || dollBodyScale.z <= 0f
				|| dollHeadScale.x <= 0f || dollHeadScale.y <= 0f || dollHeadScale.z <= 0f
				|| downedScale.x <= 0f || downedScale.y <= 0f || downedScale.z <= 0f
				|| allyBarHeight <= 0f || allyBarWidth <= 0f || allyBarThickness <= 0f
				|| allyWalkBobHeight < 0f
				|| allyLungeDistance < 0f || allyHurtDistance < 0f || foeEntranceThreshold < 0f
				|| foeHeadHeight <= 0f || foePickRadius <= 0f || foeMinHealthScale <= 0f
				|| foeMinHealthScale > 1f || foeBarHeight <= 0f || foeBarWidth <= 0f
				|| foeBarThickness <= 0f
				|| bossSpikeInset <= 0f || bossShardRadiusScale <= 0f || bossShardThickness <= 0f
				|| bossShardLift < 0f || bossShellCatchUpShare < 0f || bossShardSpinShare < 0f
				|| boltSize <= 0f || boltMissDistance < 0f || volleyShakeScale < 0f || volleyTextLift < 0f
				|| impactGlow < 0f || impactLift < 0f
				|| sceneryBaseSize <= 0f || sceneryStepSize < 0f || scenerySpacing <= 0f
				|| sceneryLaneOffset < 0f || sceneryLaneStep < 0f || sceneryWrapMargin < 0f)
			{
				error = "counts and stage thresholds must be valid";
				return false;
			}

			if (string.IsNullOrWhiteSpace(volleyText) || string.IsNullOrWhiteSpace(supplyText)
				|| string.IsNullOrWhiteSpace(appraiseText))
			{
				error = "battle effect texts must not be empty";
				return false;
			}

			if (gradeColors == null || gradeColors.Length == 0)
			{
				error = "gradeColors must not be empty";
				return false;
			}

			error = string.Empty;
			return true;
		}
	}
}
