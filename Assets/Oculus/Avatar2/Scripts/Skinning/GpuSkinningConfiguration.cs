// #define OVR_AVATAR_CLAMP_SKINNING_QUALITY

using System;

using Oculus.Skinning;
using Oculus.Skinning.GpuSkinning;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

#if UNITY_2019_3_OR_NEWER
using UnitySkinningQuality = UnityEngine.SkinWeights;
#else
using UnitySkinningQuality = UnityEngine.BlendWeights;
#endif

using SkinningConfig = Oculus.Avatar2.OvrAvatarEntity.SkinningConfig;

/// @file GpuSkinningConfiguration.cs

namespace Oculus.Avatar2
{
    // TODO: Convert to ScriptableObject
    ///
    /// Contains configuration options for GPU skinning.
    /// You can specify the skinning quality (number of bones per vertex),
    /// the numeric precision (float, half) and what type of skinning
    /// implementation to use.
    /// @see OvrAvatarSkinnedRenderable
    ///
    public class GpuSkinningConfiguration : OvrSingletonBehaviour<GpuSkinningConfiguration>
    {
        internal enum TexturePrecision { Float, Half, Unorm16, Snorm10, Byte, Nibble }

        internal static bool Initialized => hasInstance && !shuttingDown && _initComplete;

        /// Maximum allowed skinning quality (bones per vertex).
        // Helper to query Unity skinWeights/boneWeights configuration as SkinningQuality enum
        public OvrSkinningTypes.SkinningQuality MaxAllowedSkinningQuality
        {
            get
            {
#if OVR_AVATAR_CLAMP_SKINNING_QUALITY
                // It'd be nice to cache this, but it can be changed by script at runtime
#if UNITY_2019_3_OR_NEWER
                switch (QualitySettings.skinWeights)
#else // 2018
                switch (QualitySettings.blendWeights)
#endif
                {
                    case UnitySkinningQuality.OneBone:
                        return OvrSkinningTypes.SkinningQuality.Bone1;
                    case UnitySkinningQuality.TwoBones:
                        return OvrSkinningTypes.SkinningQuality.Bone2;
                    case UnitySkinningQuality.FourBones:
                        return OvrSkinningTypes.SkinningQuality.Bone4;
                }
                return OvrSkinningTypes.SkinningQuality.Invalid;
#else
                return OvrSkinningTypes.SkinningQuality.Bone4;
#endif
            }
        }

        /// Default skinning implementation to use.
        [SerializeField]
        [Tooltip("SkinningConfig used when OvrAvatarEntity is set to `DEFAULT`")]
        private SkinningConfig DefaultSkinningType = SkinningConfig.DEFAULT;

        /// Fallback skinning type (when skinning implementation is not specified).
        // Used if Entity does not specify and DefaultSkinningType == SkinningConfig.DEFAULT
        private const SkinningConfig FallbackSkinningType = SkinningConfig.UNITY;

        /// Skinning quality (bones per vertex) used for each level of detail.
        [SerializeField]
        [Tooltip("SkinningQuality (number of bone influences per vert) used for each LOD")]
        private OvrSkinningTypes.SkinningQuality[] QualityPerLOD = Array.Empty<OvrSkinningTypes.SkinningQuality>();

        /// Precision of source morph textures.
        [Header("Texture Settings (Advanced)")]
        [Tooltip("Morph texture should work well as snorm10")]
        [SerializeField]
        internal TexturePrecision SourceMorphFormat = TexturePrecision.Float;

        /// Precision of combined morph textures.
        [Tooltip("Morph texture should work well as half")]
        [SerializeField]
        internal TexturePrecision CombinedMorphFormat = TexturePrecision.Float;

        /// Precision of skinning output textures.
        [Tooltip("Skinner Output likely needs to be float")]
        [SerializeField]
        internal TexturePrecision SkinnerOutputFormat = TexturePrecision.Float;

        /// Scale used to calculate scale and bias used when using unorm formats.
        [Tooltip("Scale used to calculate scale and bias used when using unorm formats")]
        [SerializeField]
        internal float SkinnerUnormScale = 4.0f;

        /// Format of neutral pose texture.
        [SerializeField]
        readonly internal GraphicsFormat NeutralPoseFormat = GraphicsFormat.R32G32B32A32_SFloat;

        /// Format of joints texture.
        [SerializeField]
        readonly internal GraphicsFormat JointsFormat = GraphicsFormat.R32G32B32A32_SFloat;

