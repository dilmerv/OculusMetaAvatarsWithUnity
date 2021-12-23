using Oculus.Avatar2;
using UnityEngine;

/// @file OvrAvatarGpuInterpolatedSkinningRenderable

namespace Oculus.Skinning.GpuSkinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using the Avatar SDK
     * and uses the GPU. It performs skinning on every avatar
     * but not at every frame. Instead, it interpolates between
     * frames, reducing the performance overhead of skinning
     * when there are lots of avatars. It is used when the skinning configuration
     * is set to SkinningConfig.OVR_UNITY_GPU_FULL and motion smoothing
     * is enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarGpuSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     */
    public class OvrAvatarGpuInterpolatedSkinnedRenderable : OvrAvatarGpuSkinnedRenderable
    {
        private CAPI.ovrAvatar2Transform _skinningOriginFrameZero;
        private CAPI.ovrAvatar2Transform _skinningOriginFrameOne;

        private MaterialPropertyBlock _matBlock;

        private bool _invertInterpolationValue = false;

        public IInterpolationValueProvider InterpolationValueProvider { get; set; }

        // 2 "output depth texels" per "atlas packer" slice to interpolate between
        // and enable bilinear filtering to have hardware to the interpolation
        // between depth texels for us
        protected override FilterMode SkinnerOutputFilterMode => FilterMode.Bilinear;
        protected override int SkinnerOutputDepthTexelsPerSlice => 2;

        protected override void Awake()
        {
            base.Awake();
            _matBlock = new MaterialPropertyBlock();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _matBlock = null;
        }

        public override void UpdateSkinningOrigin(CAPI.ovrAvatar2Transform skinningOrigin)
        {
            switch (SkinnerWriteDestination)
            {
                case SkinningOutputFrame.FrameZero:
                    _skinningOriginFrameZero = skinningOrigin;
                    break;
                case SkinningOutputFrame.FrameOne:
                    _skinningOriginFrameOne = skinningOrigin;
                    break;
            }
        }

        public override bool UpdateJointMatrices(CAPI.ovrAvatar2EntityId entityId, OvrAvatarPrimitive primitive,
            CAPI.ovrAvatar2PrimitiveRenderInstanceID primitiveInstanceId)
        {
            bool returnVal = base.UpdateJointMatrices(entityId, primitive, primitiveInstanceId);

            // ASSUMPTION: This is called before LateUpdate below so
            // the "fast forward the frame times" logic happens before updating interpolation
            // value between frames.
            // ASSUMPTION: This is called after UpdateSkinningOrigin above
            // so that the change of "skinning destination" does not affect that call
            SkinningOutputFrame nextDest = SkinningOutputFrame.FrameZero;
            switch (SkinnerWriteDestination)
            {
                case SkinningOutputFrame.FrameZero:
                    nextDest = SkinningOutputFrame.FrameOne;
                    _invertInterpolationValue = true;
                    break;

                case SkinningOutputFrame.FrameOne:
                    nextDest = SkinningOutputFrame.FrameZero;
                    _invertInterpolationValue = false;
                    break;
            }

            // Update the skinning write destination for next frame
            SkinnerWriteDestination = nextDest;
            return returnVal;
        }

        private void LateUpdate()
        {
            // TODO*: Go through some "UpdateInternal" type interface controlled via OvrAvatarEntity

            float lerpValue = 0.0f;

            // Maybe have a null guard in the setter rather than checking here
            // every frame?
            if (InterpolationValueProvider != null)
            {
                lerpValue = InterpolationValueProvider.GetRenderInterpolationValue();
            }

            // Convert from the 0 -> 1 interpolation value to one that "ping pongs" between
            // the slices here so that an additional GPU copy isn't needed to
            // transfer from "slice 1" to "slice 0"
            if (_invertInterpolationValue)
            {
                lerpValue = 1.0f - lerpValue;
            }

            // Update the depth texel value to interpolate between skinning output slices
            rendererComponent.GetPropertyBlock(_matBlock);

            _matBlock.SetFloat(U_ATTRIBUTE_TEXEL_SLICE_PROP_ID, SkinnerLayoutSlice + lerpValue);
            rendererComponent.SetPropertyBlock(_matBlock);

            // Update the "skinning origin" via lerp/slerp.
            // NOTE: This feels dirty as we are converting from `OvrAvatar2Vector3f/Quat` to Unity
            // versions just to do the lerp/slerp. Unnecessary conversions
            transform.localPosition = Vector3.Lerp(
                _skinningOriginFrameZero.position,
                _skinningOriginFrameOne.position,
                lerpValue);
            transform.localRotation = Quaternion.Slerp(
                _skinningOriginFrameZero.orientation,
                _skinningOriginFrameOne.orientation,
                lerpValue);
            transform.localScale = Vector3.Lerp(
                _skinningOriginFrameZero.scale,
                _skinningOriginFrameOne.scale,
                lerpValue);
        }
    }
}
