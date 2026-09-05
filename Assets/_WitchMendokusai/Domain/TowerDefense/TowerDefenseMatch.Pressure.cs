using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	// TowerDefenseMatch 의 압력, 소리, 적응 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		/// <summary> 깨기 전에 「소리가 크다」고 미리 알린 횟수 — 대응할 기회를 줬는지의 창. </summary>
		public int NoiseWarnings { get; private set; }

		// 마지막으로 알린 적응 — 같은 말을 매 프레임 다시 띄우지 않기 위해.
		private string lastAdaptationNote = string.Empty;

		// 마지막으로 알린 강도 단계 — 같은 단계를 다시 알리지 않기 위해.
		private int lastPressureStep = -1;

		/// <summary> 지금 적응이 무엇이라 말하는가 — 하네스가 「보이는가」를 잴 때 기준으로 쓴다. </summary>
		public string AdaptationNote => TowerDefenseAdaptation.Describe(Adaptation);

		/// <summary> 지금 뜨거운 뚫린 자리 수 — 화면·검사가 「규칙이 살아 있나」를 볼 창. </summary>
		public int BreachHotCount => breach.HotCount;

		/// <summary>
		/// 판의 시계를 앞으로 감는다 — 검사 전용.
		///
		/// ★ 왜 필요한가: 마수 강도는 *시간*이 올린다. 한 칸 오르는 데 실제로 몇 분이 걸려서
		///   하네스가 도는 1~2분 안에는 절대 안 오른다 — 그래서 「강도가 올랐다」는 알림이
		///   여태 한 번도 화면에 안 떴고, 계산만 시험으로 덮인 채 남아 있었다.
		///   재는 쪽이 사건을 일으킬 수 있어야 닫힌다(적응·뚫린 자리에서 두 번 통한 방법).
		/// ★ 이어하기가 쓰는 것과 **같은 문**(시계 되돌리기)으로 들어간다 — 다른 문을 새로 뚫으면
		///   검사만 통과하는 길이 생긴다.
		/// </summary>
		/// <summary> 1분당 강도 상승폭 — 검사가 「몇 초를 감아야 한 칸 오르나」를 역산한다(초 박기 금지). </summary>
		public float PressurePerMinute => stage != null ? stage.Rules.PressurePerMinute : 0f;

		/// <summary> 부서진 자리는 잊히지 않는다 — 다음 파도가 그쪽으로 끌린다. </summary>
		private readonly TowerDefenseBreach breach = new();

		/// <summary> 내가 낸 소리 — 자는 것을 깨운다. </summary>
		private readonly TowerDefenseNoise noise = new();

		/// <summary> 지금 판에서 가장 시끄러운 소리 — 화면·검사가 「규칙이 도나」를 볼 창. </summary>
		public float LoudestNoise => noise.LoudestLevel;

		/// <summary> 서식지가 깨어나는 소리 문턱 · 거리 — 검사가 값을 박지 않고 판에서 읽는다. </summary>
		public float NoiseWakeThreshold => stage != null ? stage.NoiseWakeThreshold : 0f;
		public float NoiseFromShotForVerification => stage != null ? stage.NoiseFromShot : 0f;

		/// <summary> 그 자리에서 들리는 소리 — 검사가 「둥지가 들을 만한가」를 직접 잰다. </summary>
		public float NoiseHeardAt(Vector3 worldPosition)
		{
			return stage != null ? noise.LevelAt(worldPosition, stage.NoiseHearingRadius) : 0f;
		}

		/// <summary>
		/// 소리를 낸다 — 짓기·사격·얻어맞기가 전부 이 문으로 들어온다.
		///
		/// ★ 문을 하나로 두는 이유: 소리를 내는 자리가 늘어날 때마다 합치는 거리·상한을 각자
		///   정하면, 어떤 소리는 자리를 스무 개 만들고 어떤 소리는 하나로 뭉친다. 규칙이 갈라진다.
		/// </summary>
		/// <summary> 한 발의 소리 — 무기가 부르는 통로. 값은 판 자산이 정한다(무기에 박지 X). </summary>
		private void ReportShotNoise(Vector3 worldPosition)
		{
			ShotsReported++;
			if (stage == null || stage.NoiseFromShot <= 0f)
				return;
			EmitNoise(worldPosition, stage.NoiseFromShot);
		}

		/// <summary>
		/// 「쏜 것을 알린」 횟수 — 검사가 「소리가 0 인 이유」를 가르는 유일한 창.
		/// 0 이면 통로가 안 불린 것(죽은 배선)이고, 0 이 아닌데 소리가 0 이면 값이나 잦아듦 문제다.
		/// 둘은 고치는 자리가 전혀 다른데 화면에는 똑같이 「조용함」으로 보인다.
		/// </summary>
		public int ShotsReported { get; private set; }

		public void EmitNoise(Vector3 worldPosition, float amount)
		{
			if (stage == null)
				return;
			noise.Emit(worldPosition, amount, stage.NoiseMergeDistance);
		}

		/// <summary>
		/// 흐른 시간만큼 마수를 단단하게 + 카드로 고른 감속을 건다.
		///
		/// ★ 왜 시간인가: 실시간에서 웨이브는 시계가 부른다 — 웨이브 수로 난이도를 올리면 플레이어가
		///   무엇을 하든 똑같이 오른다. 「빨리 정리했다」와 「겨우 버텼다」가 구분되지 않는다.
		///   시간으로 올리면 *오래 끌수록 아프다* 가 되어 둥지를 부수러 나갈 이유가 생긴다.
		/// ★ 상한을 두는 이유: 무한히 오르면 어느 순간부터는 무엇을 해도 지는 판이 된다 — 그건 난이도가
		///   아니라 타이머다.
		/// </summary>
		private void ApplyPressure(UnitObject enemyUnit)
		{
			if (enemyUnit == null || core == null)
				return;

			float pressure = core.Pressure;
			if (pressure > 1f)
			{
				int scaledHp = Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.HP_MAX] * pressure));
				enemyUnit.UnitStat[UnitStatType.HP_MAX] = scaledHp;
				enemyUnit.UnitStat[UnitStatType.HP_CUR] = scaledHp;
			}

			// 카드로 고른 「무거운 걸음」은 *앞으로 나오는* 마수에만 걸린다(이미 걷는 것을 늦추면
			// 고른 순간 판이 통째로 멎어 선택이 아니라 버튼이 된다).
			float speedMultiplier = boons.EnemySpeedMultiplier;
			if (speedMultiplier < 1f)
			{
				enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] =
					Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedMultiplier));
			}
		}

		/// <summary> 지금 마수에 걸린 압력 — 화면이 「점점 세진다」를 말한다. </summary>
		public float Pressure => core != null ? core.Pressure : 1f;

		/// <summary>
		/// 지금까지 내가 쓴 수단의 누적 — 세워둔 포탑들이 각자 센 것을 모은다.
		/// 「무엇을 많이 썼나」가 곧 마수가 무엇에 익숙해지는가다.
		/// </summary>
		public TowerDefenseAdaptationState Adaptation
		{
			get
			{
				if (stage == null)
					return default;

				int slowUses = 0;
				int splashHits = 0;
				int pierceHits = 0;
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit == null)
						continue;
					TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						continue;
					slowUses += weapon.SlowApplied;
					splashHits += weapon.SplashHits;
					pierceHits += weapon.PierceHits;
				}

				return TowerDefenseAdaptation.From(slowUses, splashHits, pierceHits, stage.AdaptationSensitivity);
			}
		}
	}
}
