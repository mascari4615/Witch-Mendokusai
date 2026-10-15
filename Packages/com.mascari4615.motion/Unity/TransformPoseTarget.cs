using System;
using UnityEngine;

namespace Motion.Unity
{
    public sealed class TransformPoseTarget : IPoseTarget
    {
        private readonly Func<string, Transform> resolve;

        public TransformPoseTarget(Func<string, Transform> resolver)
        {
            resolve = resolver ?? throw new ArgumentNullException("resolver");
        }

        public PoseValue Read(string nodeId)
        {
            Transform target = Resolve(nodeId);
            Vector3 position = target.localPosition;
            Quaternion rotation = target.localRotation;
            Vector3 scale = target.localScale;
            return new PoseValue(new System.Numerics.Vector3(position.x, position.y, -position.z),
                new System.Numerics.Quaternion(-rotation.x, -rotation.y, rotation.z, rotation.w),
                new System.Numerics.Vector3(scale.x, scale.y, scale.z));
        }

        public void Write(string nodeId, PoseValue pose)
        {
            Transform target = Resolve(nodeId);
            target.localPosition = new Vector3(pose.Position.X, pose.Position.Y, -pose.Position.Z);
            target.localRotation = new Quaternion(-pose.Rotation.X, -pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
            target.localScale = new Vector3(pose.Scale.X, pose.Scale.Y, pose.Scale.Z);
        }

        private Transform Resolve(string nodeId)
        {
            Transform target = resolve(nodeId);
            if (target == null)
            {
                throw new ArgumentException("Missing pose target: " + nodeId);
            }
            return target;
        }
    }
}
