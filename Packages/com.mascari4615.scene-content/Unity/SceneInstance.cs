using System.Collections.Generic;
using UnityEngine;

namespace SceneContent.Unity
{
    public sealed class SceneInstance : MonoBehaviour
    {
        [SerializeField] private List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        [SerializeField] private string[] nodeIds;
        [SerializeField] private Transform[] nodeTransforms;
        [SerializeField] private Renderer[] meshRenderers;
        [SerializeField] private Quaternion[] initialRotations;
        [SerializeField] private Quaternion[] sourceRotations;
        private Dictionary<string, int> nodes = new Dictionary<string, int>();

        public Transform FindNode(string id)
        {
            if (nodes.Count == 0 && nodeIds != null)
                for (int index = 0; index < nodeIds.Length; index++) nodes.Add(nodeIds[index], index);
            return nodeTransforms[nodes[id]];
        }

        internal Quaternion CaptureRotation(string id)
        {
            Quaternion current = FindNode(id).localRotation;
            int index = nodes[id];
            Quaternion initial = initialRotations[index];
            return current.x == initial.x && current.y == initial.y && current.z == initial.z && current.w == initial.w
                ? sourceRotations[index] : current;
        }

        public Renderer FindMesh(int index)
        {
            return meshRenderers[index];
        }

        internal void Initialize(Dictionary<string, Transform> values, Renderer[] renderers, SceneDocument document)
        {
            meshRenderers = renderers;
            nodes.Clear();
            nodeIds = new string[values.Count];
            nodeTransforms = new Transform[values.Count];
            initialRotations = new Quaternion[values.Count];
            sourceRotations = new Quaternion[values.Count];
            for (int index = 0; index < document.Nodes.Length; index++)
            {
                SceneNode node = document.Nodes[index];
                nodes.Add(node.Id, index);
                nodeIds[index] = node.Id;
                nodeTransforms[index] = values[node.Id];
                initialRotations[index] = nodeTransforms[index].localRotation;
                sourceRotations[index] = new Quaternion(-node.Rotation[0], -node.Rotation[1], node.Rotation[2], node.Rotation[3]);
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
