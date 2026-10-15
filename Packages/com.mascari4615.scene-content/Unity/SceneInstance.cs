using System.Collections.Generic;
using UnityEngine;

namespace SceneContent.Unity
{
    public sealed class SceneInstance : MonoBehaviour
    {
        [SerializeField] private List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        [SerializeField] private string[] nodeIds;
        [SerializeField] private Transform[] nodeTransforms;
        private Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();

        public Transform FindNode(string id)
        {
            if (nodes.Count == 0 && nodeIds != null)
                for (int index = 0; index < nodeIds.Length; index++) nodes.Add(nodeIds[index], nodeTransforms[index]);
            return nodes[id];
        }

        internal void Initialize(Dictionary<string, Transform> values)
        {
            nodes = values;
            nodeIds = new string[values.Count];
            nodeTransforms = new Transform[values.Count];
            int index = 0;
            foreach (KeyValuePair<string, Transform> value in values)
            {
                nodeIds[index] = value.Key;
                nodeTransforms[index] = value.Value;
                index++;
            }
        }
        internal void Own(UnityEngine.Object asset) { assets.Add(asset); }

        internal void DisposeAssets()
        {
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
            assets.Clear();
        }

        private void OnDestroy() { DisposeAssets(); }
    }
}
