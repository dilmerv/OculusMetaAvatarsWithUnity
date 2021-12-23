using System;

/**
 * @file OvrAvatarTrackingBodyState.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Collects the position and orientation of the headset and
     * controllers, the controller button state,
     * the type of controller and tracking method,
     * whether the avatar is sitting or standing and
     * converts to and from C# and C++ native versions.
     * @see OvrAvatarBodyTrackingContextBase
     */
    public sealed class OvrAvatarTrackingBodyState
    {
        /// Position, orientation and scale of each controller.
        public CAPI.ovrAvatar2InputTrackingState inputTrackingState;

        /// Button and joystick state for each controller.
        public CAPI.ovrAvatar2InputControlState inputControlState;

        /// Type of hand tracking being performed.
        public readonly CAPI.ovrAvatar2HandInputType[] handInputType = new CAPI.ovrAvatar2HandInputType[(int)CAPI.ovrAvatar2Side.Count];

        /// Skeleton version number.
        public Int32 skeletonVersion;

        /// Avatar body modality (sitting vs standing).
        public CAPI.ovrAvatar2TrackingBodyModality bodyModality;

        /// Number of bones in the skeleton.
        public Int32 numBones;

        #region Native Conversions
        /**
         * Creates a native C++ pose from this C# pose.
         * @see CAPI.ovrAvatar2TrackingBodyState
         * @see FromNative
         */
        internal CAPI.ovrAvatar2TrackingBodyState ToNative()
        {
            return new CAPI.ovrAvatar2TrackingBodyState
            {
                inputTrackingState = inputTrackingState,
                inputControlState = inputControlState,
                skeletonVersion = skeletonVersion,
                bodyModality = bodyModality,
                leftHandInputType = handInputType[(int)CAPI.ovrAvatar2Side.Left],
                rightHandInputType = handInputType[(int)CAPI.ovrAvatar2Side.Right],
                numBones = numBones
            };
        }

        /**
         * Copies the native pose provided to this C# pose.
         * @see CAPI.ovrAvatar2TrackingBodyState
         * @see ToNative
         */
        internal void FromNative(ref CAPI.ovrAvatar2TrackingBodyState native)
        {
            inputTrackingState = native.inputTrackingState;
            inputControlState = native.inputControlState;
            skeletonVersion = native.skeletonVersion;
            bodyModality = native.bodyModality;
            handInputType[(int)CAPI.ovrAvatar2Side.Left] = native.leftHandInputType;
            handInputType[(int)CAPI.ovrAvatar2Side.Right] = native.rightHandInputType;
            numBones = native.numBones;

        }
        #endregion
    }
}
