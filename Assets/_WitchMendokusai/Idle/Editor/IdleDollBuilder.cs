using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WitchMendokusai.Idle;

namespace WitchMendokusai.Idle.Editor
{
	/// <summary>
	/// 아군 인형 프리팹을 코드로 짓는다. Yawn2 모델 + 휴머노이드 클립 5개 + 동작기
	///
	/// ★ 본편 Player 와 같은 재료 (Yawn2.fbx, Quaternius UAL CC0 클립). 컨트롤러는 따로.
	///   본편은 루트 모션, 여기는 시뮬이 자리를 정하니 루트 모션 없음
	/// ★ 클립은 Lab 원본을 복사해 둔다. 원본 임포트 설정(반복)을 건드리면 본편 Yawn 이 같이 바뀜
	/// </summary>
	public static class IdleDollBuilder
	{
		private const string MODEL_PATH = "Assets/_WitchMendokusai/Domain/NPC/Human/Yawn/Mesh/Ver2/Yawn2.fbx";
		private const string CLIP_SOURCE_PATH =
			"Assets/_WitchMendokusai/Lab/Animation/Universal Animation Library[Standard]/UAL1_Standard.fbx";
		private const string FOLDER = "Assets/_WitchMendokusai/Idle/Data/Assets/Doll";
		private const string CONTROLLER_PATH = FOLDER + "/AC_0001_IdleDoll.controller";
		private const string PREFAB_PATH = FOLDER + "/PF_0001_IdleDoll.prefab";
		private const string BATTLE_PRESENTATION_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/BP_0001_Idle.asset";
		private const string TAG = "[IdleDoll]";

		private const string IDLE_CLIP = "Armature|Idle_Loop";
		private const string MOVE_CLIP = "Armature|Jog_Fwd_Loop";
		private const string ATTACK_CLIP = "Armature|Spell_Simple_Shoot";
		private const string HIT_CLIP = "Armature|Hit_Chest";
		private const string DOWN_CLIP = "Armature|Death01";

		public static void Build()
		{
			GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);
			if (model == null)
			{
				Debug.LogError(TAG + " 모델이 없다: " + MODEL_PATH);
				return;
			}

			if (AssetDatabase.IsValidFolder(FOLDER) == false)
			{
				AssetDatabase.CreateFolder("Assets/_WitchMendokusai/Idle/Data/Assets", "Doll");
			}

			AnimationClip idle = CopyClip(IDLE_CLIP, true);
			AnimationClip move = CopyClip(MOVE_CLIP, true);
			AnimationClip attack = CopyClip(ATTACK_CLIP, false);
			AnimationClip hit = CopyClip(HIT_CLIP, false);
			AnimationClip down = CopyClip(DOWN_CLIP, false);
			if (idle == null || move == null || attack == null || hit == null || down == null)
			{
				return;
			}

			AnimatorController controller = BuildController(idle, move, attack, hit, down);
			GameObject prefab = BuildPrefab(model, controller);
			Wire(prefab);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			// 같은 호출에서 만든 컨트롤러를 가리키는 수정본은 첫 임포트에서 null 로 굳는다 (실측 2026-09-05). 한 번 더 임포트
			AssetDatabase.ImportAsset(PREFAB_PATH, ImportAssetOptions.ForceUpdate);
			Debug.Log(TAG + " 지음: " + PREFAB_PATH);
		}