        /// Format of indirection texture.
        [SerializeField]
        readonly internal GraphicsFormat IndirectionFormat = GraphicsFormat.R32G32B32A32_SFloat;

        [Header("Experimental Settings (Advanced, Unsupported)")]

        /// Enable motion smoothing for GPU skinning.
        [Tooltip("Smooths Motion Between Animation Updates but Introduces Latency. Ignored for Unity skinning types.")]
        [SerializeField]
        internal bool MotionSmoothing = false;

        [Header("Shader Settings (Advanced)")]

        // Assigned via editor
        // TODO: Remove suppresion of unused variable warning once auto-recompile is landed
#pragma warning disable CS0649
        [SerializeField]
        private Shader _CombineMorphTargetsShader;
        [SerializeField]
        private Shader _SkinToTextureShader;
#pragma warning restore CS0649 //  is never assigned to

        /// Shader to use for combining morph targets.
        public Shader CombineMorphTargetsShader => _CombineMorphTargetsShader;

        // Shader to use for skinning.
        public Shader SkinToTextureShader => _SkinToTextureShader;

        private static bool _initComplete = false;

        protected override void Initialize()
        {
            _initComplete = true;
        }

        protected override void Shutdown()
        {
            _initComplete = false;
        }

        internal static void HandleDefaultConfig(ref SkinningConfig config)
        {
            Debug.Assert(hasInstance);
            if (config == SkinningConfig.DEFAULT)
            {
                var defaultConfig = Instance.DefaultSkinningType;
                config = defaultConfig != SkinningConfig.DEFAULT ? defaultConfig : FallbackSkinningType;
            }
        }

        internal OvrSkinningTypes.SkinningQuality GetQualityForLOD(uint lodIndex)
        {
            Debug.Assert(lodIndex < QualityPerLOD.Length);
            var configValue = QualityPerLOD[lodIndex];
#if OVR_AVATAR_CLAMP_SKINNING_QUALITY
            configValue = (OvrSkinningTypes.SkinningQuality)Math.Min((int)configValue, (int)MaxAllowedSkinningQuality);
#endif
            return configValue;
        }

#if UNITY_EDITOR
        protected void OnValidate()
        {
            EnforceValidFormatMorph(ref SourceMorphFormat);
            EnforceValidFormat(ref CombinedMorphFormat);
            EnforceValidFormat(ref SkinnerOutputFormat);

            ValidateQualityPerLOD();
        }

        private void EnforceValidFormat(ref TexturePrecision precision)
        {
            if (precision == TexturePrecision.Byte || precision == TexturePrecision.Nibble || precision == TexturePrecision.Snorm10)
            {
                RevertFormat(ref precision, TexturePrecision.Half);
            }
        }

        private void EnforceValidFormatMorph(ref TexturePrecision precision)
        {
            if (precision == TexturePrecision.Byte || precision == TexturePrecision.Nibble || precision == TexturePrecision.Unorm16)
            {
                RevertFormat(ref precision, TexturePrecision.Snorm10);
            }
        }

        private void RevertFormat<T>(ref T textureFormat, T correctFormat) where T : Enum
        {
            OvrAvatarLog.LogError($"Unsupported format {textureFormat}, reverted to {correctFormat}");
            Debug.LogWarning("Bad GpuSkinningConfig setting", this);
            textureFormat = correctFormat;
        }

        private void ValidateQualityPerLOD()
        {
            int oldLength = QualityPerLOD?.Length ?? 0;
            for (int idx = 0; idx < oldLength; idx++)
            {
                ref var lod = ref QualityPerLOD[idx];
                // TODO: Does `Invalid` have any useful meaning as a configuration here?
                lod = (OvrSkinningTypes.SkinningQuality)
                    Mathf.Clamp((int)lod, (int)OvrSkinningTypes.SkinningQuality.Bone1, (int)OvrSkinningTypes.SkinningQuality.Bone4);
            }
            if (oldLength != CAPI.ovrAvatar2EntityLODFlagsCount)
            {
                Array.Resize(ref QualityPerLOD, (int)CAPI.ovrAvatar2EntityLODFlagsCount);

                var fillValue = oldLength > 0 && oldLength <= QualityPerLOD.Length ? QualityPerLOD[oldLength - 1] : OvrSkinningTypes.SkinningQuality.Bone4;
                for (int newIdx = oldLength; newIdx < CAPI.ovrAvatar2EntityLODFlagsCount; newIdx++)
                {
                    QualityPerLOD[newIdx] = fillValue;
                }
            }
        }
#endif
    }
} // namespace Oculus.Avatar
