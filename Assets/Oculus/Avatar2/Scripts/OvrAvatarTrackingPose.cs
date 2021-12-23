using System;
using UnityEngine;

/**
 * @file OvrAvatarTrackingPose.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Describes a contiguous array of structures.
     * Used to transfer data between C# and native.
     */
    public ref struct OvrSpan<T> where T : struct
    {
        private readonly IntPtr _ptr;
        private readonly int _length;

        public int Length => _length;

        public unsafe OvrSpan(void* ptr, int length)
        {
            _ptr = new IntPtr(ptr);
            _length = length;
        }
    }

    /**
     * Collects the position and orientation of each bone and
     * converts to and from C# and C++ native versions.
     * @see OvrAvatarBodyTrackingContextBase
     */
    public ref struct OvrAvatarTrackingPose
    {
        /// Coordinate space of this pose (local or object).
        public CAPI.ovrAvatar2Space space;

        /// Contiguous array of 4x4 bone transforms.
        public readonly OvrSpan<CAPI.ovrAvatar2Transform> bones;

        /// Handle to native bone data.
        private readonly IntPtr _bonesPtr;

        /**
         * Create a C# struct from a native pose.
         */
        internal OvrAvatarTrackingPose(ref CAPI.ovrAvatar2TrackingBodyPose pose)
        {
            space = pose.space;
            unsafe
            {
                bones = new OvrSpan<CAPI.ovrAvatar2Transform>(pose.bones, (int)pose.numBones);
                _bonesPtr = new IntPtr(pose.bones);
            }
        }

        /**
         * Copy the pose from this C# instance to the native pose provided.
         * @param native    where to store the native pose copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal void CopyToNative(ref CAPI.ovrAvatar2TrackingBodyPose native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                Debug.Assert(_bonesPtr.ToPointer() == native.bones);
            }
            native.space = space;
        }

        /**
         * Create a native pose from this C# instance.
         * @returns native pose copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal CAPI.ovrAvatar2TrackingBodyPose GetNative()
        {
            unsafe
            {
                return new CAPI.ovrAvatar2TrackingBodyPose((CAPI.ovrAvatar2Transform*)_bonesPtr.ToPointer(),
                    (UInt32)bones.Length)
                {
                    space = space
                };
            }
        }

        /**
         * Copy the native pose provided to this C# instance.
         * @param native   native pose to be copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal void CopyFromNative(ref CAPI.ovrAvatar2TrackingBodyPose native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                Debug.Assert(_bonesPtr.ToPointer() == native.bones);
            }

            space = native.space;
        }
    }
}
