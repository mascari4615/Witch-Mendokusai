using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class ChatManager : Singleton<ChatManager>
	{
		[SerializeField] private TextAsset chatScripts;
		private readonly Dictionary<string, List<LineData>> chatDataDic = new();

		public bool TryGetChatData(string eventName, out List<LineData> chatDatas)
		{
			if (chatDataDic.Count == 0)
				InitChatDic();

			return chatDataDic.TryGetValue(eventName, out chatDatas);
		}

		private void InitChatDic()
		{
			if (chatScripts.bytes[0] == 0xEF && chatScripts.bytes[1] == 0xBB && chatScripts.bytes[2] == 0xBF)
				Debug.Log("It's BOM");

			// var bytes = Encoding.GetEncoding(1252).GetBytes(chatScripts.text);
			// var myString = Encoding.UTF8.GetString(chatScripts.bytes);
			string myString = chatScripts.text;

			// Debug.Log(myString);

			string csvText = myString[..(chatScripts.text.Length - 1)];
			string[] rows = csvText.Split(new[] { '\n' });

			string eventName = string.Empty;
			List<LineData> lineDatas = new();

			for (int i = 1; i < rows.Length; i++)
			{
				string[] columns = rows[i].Split(',');

				if (columns[0] == "end")
				{
					chatDataDic.Add(eventName, lineDatas);
					eventName = string.Empty;
					lineDatas = new List<LineData>();
					continue;
				}

				if (columns[0] != string.Empty)
				{
					eventName = columns[0];
					lineDatas = new List<LineData>();
				}

				LineData chatData = new(ref columns);
				lineDatas.Add(chatData);
			}
		}
	}
}