		/// <summary>프리팹, 컨트롤러, 클립 5개, 무대 자산 연결까지 전부 있으면 참</summary>
		public static bool Verify()
		{
			List<string> missing = new List<string>();
			if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH) == null) { missing.Add(CONTROLLER_PATH); }
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (prefab == null) { missing.Add(PREFAB_PATH); }
			else if (prefab.GetComponentInChildren<IdleDollAnimator>() == null) { missing.Add("IdleDollAnimator"); }
			else if (prefab.GetComponentInChildren<Animator>() == null) { missing.Add("Animator"); }

			BattlePresentationSO presentation =
				AssetDatabase.LoadAssetAtPath<BattlePresentationSO>(BATTLE_PRESENTATION_PATH);
			if (presentation == null) { missing.Add(BATTLE_PRESENTATION_PATH); }
			else
			{
				SerializedObject serialized = new SerializedObject(presentation);
				if (serialized.FindProperty("dollPrefab").objectReferenceValue != prefab)
				{
					missing.Add("BattlePresentationSO.dollPrefab");
				}
			}

			if (missing.Count > 0)
			{
				Debug.LogError(TAG + " 빠짐: " + string.Join(", ", missing));
				return false;
			}

			return true;
		}

		private static AnimationClip CopyClip(string sourceName, bool loop)
		{
			AnimationClip source = null;
			foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(CLIP_SOURCE_PATH))
			{
				if (asset is AnimationClip clip && clip.name == sourceName)
				{
					source = clip;
					break;
				}
			}

			if (source == null)
			{
				Debug.LogError(TAG + " 클립이 없다: " + sourceName + " in " + CLIP_SOURCE_PATH);
				return null;
			}

			string shortName = sourceName.Substring(sourceName.IndexOf('|') + 1);
			string path = FOLDER + "/AN_" + shortName + ".anim";
			AnimationClip copy = Object.Instantiate(source);
			copy.name = shortName;
			AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
			settings.loopTime = loop;
			settings.loopBlend = loop;
			AnimationUtility.SetAnimationClipSettings(copy, settings);
			AssetDatabase.CreateAsset(copy, path);
			return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		}

		private static AnimatorController BuildController(
			AnimationClip idle, AnimationClip move, AnimationClip attack, AnimationClip hit, AnimationClip down)
		{
			AssetDatabase.DeleteAsset(CONTROLLER_PATH);
			AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
			controller.AddParameter("MOVE", AnimatorControllerParameterType.Bool);
			controller.AddParameter("DOWN", AnimatorControllerParameterType.Bool);
			controller.AddParameter("ATTACK", AnimatorControllerParameterType.Trigger);
			controller.AddParameter("HIT", AnimatorControllerParameterType.Trigger);

			AnimatorStateMachine machine = controller.layers[0].stateMachine;
			AnimatorState idleState = AddState(machine, "Idle", idle, new Vector3(300f, 0f, 0f));
			AnimatorState moveState = AddState(machine, "Move", move, new Vector3(300f, 120f, 0f));
			AnimatorState attackState = AddState(machine, "Attack", attack, new Vector3(600f, 0f, 0f));
			AnimatorState hitState = AddState(machine, "Hit", hit, new Vector3(600f, 120f, 0f));
			AnimatorState downState = AddState(machine, "Down", down, new Vector3(600f, 240f, 0f));
			machine.defaultState = idleState;

			Link(idleState.AddTransition(moveState), false, 0f, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "MOVE");
			Link(moveState.AddTransition(idleState), false, 0f, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "MOVE");

			// 공격과 피격은 어디서든 끼어들고 끝나면 대기로. 대기가 MOVE 를 보고 걷기로 이어감
			AnimatorStateTransition anyAttack = Link(machine.AddAnyStateTransition(attackState), false, 0f, 0.05f);
			anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "ATTACK");
			anyAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "DOWN");
			anyAttack.canTransitionToSelf = false;
			Link(attackState.AddTransition(idleState), true, 0.85f, 0.1f);

			AnimatorStateTransition anyHit = Link(machine.AddAnyStateTransition(hitState), false, 0f, 0.05f);
			anyHit.AddCondition(AnimatorConditionMode.If, 0f, "HIT");
			anyHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "DOWN");
			anyHit.canTransitionToSelf = false;
			Link(hitState.AddTransition(idleState), true, 0.8f, 0.1f);

			AnimatorStateTransition anyDown = Link(machine.AddAnyStateTransition(downState), false, 0f, 0.1f);
			anyDown.AddCondition(AnimatorConditionMode.If, 0f, "DOWN");
			anyDown.canTransitionToSelf = false;
			Link(downState.AddTransition(idleState), false, 0f, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "DOWN");

			EditorUtility.SetDirty(controller);
			return controller;
		}

		private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 at)
		{
			AnimatorState state = machine.AddState(name, at);
			state.motion = clip;
			return state;
		}

		private static AnimatorStateTransition Link(
			AnimatorStateTransition transition, bool hasExitTime, float exitTime, float duration)
		{
			transition.hasExitTime = hasExitTime;
			transition.exitTime = exitTime;
			transition.hasFixedDuration = true;
			transition.duration = duration;
			return transition;
		}

		private static GameObject BuildPrefab(GameObject modelAsset, AnimatorController controller)
		{
			GameObject root = new GameObject("IdleDoll");
			GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
			model.name = "Model";
			model.transform.SetParent(root.transform, false);

			Animator animator = model.GetComponent<Animator>();
			if (animator == null)
			{
				animator = model.AddComponent<Animator>();
			}

			animator.runtimeAnimatorController = controller;
			animator.applyRootMotion = false;
			// 에디트 모드 미리보기와 화면 밖 인형도 움직여야 사진과 어긋남이 없음
			animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

			IdleDollAnimator driver = root.AddComponent<IdleDollAnimator>();
			SerializedObject serialized = new SerializedObject(driver);
			serialized.FindProperty("animator").objectReferenceValue = animator;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);
			return prefab;
		}

		private static void Wire(GameObject prefab)
		{
			BattlePresentationSO presentation =
				AssetDatabase.LoadAssetAtPath<BattlePresentationSO>(BATTLE_PRESENTATION_PATH);
			if (presentation == null)
			{
				Debug.LogError(TAG + " 무대 자산이 없다: " + BATTLE_PRESENTATION_PATH);
				return;
			}

			SerializedObject serialized = new SerializedObject(presentation);
			serialized.FindProperty("dollPrefab").objectReferenceValue = prefab;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(presentation);
		}
	}
}
