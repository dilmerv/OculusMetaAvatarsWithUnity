using System;
using System.Runtime.InteropServices;

using Oculus.Avatar2;

using UnityEngine;

/// @file OvrAvatarUnitySkinnedRenderable.cs

namespace Oculus.Skinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using Unity.
     * It is used when the skinning configuration is set
     * to *SkinningConfig.UNITY*.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarGpuSkinnedRenderable
     * @see OvrAvatarEntity.SkinningConfig
     */
    public class OvrAvatarUnitySkinnedRenderable : OvrAvatarSkinnedRenderable
    {
        private SkinnedMeshRenderer _skinnedRenderer;

        [SerializeField]
        [Tooltip("Configuration to override SkinQuality, otherwise indicates which Quality was selected for this LOD")]
        private SkinQuality _skinQuality = SkinQuality.Auto;

        public SkinQuality SkinQuality
        {
            get => _skinQuality;
            set
            {
                if (_skinQuality != value)
                {
                    _skinQuality = value;
                    UpdateSkinQuality();
                }
            }
        }
        private void UpdateSkinQuality()
        {
            if (_skinnedRenderer != null)
            {
                _skinnedRenderer.quality = _skinQuality;
            }
        }

        private uint _morphCount;

        private float[] _morphBuffer;
        private float[] MorphBuffer
        {
            get
            {
                return _morphBuffer ??
                   (_morphBuffer = _morphCount > 0
                        ? new float[(int)_morphCount]
                        : Array.Empty<float>());
            }
        }
        protected void OnDisable()
        {
            _morphBuffer = null;

            _bufferHandle.Dispose();
        }

        protected override void AddDefaultRenderer()
        {
            _skinnedRenderer = AddRenderer<SkinnedMeshRenderer>();
        }

        public override void ApplyMeshPrimitive(OvrAvatarPrimitive primitive)
        {
            base.ApplyMeshPrimitive(primitive);

            _skinnedRenderer.sharedMesh = _mesh;

            _skinQuality = _skinQuality == SkinQuality.Auto ? QualityForLODIndex(primitive.HighestQualityLODIndex) : _skinQuality;
            _skinnedRenderer.quality = _skinQuality;

            _morphCount = primitive.morphTargetCount;
        }

        public override void ApplySkeleton(Transform[] bones)
        {
            if (_skinnedRenderer.sharedMesh)
            {
                _skinnedRenderer.rootBone = transform;
                _skinnedRenderer.bones = bones;
            }
            else
            {
                OvrAvatarLog.LogError("Had no shared mesh to apply skeleton to!");
            }
        }

        public override IDisposableBuffer CheckoutMorphTargetBuffer(uint morphCount)
        {
            _bufferHandle.SetMorphBuffer(MorphBuffer);
            return _bufferHandle;
        }

        public override void MorphTargetBufferUpdated(IDisposableBuffer buffer)
        {
            Debug.Assert(_bufferHandle.BufferPtr == buffer.BufferPtr);
            for (int morphTargetIndex = 0; morphTargetIndex < _morphBuffer.Length; ++morphTargetIndex)
            {
                _skinnedRenderer.SetBlendShapeWeight(morphTargetIndex, _morphBuffer[morphTargetIndex]);
            }
        }

        public override bool UpdateJointMatrices(CAPI.ovrAvatar2EntityId entityId, OvrAvatarPrimitive primitive, CAPI.ovrAvatar2PrimitiveRenderInstanceID primitiveInstanceId)
        {
            // No-op
            // TODO: Update transforms here
            return false;
        }

        private static SkinQuality QualityForLODIndex(uint lodIndex)
        {
            return OvrAvatarManager.Instance.GetUnitySkinQualityForLODIndex(lodIndex);
        }

        // TODO: This is disposed via the `Cleanup` method
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly BufferHandle _bufferHandle = new BufferHandle();
#pragma warning restore CA2213 // Disposable fields should be disposed

        private class BufferHandle : IDisposableBuffer
        {
            public BufferHandle() { }

            private GCHandle _morphHandle;

            public void SetMorphBuffer(float[] buffer)
            {
                Debug.Assert(!_morphHandle.IsAllocated);

                _morphHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            }

            public IntPtr BufferPtr
            {
                get
                {
                    return _morphHandle.AddrOfPinnedObject();
                }
            }

            public void Dispose()
            {
                if (_morphHandle.IsAllocated)
                {
                    _morphHandle.Free();
                }
            }
        }

#if UNITY_EDITOR
        protected void OnValidate()
        {
            UpdateSkinQuality();
        }
#endif
    }
}
