using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oculus.Avatar2
{
    public interface IInterpolationValueProvider
    {
        // Will return a value between 0.0 and 1.0 (inclusive)
        float GetRenderInterpolationValue();
    }

    // Partial class intended to encapsulate "avatar animation" related functionality.
    // Mainly related to "morph targets" and "skinning"
    public partial class OvrAvatarEntity
    {
        private EntityAnimatorBase _entityAnimator;
        private IInterpolationValueProvider _interpolationValueProvider;

        #region Entity Animators
        private abstract class EntityAnimatorBase
        {
            protected readonly OvrAvatarEntity Entity;

            protected EntityAnimatorBase(OvrAvatarEntity entity)
            {
                Entity = entity;
            }

            public virtual void AddNewAnimationFrame(
                float timestamp,
                float deltaTime,
                in CAPI.ovrAvatar2Pose entityPose,
                in CAPI.ovrAvatar2EntityRenderState renderState)
            {
                // If a remote avatar is playing back streaming packet, update pose and morph targets.
                var isPlayingStream = !Entity.IsLocal && Entity.GetStreamingPlaybackState().HasValue;

                bool skeletalAnimation = isPlayingStream || Entity.HasAnyFeatures(UPDATE_POSE_FEATURES);
                if (skeletalAnimation)
                {
                    Entity.SamplePose(in entityPose, in renderState);
                }

                bool morphAnimation = isPlayingStream || Entity.HasAnyFeatures(UPDATE_MOPRHS_FEATURES);
                if (morphAnimation)
                {
                    Entity.SampleMorphTargets();
                }

                Entity.MonitorJoints(in entityPose);
            }

            public abstract void UpdateAnimationTime(float deltaTime);
        }

        private class EntityAnimatorMotionSmoothing : EntityAnimatorBase, IInterpolationValueProvider
        {
            // Currently using as a double buffering setup with only two frames, FrameA and FrameB
            // No pending frames are stored, as new frames come in before previous render frames are finished,
            // old frames are dropped
            private static readonly int NUM_RENDER_FRAMES = 2;

            private sealed class RenderFrameInfo
            {
                public float Timestamp { get; private set; } = 0.0f;
                public IReadOnlyList<OvrAvatarSkinnedRenderable> Renderables => _renderables;

                private OvrAvatarSkinnedRenderable[] _renderables = Array.Empty<OvrAvatarSkinnedRenderable>();

                public bool IsValid { get; private set; } = false;

                public void UpdateValues(float time, List<OvrAvatarSkinnedRenderable> frameRenderables)
                {
                    Timestamp = time;
                    IsValid = true;

                    if (_renderables.Length != frameRenderables.Count)
                    {
                        _renderables = frameRenderables.ToArray();
                    }
                    else
                    {
                        frameRenderables.CopyTo(_renderables);
                    }
                }
            }

            // In this implementation, no pending frames are held, the only "animation frames"
            // held on to are the frames that are going to be the two "render frames"
            private readonly RenderFrameInfo[] _renderFrameInfo = new RenderFrameInfo[NUM_RENDER_FRAMES];
            private float _currentRenderFrameTime;
            private int _nextRenderFrameIndex;

            private bool _hasTwoValidRenderFrames;
            private bool _allRenderablesHaveLastTwoFrames;

            private float _interpolationValue;

            private int EarliestRenderFrameIndex => _nextRenderFrameIndex;
            private int LatestRenderFrameIndex => 1 - _nextRenderFrameIndex;

            private readonly List<OvrAvatarSkinnedRenderable> visibleSkinnedRenderables_
                = new List<OvrAvatarSkinnedRenderable>();

            public EntityAnimatorMotionSmoothing(OvrAvatarEntity entity) : base(entity)
            {
                for (int i = 0; i < _renderFrameInfo.Length; i++) {
                    _renderFrameInfo[i] = new RenderFrameInfo();
                }
            }

            public float GetRenderInterpolationValue()
            {
                if (!_hasTwoValidRenderFrames)
                {
                    return 0.0f;
                }

                // ASSUMPTION: "AddNewAnimationFrame" is called any time the visible
                // renderers are changed.
                // GetRenderInterpolationValue() is called every
                // "RenderUpdate" but, _allRenderablesHaveLastTwoFrames is only updated
                // when a new render frame is update which only haves on "ActiveRender". If a
                // renderable becomes visible without having "AddNewAnimationFrame" called, then
                // this only boolean is insufficient.
                if (!_allRenderablesHaveLastTwoFrames)
                {
                    // Not all renderables have the last two render frame's worth of data,
                    // return 1.0 so that the renderables are rendering their
                    // latest (and only guaranteed valid) render frame
                    return 1.0f;
                }

                return _interpolationValue;
            }

            public override void AddNewAnimationFrame(
                float timestamp,
                float deltaTime,
                in CAPI.ovrAvatar2Pose entityPose,
                in CAPI.ovrAvatar2EntityRenderState renderState)
            {
                base.AddNewAnimationFrame(timestamp, deltaTime, entityPose, renderState);

                // For "motion smoothing" any OvrAvatarSkinnedRenderable subclass that is going to be rendered,
                // needs to be rendering in between the last two known "animation frames" to be
                // able to interpolate correctly. The "joint monitoring" however is done on a per entity
                // basis (the renderables all share a common single skeleton).
                // For both the joint monitor and the renderables to all have the same interpolation value,
                // they will all pull from the same source/get passed the same value instead of calculating
                // it themselves (which will also save computation).
                // Given these facts, there needs to be some coupling so that the calculation of the interpolation
                // value knows if all of the renderables being rendered in a given frame have the last two
                // "animation frames" worth of data. To figure that out, this will keep track of all
                // visible skinned renderables per "animation frame" and see if they are the same between
                // the last two frames
                visibleSkinnedRenderables_.Clear();
                foreach (var primRenderables in Entity._visiblePrimitiveRenderers)
                {
                    foreach (var primRenderable in primRenderables)
                    {
                        var skinnedRenderable = primRenderable.skinnedRenderable;
                        // TODO: Remove this expensive `GameObject.==` check
                        if (skinnedRenderable == null || !skinnedRenderable.isActiveAndEnabled) { continue; }

                        visibleSkinnedRenderables_.Add(skinnedRenderable);
                    }
                }

                AddNewAnimationFrameTime(timestamp, deltaTime, visibleSkinnedRenderables_);
            }

            public override void UpdateAnimationTime(float deltaTime)
            {
                AdvanceRenderingTimeIfPossible(deltaTime);
            }

            private void AddNewAnimationFrameTime(float timestamp, float deltaTime, List<OvrAvatarSkinnedRenderable> renderables)
            {
                // In this implementation, there are no historical/pending frames on top of the "render frames"
                // (the frames currently rendered/interpolated between).
                // Note the time of the frame to be added
                _renderFrameInfo[_nextRenderFrameIndex].UpdateValues(timestamp, renderables);

                // Advance/ping pong frame index
                _nextRenderFrameIndex =
                    1 - _nextRenderFrameIndex; // due to their only being 2 frames, this will ping pong

                if (!_hasTwoValidRenderFrames && _renderFrameInfo[1].IsValid)
                {
                    _hasTwoValidRenderFrames = true;
                }

                if (_hasTwoValidRenderFrames)
                {
                    // The entity has now has two render frames,
                    // check if all of the renderables in the two frames are the same.
                    // If so, then interpolation value can be calculated as normal, otherwise, it will
                    // be clamped to 1.0
                    var earliestFrame = _renderFrameInfo[EarliestRenderFrameIndex];

                    _allRenderablesHaveLastTwoFrames =
                        DoesListTwoContainAllOfListOne(earliestFrame.Renderables, _renderFrameInfo[LatestRenderFrameIndex].Renderables);

                    // Fast forward/rewind render frame time to be the earliest frame's timestamp minus the delta.
                    // This has two effects:
                    // 1) If the frame generation frequency changes to be faster (i.e. frames at 0, 1, 1.5),
                    //    then this logic "fast forwards" the render time which may cause a jump in animation, but
                    //    keeps the "interpolation window" (the time that fake animation data is generated) to
                    //    be the smallest possible.
                    // 2) If the frame generate frequency slows down (i.e. frames at 0, 0.5, 2), then this logic
                    //    "rewinds" the render time which will cause the animation to not skip any of the animation
                    //    window
                    _currentRenderFrameTime = earliestFrame.Timestamp - deltaTime;
                }
            }

            private static bool DoesListTwoContainAllOfListOne<T>(IReadOnlyList<T> listOne, IReadOnlyList<T> listTwo)
                where T : class
            {
                var listOneCount = listOne.Count;
                var listTwoCount = listTwo.Count;
                if (listOneCount > listTwoCount) { return false; }

                for(var idx1 = 0; idx1 < listOneCount; ++idx1 )
                {
                    if (!listTwo.Contains(listOne[idx1], listTwoCount)) { return false; }
                }
                return true;
            }

            private void AdvanceRenderingTimeIfPossible(float delta)
            {
                // Can only advance if there are 2 or more valid render frames
                if (!_hasTwoValidRenderFrames) return;

                float t0 = _renderFrameInfo[EarliestRenderFrameIndex].Timestamp;
                float t1 = _renderFrameInfo[LatestRenderFrameIndex].Timestamp;

                _currentRenderFrameTime += delta;

                // InverseLerp clamps to 0 to 1
                _interpolationValue = Mathf.InverseLerp(t0, t1, _currentRenderFrameTime);
            }
        }

        private class EntityAnimatorDefault : EntityAnimatorBase
        {
            public EntityAnimatorDefault(OvrAvatarEntity entity) : base(entity)
            {
            }

            public override void UpdateAnimationTime(float deltaTime)
            {
                // Intentionally empty
            }
        }

        #endregion

        #region Runtime

        private void SampleSkinningOrigin(in CAPI.ovrAvatar2PrimitiveRenderState primState, out CAPI.ovrAvatar2Transform skinningOrigin)
        {
            skinningOrigin = primState.skinningOrigin;
            // HACK: Mirror rendering transforms to fixup coordinate system errors
            skinningOrigin.scale.z *= -1f;

            skinningOrigin = skinningOrigin.ConvertSpace();
        }

        protected void SamplePose(in CAPI.ovrAvatar2Pose entityPose, in CAPI.ovrAvatar2EntityRenderState renderState)
        {
            for (uint i = 0; i < renderState.primitiveCount; ++i)
            {
                if (QueryPrimitiveRenderState_Direct(i, out var primState))
                {
                    SampleSkinningOrigin(in primState, out var skinningOrigin);

                    var primRenderables = _primitiveRenderables[primState.id];
                    foreach (var primRend in primRenderables)
                    {
                        var skinnedRenderable = primRend.skinnedRenderable;
                        if (skinnedRenderable is null)
                        {
                            // Non-skinned renderables just apply the transform
                            var t = primRend.renderable.transform;
                            t.ApplyOvrTransform(skinningOrigin);
                        }
                        else
                        {
                            // Otherwise call function on skinned renderable.
                            // Why does this needs to be called for all renderables
                            // but UpdateJointMatrices is only called on "visible renderers"?
                            // It would make sense if they were updated together
                            skinnedRenderable.UpdateSkinningOrigin(skinningOrigin);
                        }
                    }
                }
            }

            OvrAvatarLog.AssertConstMessage(entityPose.jointCount == SkeletonJointCount
                , "entity pose does not match skeleton.", logScope, this);

            // Are all SkinnedRenderables able to update without using Unity.Transform?
            bool needsFullTransformUpdate = false;
            foreach (var primRenderables in _visiblePrimitiveRenderers)
            {
                // TODO: This will result in redundant skinningMatrices query in UpdateJointMatrices
                foreach (var primRenderable in primRenderables)
                {
                    var skinnedRenderable = primRenderable.skinnedRenderable;
                    if (skinnedRenderable is null || !skinnedRenderable.isActiveAndEnabled) { continue; }

                    var primitive = primRenderable.primitive;
                    needsFullTransformUpdate |=
                        !skinnedRenderable.UpdateJointMatrices(entityId, primitive, primRenderable.instanceId);
                }
            }

            needsFullTransformUpdate |= (_debugDrawing.drawSkelHierarchy ||
                                         _debugDrawing.drawSkelHierarchyInGame ||
                                         _debugDrawing.drawSkinTransformsInGame);

            // If JointMonitoring is enabled, full hierarchy isn't created, so it can't be fully updated
            if (needsFullTransformUpdate && _jointMonitor == null)
            {
                for (uint i = 0; i < entityPose.jointCount; ++i)
                {
                    UpdateSkeletonTransformAtIndex(in entityPose, i);
                }
            }
            else
            {
                foreach (var skeletonIdx in _unityUpdateJointIndices)
                {
                    UpdateSkeletonTransformAtIndex(in entityPose, skeletonIdx);
                }
            }
        }

        private void UpdateSkeletonTransformAtIndex(in CAPI.ovrAvatar2Pose entityPose, uint skeletonIdx)
        {
            var jointUnityTx = GetSkeletonTxByIndex(skeletonIdx);

            unsafe
            {
                CAPI.ovrAvatar2Transform* jointTransform = entityPose.localTransforms + skeletonIdx;
                if ((*jointTransform).IsNan()) return;

                var jointParentIndex = entityPose.GetParentIndex(skeletonIdx);

                if (jointParentIndex != -1)
                {
                    jointUnityTx.ApplyOvrTransform(jointTransform);
                }
                else
                {
                    // HACK: Mirror rendering transforms across Z to fixup coordinate system errors
                    // Copy provided transform, we should not modify the source array
                    var flipScaleZ = *jointTransform;
                    flipScaleZ.scale.z = -flipScaleZ.scale.z;
                    jointUnityTx.ApplyOvrTransform(in flipScaleZ);
                }
            }
        }

        protected void SampleMorphTargets()
        {
            OvrAvatarLog.Assert(!IsLoading, logScope, this);

            // TODO: Should probably catch this earlier
            if (_skinnedRenderables.Count == 0) { return; }

            foreach (var primRenderables in _visiblePrimitiveRenderers)
            {
                foreach (var primRenderable in primRenderables)
                {
                    var skinnedRenderable = primRenderable.skinnedRenderable;
                    if (skinnedRenderable is null) { continue; }

                    var primitive = primRenderable.primitive;
                    if (primitive.morphTargetCount == 0) { continue; }

                    if (!skinnedRenderable.isActiveAndEnabled) { continue; }

                    var instanceId = primRenderable.instanceId;
                    UInt32 morphTargetCount = primitive.morphTargetCount;
                    using (var weightsBufferHandle = skinnedRenderable.CheckoutMorphTargetBuffer(morphTargetCount))
                    {
                        UInt32 bufferSize = sizeof(float) * morphTargetCount;
                        var result =
                            CAPI.ovrAvatar2Render_GetMorphTargetWeights(entityId, instanceId, weightsBufferHandle.BufferPtr, bufferSize);
                        if (result.IsSuccess())
                        {
                            skinnedRenderable.MorphTargetBufferUpdated(weightsBufferHandle);
                        }
                        else
                        {
                            OvrAvatarLog.LogError(
                                $"Error: GetMorphTargetWeights {result} for ID {primitive.assetId}, instance {primRenderable.instanceId}",
                                logScope);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
