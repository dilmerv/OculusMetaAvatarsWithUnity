using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        internal const string InternalLibFile = LibFile;
    }
}

namespace Oculus.Avatar2.External
{
    using EntityPtr = IntPtr;
    /* Pointer to pinned float[] */
    using FloatArrayPtr = IntPtr;
    using MixerLayerPtr = IntPtr;
    /* Pointer to pinned string[], [In] string[] is used instead */
    // using StringArrayPtr = IntPtr;

    using OvrAnimClipPtr = IntPtr;
    using OvrAnimHierarchyPtr = IntPtr;
    /* Pointer to pinned ovrAvatar2AnimationParameterId[]*/
    using ParameterIdArrayPtr = IntPtr;

#pragma warning disable CA1401 // P/Invokes should not be visible
#pragma warning disable IDE1006 // Naming Styles
    public partial class InternalCAPI
    {
        private const string LibFile = CAPI.InternalLibFile;
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_LoadAnimHierarchy(
    IntPtr data, UInt32 size, out CAPI.ovrAvatar2Id assetId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_UnloadAnimHierarchy(CAPI.ovrAvatar2Id assetId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetAnimHierarchy(
            CAPI.ovrAvatar2Id assetId,
            OvrAnimHierarchyPtr hierarchyAsset);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_LoadAnimClip(
            IntPtr data, UInt32 size, out CAPI.ovrAvatar2Id assetId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_UnloadAnimClip(CAPI.ovrAvatar2Id assetId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetAnimClip(
            CAPI.ovrAvatar2Id assetId, ref ovrAvatar2AnimClipAsset outClipAsset);

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2SampleAnimationClipParams
        {
            public float phase;
            public Int32 numJoints;
            public IntPtr jointTransformArray; // ovrAvatar2Transform[]
            public Int32 numFloats;
            public IntPtr floatChannels; // float[]
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_SampleAnimationClip(
            CAPI.ovrAvatar2Id clipAssetId,
            // TODO: Pass as `in` pointer
            ovrAvatar2SampleAnimationClipParams sampleParams);

        /// Assets

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_SetMood(
            CAPI.ovrAvatar2EntityId entityId, ovrAvatar2Mood desiredMood);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_GetMood(
            CAPI.ovrAvatar2EntityId entityId, out ovrAvatar2Mood currentMood);

        /// Loads an animation state machine definition asset from memory.
        /// \param the json data
        /// \param result id of the loaded asset
        /// \return result code
        ///
        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_LoadAnimStateMachineDefinitionFromJson(
            string json, out ovrAvatar2AnimationStateMachineDefinitionId outAssetId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_GetHierarchyId(CAPI.ovrAvatar2EntityId entityId, out ovrAvatar2AnimationHierarchyId outHierarchyId);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_CreateMask(
            ovrAvatar2AnimationId hierarchyAssetId,
            string name,
            [In] string[] includedJoints,
            int numJoints,
            [In] string[] includedFloats,
            int numFloats,
            out ovrAvatar2AnimationMaskId outAssetId);

        // Layers

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_SetLayerWeight(MixerLayerPtr mixerLayer, float weight);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_DestroyLayer(MixerLayerPtr mixerLayer);


        // State layer

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_GetParameterId(string paramName, out ovrAvatar2AnimationParameterId paramId);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_GetStateId(string stateName, out ovrAvatar2AnimationStateId paramId);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_GetTransitionId(string transitionName, out ovrAvatar2AnimationTransitionId paramId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_CreateStateLayer(
            CAPI.ovrAvatar2EntityId entityId,
            ovrAvatar2AnimationStateMachineDefinitionId stateMachineId,
            int priority,
            ovrAvatar2AnimationBlendMode blendMode,
            out MixerLayerPtr layer);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerSetFloatParameter(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationParameterId param, float value);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerSetFloatParameters(
            MixerLayerPtr mixerLayer, int numParams, ParameterIdArrayPtr paramIds, FloatArrayPtr values);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerSetNameParameter(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationParameterId param, string name);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerSetNameParameters(
            MixerLayerPtr mixerLayer, int numParams, ParameterIdArrayPtr paramIds, [In] string[] names);


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerRequestTransition(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationTransitionId transitionId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_StateLayerRequestFadeToState(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationTransitionId transitionId, float transitionTimeSec);


        /// Clip Layer

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_ClipLayerCreate(
            CAPI.ovrAvatar2EntityId entityId, int priority, out MixerLayerPtr newLayer);

        [DllImport(LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetClipByName(
            MixerLayerPtr mixerLayer, string animName);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetClipById(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationClipId animId);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetRate(
            MixerLayerPtr mixerLayer, float rate);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetPhase(
            MixerLayerPtr mixerLayer, float phase);

        /// Viseme Layer

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Animation_CreateVisemeLayer(
            CAPI.ovrAvatar2EntityId entityId,
            in ovrAvatar2AnimVisemeLayerParams visemeParams,
            int numVisemeAnims,
            [In] string[] visemeAnimNames,
            out MixerLayerPtr layer);


        /// Ik Layer
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result
           ovrAvatar2Animation_CreateIkLayer(
               CAPI.ovrAvatar2EntityId entityId,
               int priority,
               out MixerLayerPtr layer);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result
           ovrAvatar2Animation_CreateIkLayerFromParams(
                CAPI.ovrAvatar2EntityId entityId,
                int priority,
                in ovrAvatar2AnimationIkLayerParams parameters,
                out MixerLayerPtr layer);


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result
            ovrAvatar2Animation_IkLayerSetTargetWeight(
                MixerLayerPtr layer,
                ovrAvatar2AnimationIkTarget target,
                float weight,
                float timeSec);

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimVisemeLayerParams
        {
            int priority;
            ovrAvatar2AnimationVisemeFilterSettings filterSetiings;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimationVisemeFilterSettings
        {
            float saturationThreshold;
            float onsetSpeed;
            float falloffSpeed;
        }

        /// Ik Layer
        ///

        [StructLayout(LayoutKind.Sequential)]
        public struct OvrAvatarAnimationLimbConfig
        {
            public int upperLimb;
            public int lowerLimb;
            public int extremity;
            public int lowerLimbPartial;
            public int extremityPartial;

            public int numUpperLimbTwists;
            public IntPtr upperLimbTwists;
            public int numLowerLimbTwists;
            public IntPtr lowerLimbTwists;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimationIkLayerParams
        {
            public int headTracker;
            public int leftHandTracker;
            public int rightHandTracker;

            public int root;
            public int hips;
            public int chest;
            public int neck;
            public int head;

            public int numSpineJoints;
            public IntPtr spineJoints;

            public int rightShoulder;
            public int leftShoulder;

            public OvrAvatarAnimationLimbConfig leftArm;
            public OvrAvatarAnimationLimbConfig rightArm;
            public OvrAvatarAnimationLimbConfig leftLeg;
            public OvrAvatarAnimationLimbConfig rightLeg;
        }

        public enum ovrAvatar2AnimationStateMachineDefinitionId : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2AnimationMaskId : Int32
        {
            Invalid = 0,
        }
        public enum ovrAvatar2AnimationClipId : Int32
        {
            Invalid = 0,
        }

        // TODO: The size of ovrAvatar2AnimationBlendMode is not explicitly specified
        public enum ovrAvatar2AnimationBlendMode
        {
            Override = 0,
            Additive,
        }

        // TODO: The size of ovrAvatar2AnimationIkTarget is not explicitly specified
        public enum ovrAvatar2AnimationIkTarget
        {
            Head = 0,
            LeftHand = 1,
            RightHand = 2,
            Count
        }

        // TODO: The size is not explicitly specified
        public enum ovrAvatar2Mood
        {
            Invalid = -1,
            Neutral = 0,
            Like,
            VeryLike,
            Happy,
            Confused,
            VeryConfused,
            Dislike,
            VeryDislike,
            Unhappy,

            Count
        }

        // typedefs (effectively)
        public enum ovrAvatar2AnimationId : UInt64 { }
        public enum ovrAvatar2AnimationHierarchyId : UInt64 { }
        public enum ovrAvatar2AnimationParameterId : UInt64 { }
        public enum ovrAvatar2AnimationTransitionId : UInt64 { }
        public enum ovrAvatar2AnimationStateId : UInt64 { }

    }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1401 // P/Invokes should not be visible
}
