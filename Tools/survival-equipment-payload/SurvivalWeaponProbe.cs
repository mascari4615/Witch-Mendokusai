using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	/// <summary>
	/// Play 중 장비 하나만 켜서 실제 적중을 재는 도구. 던전 안에서 호출.
	/// Begin(ID), 몇 초 대기, Report(). Report 는 한 줄 JSON.
	/// </summary>
	public static class SurvivalWeaponProbe
	{
		private static readonly HashSet<UnitHealth> subscribed = new();
		private static EquipmentData currentEquipment;
		private static GameObject currentObject;
		private static int hitCount;
		private static int damageSum;
		private static int killStart;
		private static int peakSkillObjects;
		private static int exceptionCount;
		private static int peakSlowed;
		private static double startTime;
		private static bool hooked;

		public static string Begin(int equipmentId)
		{
			if (Application.isPlaying == false)
				throw new InvalidOperationException("Play 중에만 호출");
			if (Application.persistentDataPath.EndsWith("WM Survival Verify", StringComparison.Ordinal) == false)
				throw new InvalidOperationException("격리 저장 경로 아님 " + Application.persistentDataPath);
			if (DungeonManager.Instance.IsDungeon == false)
				throw new InvalidOperationException("던전 밖");

			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			Clear();

			currentEquipment = SOHelper.Get<EquipmentData>(equipmentId);
			if (currentEquipment.Object != null)
			{
				currentObject = ObjectPoolManager.Instance.Spawn(currentEquipment.Object);
				if (currentObject.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(new SkillContext(PlayerProvider.Instance.CurrentObject, PlayerProvider.Instance, ObjectPoolManager.Instance));
				currentObject.SetActive(true);
			}

			Hook();
			return "begin " + currentEquipment.Name;
		}

		public static string SetStat(string statName, int value)
		{
			UnitStatType statType = (UnitStatType)Enum.Parse(typeof(UnitStatType), statName);
			PlayerProvider.Instance.Current.UnitStat[statType] = value;
			return statName + "=" + value;
		}

		public static string Report()
		{
			Sample();
			Spawner spawner = currentObject != null ? currentObject.GetComponent<Spawner>() : null;
			StringBuilder json = new();
			json.Append("{\"id\":").Append(currentEquipment.ID);
			json.Append(",\"name\":\"").Append(currentEquipment.Name).Append('"');
			json.Append(",\"seconds\":").Append((Time.timeAsDouble - startTime).ToString("0.0"));
			json.Append(",\"hits\":").Append(hitCount);
			json.Append(",\"damage\":").Append(damageSum);
			json.Append(",\"kills\":").Append(KillCount() - killStart);
			json.Append(",\"peakSkillObjects\":").Append(peakSkillObjects);
			json.Append(",\"peakSlowed\":").Append(peakSlowed);
			json.Append(",\"monsters\":").Append(ObjectBufferManager.GetObjects(ObjectType.Monster).Count);
			json.Append(",\"interval\":").Append(spawner != null ? spawner.CurrentInterval.ToString("0.000") : "null");
			json.Append(",\"spawnCount\":").Append(spawner != null ? spawner.SpawnCount : 0);
			json.Append(",\"exceptions\":").Append(exceptionCount);
			json.Append('}');
			return json.ToString();
		}

		private static int KillCount() => DataManager.Instance.DungeonStat[DungeonStatType.MONSTER_KILL];

		private static void Clear()
		{
			hitCount = 0;
			damageSum = 0;
			peakSkillObjects = 0;
			peakSlowed = 0;
			exceptionCount = 0;
			killStart = KillCount();
			startTime = Time.timeAsDouble;
		}

		private static void Hook()
		{
			if (hooked)
				return;
			hooked = true;
			Application.logMessageReceived += OnLog;
			EditorApplication.update += Sample;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.ExitingPlayMode)
				return;
			hooked = false;
			subscribed.Clear();
			Application.logMessageReceived -= OnLog;
			EditorApplication.update -= Sample;
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		}

		private static void OnLog(string condition, string stackTrace, LogType type)
		{
			if (type == LogType.Exception)
				exceptionCount++;
		}

		private static void Sample()
		{
			if (Application.isPlaying == false)
				return;

			peakSkillObjects = Mathf.Max(peakSkillObjects, ObjectBufferManager.GetObjects(ObjectType.Skill).Count);

			Player player = PlayerProvider.Instance.Current;
			player.UnitStat[UnitStatType.HP_CUR] = player.UnitStat[UnitStatType.HP_MAX];

			int slowed = 0;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
			{
				if (monsters[i].TryGetComponent(out UnitSlow slow) && slow.IsActive)
					slowed++;
				UnitHealth health = monsters[i].GetComponent<UnitHealth>();
				if (health != null && subscribed.Add(health))
					health.OnTakeDamage += OnMonsterDamaged;
			}
			peakSlowed = Mathf.Max(peakSlowed, slowed);
		}

		private static void OnMonsterDamaged(DamageInfo damageInfo)
		{
			hitCount++;
			damageSum += damageInfo.damage;
		}
	}
}
