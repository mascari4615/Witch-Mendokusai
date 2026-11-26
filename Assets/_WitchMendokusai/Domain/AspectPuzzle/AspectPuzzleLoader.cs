using Newtonsoft.Json;
using Newtonsoft.Json.Utilities;
using UnityEngine;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>AspectPuzzles.json 읽기. 자료 타입의 가지치기 보존은 Core/link.xml</summary>
	public static class AspectPuzzleLoader
	{
		static AspectPuzzleLoader()
		{
			// 반사로 생성하는 목록의 IL2CPP 제네릭 코드 생성 보장 (SkillTreeParser 와 같은 까닭)
			AotHelper.EnsureList<AspectRecipe>();
			AotHelper.EnsureList<HexSpot>();
			AotHelper.EnsureList<HexAspect>();
			AotHelper.EnsureList<HexPaperData>();
			AotHelper.EnsureList<TurnCell>();
			AotHelper.EnsureList<TurnDoll>();
			AotHelper.EnsureList<TurnRun>();
			AotHelper.EnsureList<TurnBoardData>();
			AotHelper.EnsureList<int>();
		}

		public static AspectPuzzleBook Load(TextAsset asset) => JsonConvert.DeserializeObject<AspectPuzzleBook>(asset.text);
	}
}
