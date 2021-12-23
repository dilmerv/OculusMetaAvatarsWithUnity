using System;
using UnityEngine;

/**
 * @file OvrAvatarTrackingSkeleton.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Collects the body tracking skeleton and its reference pose
     * and onverts to and from C# and C++ native versions.
     * @see OvrAvatarBodyTrackingContextBase
     */
    public ref struct OvrAvatarTrackingSkeleton
    {
        /// Vector giving the forward bone direction.
        public CAPI.ovrAvatar2Vector3f forwardDir;

        /// Indices for each bone and its corresponding parent bone.
        /// This describes the structure of the skeleton.
        public readonly OvrSpan<CAPI.ovrAvatar2Bone> bones;

        /// Reference pose for the skeleton. This is the pose it is
        /// in if no external transformations are applied.
        public OvrAvatarTrackingPose referencePose;

        /// Handle to native skeleton bone data.
        private readonly IntPtr _bonesPtr;

        /**
         * Constructs a C# skeleton from a native skeleton.
         * @param skeleton  native skeleton to copy.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         */
        internal OvrAvatarTrackingSkeleton(ref CAPI.ovrAvatar2TrackingBodySkeleton skeleton)
        {
            forwardDir = skeleton.forwardDir;
            referencePose = new OvrAvatarTrackingPose(ref skeleton.referencePose);
            unsafe
            {
                bones = new OvrSpan<CAPI.ovrAvatar2Bone>(skeleton.bones, (int)skeleton.numBones);
                _bonesPtr = new IntPtr(skeleton.bones);
            }
        }

        /**
         * Copy the skeleton from this C# instance to a native skeleton.
         * @param skeleton  native skeleton to update.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see GetNative
         * @see FromNative
         */
        internal void CopyToNative(ref CAPI.ovrAvatar2TrackingBodySkeleton native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                Debug.Assert(_bonesPtr.ToPointer() == native.bones);
            }
            native.forwardDir = forwardDir;
            referencePose.CopyToNative(ref native.referencePose);
        }

        /**
         * Create a native skeleton from this C# instance.
         * @returns native skeleton structure.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see CopyToNative
         * @see FromNative
         */
        internal CAPI.ovrAvatar2TrackingBodySkeleton GetNative()
        {
            unsafe
            {
                return new CAPI.ovrAvatar2TrackingBodySkeleton((CAPI.ovrAvatar2Bone*)_bonesPtr.ToPointer(),
                    (UInt32)bones.Length,
                    referencePose.GetNative())
                {
                    forwardDir = forwardDir
                };
            }
        }

        /**
         * Copy a native skeleton to this C# instance.
         * @param skeleton  native skeleton to copy.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see CopyToNative
         * @see GetNative
         */
        internal void CopyFromNative(ref CAPI.ovrAvatar2TrackingBodySkeleton native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                Debug.Assert(_bonesPtr.ToPointer() == native.bones);
            }
            forwardDir = native.forwardDir;
            referencePose.CopyFromNative(ref native.referencePose);
        }
    }
}
