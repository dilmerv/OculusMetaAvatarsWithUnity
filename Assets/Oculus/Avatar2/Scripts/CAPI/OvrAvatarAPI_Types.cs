using System;
using System.Runtime.InteropServices;
using UnityEngine;

/**
 * @file OvrAvatarAPI_Types.cs
 */
namespace Oculus.Avatar2
{
    /**
     * @class CAPI
     * Encapsulates C# entry points for Avatar SDK native implementation.
     */
    public partial class CAPI
    {
        //-----------------------------------------------------------------
        //
        // Opaque ID types
        //
        //

        public enum ovrAvatar2EntityId : Int32
        {
            Invalid = 0
        }

        public enum ovrAvatar2RequestId : Int32
        {
            Invalid = 0
        }

        // TODO: This is Int32 in native
        public enum ovrAvatar2Id : UInt32
        {
            Invalid = 0
        }

        public enum ovrAvatar2VertexBufferId : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2MorphTargetBufferId : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2NodeId : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2LoadRequestId : Int32
        {
            Invalid = 0,
        }

        //-----------------------------------------------------------------
        //
        // Opaque version types
        //
        //

        public enum ovrAvatar2HierarchyVersion : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2EntityRenderStateVersion : Int32
        {
            Invalid = 0,
        }


        //-----------------------------------------------------------------
        //
        // Flags
        //
        //

        /**
         * Configures avatar level of detail.
         * One or more flags may be set.
         *
         * @see ovrAvatar2EntityCreateInfo
         */
        [Flags]
        [System.Serializable]
        public enum ovrAvatar2EntityLODFlags : Int32
        {
            /// level of detail 0 (highest fidelity)
            LOD_0 = 1 << 0,

            /// level of detail 1
            LOD_1 = 1 << 1,

            /// level of detail 2
            LOD_2 = 1 << 2,

            /// level of detail 3
            LOD_3 = 1 << 3,

            /// level of detail 4 (lowest level)
            LOD_4 = 1 << 4,

            /// All levels of detail
            All = LOD_0 | LOD_1 | LOD_2 | LOD_3 | LOD_4,
        }
        public const uint ovrAvatar2EntityLODFlagsCount = 5;

        /**
         * Configures how the avatar is manifested
         * (full body, head and hands only).
         * NOTE: Only Half is currently available
         *
         * @see ovrAvatar2EntityCreateInfo
         */
        [Flags]
        [System.Serializable]
        public enum ovrAvatar2EntityManifestationFlags : Int32
        {
            /// No avatar parts manifested
            None = 0,

            /// All body parts
            Full = 1 << 0,

            /// Upper body only
            Half = 1 << 1,

            /// Head and hands only
            HeadHands = 1 << 2,

            /// Head only
            Head = 1 << 3,

            /// Hands only
            Hands = 1 << 4,

            ///  All manifestations requested.
            All = Full | Half | HeadHands | Head | Hands,
        }

        /**
         * Configures how the avatar is viewed
         * (first person, third person).
         *
         * @see ovrAvatar2EntityCreateInfo
         */
        [Flags]
        [System.Serializable]
        public enum ovrAvatar2EntityViewFlags : Int32
        {
            None = 0,

            /// First person view
            FirstPerson = 1 << 0,

            /// Third person view
            ThirdPerson = 1 << 1,

            /// All views
            All = FirstPerson | ThirdPerson
        }

        //-----------------------------------------------------------------
        //
        // Math
        //
        //

        /// 2D Vector Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Vector2f
        {
            public float x;
            public float y;

            public static implicit operator ovrAvatar2Vector2f(Vector2 v)
            {
                ovrAvatar2Vector2f result = new ovrAvatar2Vector2f();
                result.x = v.x;
                result.y = v.y;
                return result;
            }

            public static implicit operator Vector2(ovrAvatar2Vector2f v)
            {
                return new Vector3(v.x, v.y);
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct ovrAvatar2Vector2u
        {
            public readonly UInt32 x;
            public readonly UInt32 y;

            public ovrAvatar2Vector2u(UInt32 X, UInt32 Y) { x = X; y = Y; }
        };

        /// 3D Vector Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Vector3f
        {
            public float x;
            public float y;
            public float z;

            public float Length => Mathf.Sqrt(LengthSquared);
            public float LengthSquared => (x * x + y * y + z * z);

            public static implicit operator ovrAvatar2Vector3f(in Vector3 v)
            {
                ovrAvatar2Vector3f result = new ovrAvatar2Vector3f();
                result.x = v.x;
                result.y = v.y;
                result.z = v.z;
                return result;
            }

            public static implicit operator Vector3(in ovrAvatar2Vector3f v)
            {
                return new Vector3(v.x, v.y, v.z);
            }

            public static ovrAvatar2Vector3f operator +(in ovrAvatar2Vector3f lhs, in ovrAvatar2Vector3f rhs)
            {
                return new ovrAvatar2Vector3f
                {
                    x = lhs.x + rhs.x,
                    y = lhs.y + rhs.y,
                    z = lhs.z + rhs.z
                };
            }
            public static ovrAvatar2Vector3f operator -(in ovrAvatar2Vector3f lhs, in ovrAvatar2Vector3f rhs)
            {
                return new ovrAvatar2Vector3f
                {
                    x = lhs.x - rhs.x,
                    y = lhs.y - rhs.y,
                    z = lhs.z - rhs.z
                };
            }

            public static ovrAvatar2Vector3f operator *(in ovrAvatar2Vector3f vec, float scale)
            {
                return new ovrAvatar2Vector3f
                {
                    x = vec.x * scale,
                    y = vec.y * scale,
                    z = vec.z * scale
                };
            }
            public static ovrAvatar2Vector3f operator /(in ovrAvatar2Vector3f numer, float denom)
            {
                return numer * (1.0f / denom);
            }
        };

        /// 4D Vector Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Vector4f
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public static implicit operator ovrAvatar2Vector4f(Vector4 v)
            {
                ovrAvatar2Vector4f result = new ovrAvatar2Vector4f();
                result.x = v.x;
                result.y = v.y;
                result.z = v.z;
                result.w = v.w;
                return result;
            }

            public static implicit operator Vector4(ovrAvatar2Vector4f v)
            {
                return new Vector4(v.x, v.y, v.z, v.w);
            }

            public static implicit operator Color(ovrAvatar2Vector4f v)
            {
                return new Color(v.x, v.y, v.z, v.w);
            }
        };

        /// 4D Vector Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Vector4ub
        {
            public Byte x;
            public Byte y;
            public Byte z;
            public Byte w;
        };

        /// 4D Vector Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Vector4us
        {
            public UInt16 x;
            public UInt16 y;
            public UInt16 z;
            public UInt16 w;
        };

        /// Quaternion Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Quatf
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public static implicit operator ovrAvatar2Quatf(in Quaternion q)
            {
                ovrAvatar2Quatf result = new ovrAvatar2Quatf();
                result.x = q.x;
                result.y = q.y;
                result.z = q.z;
                result.w = q.w;
                return result;
            }

            public static implicit operator Quaternion(in ovrAvatar2Quatf q)
            {
                return new Quaternion(q.x, q.y, q.z, q.w);
            }
        };


        /// Transform Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Transform
        {
            public ovrAvatar2Vector3f position;
            public ovrAvatar2Quatf orientation;
            public ovrAvatar2Vector3f scale;

            public ovrAvatar2Transform(ovrAvatar2Vector3f position, ovrAvatar2Quatf orientation)
            {
                this.position = position;
                this.orientation = orientation;
                this.scale.x = 1f;
                this.scale.y = 1f;
                this.scale.z = 1f;
            }

            public ovrAvatar2Transform(ovrAvatar2Vector3f position, ovrAvatar2Quatf orientation, ovrAvatar2Vector3f scale)
            {
                this.position = position;
                this.orientation = orientation;
                this.scale = scale;
            }

            public static implicit operator ovrAvatar2Transform(Transform t)
            {
                ovrAvatar2Transform result = new ovrAvatar2Transform();
                result.position = t.localPosition;
                result.orientation = t.localRotation;
                result.scale = t.localScale;
                return result;
            }
        };

        // Matrix Type
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Matrix4f
        {
            private float m00, m10, m20, m30;
            private float m01, m11, m21, m31;
            private float m02, m12, m22, m32;
            private float m03, m13, m23, m33;

            public float this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0:
                            return this.m00;
                        case 1:
                            return this.m10;
                        case 2:
                            return this.m20;
                        case 3:
                            return this.m30;
                        case 4:
                            return this.m01;
                        case 5:
                            return this.m11;
                        case 6:
                            return this.m21;
                        case 7:
                            return this.m31;
                        case 8:
                            return this.m02;
                        case 9:
                            return this.m12;
                        case 10:
                            return this.m22;
                        case 11:
                            return this.m32;
                        case 12:
                            return this.m03;
                        case 13:
                            return this.m13;
                        case 14:
                            return this.m23;
                        case 15:
                            return this.m33;
                        default:
                            throw new IndexOutOfRangeException("Invalid matrix index!");
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0:
                            this.m00 = value;
                            break;
                        case 1:
                            this.m10 = value;
                            break;
                        case 2:
                            this.m20 = value;
                            break;
                        case 3:
                            this.m30 = value;
                            break;
                        case 4:
                            this.m01 = value;
                            break;
                        case 5:
                            this.m11 = value;
                            break;
                        case 6:
                            this.m21 = value;
                            break;
                        case 7:
                            this.m31 = value;
                            break;
                        case 8:
                            this.m02 = value;
                            break;
                        case 9:
                            this.m12 = value;
                            break;
                        case 10:
                            this.m22 = value;
                            break;
                        case 11:
                            this.m32 = value;
                            break;
                        case 12:
                            this.m03 = value;
                            break;
                        case 13:
                            this.m13 = value;
                            break;
                        case 14:
                            this.m23 = value;
                            break;
                        case 15:
                            this.m33 = value;
                            break;
                        default:
                            throw new IndexOutOfRangeException("Invalid matrix index!");
                    }
                }
            }

            public static implicit operator ovrAvatar2Matrix4f(Matrix4x4 m)
            {
                var matrix = new ovrAvatar2Matrix4f();

                // Unity and sdk both use column major matrices
                for (int i = 0; i < 16; ++i)
                {
                    matrix[i] = m[i];
                }

                return matrix;
            }

            public static implicit operator Matrix4x4(ovrAvatar2Matrix4f m)
            {
                var matrix = new Matrix4x4();

                // Unity and sdk both use column major matrices
                for (int i = 0; i < 16; ++i)
                {
                    matrix[i] = m[i];
                }

                return matrix;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly unsafe ref struct ovrAvatar2Pose
        {
            public readonly UInt32 jointCount;
            public readonly ovrAvatar2Transform* localTransforms; // Array of ovrAvatar2Transforms
            public readonly ovrAvatar2Transform* objectTransforms; // Array of ovrAvatar2Transforms relative to root
            private readonly Int32* parents; // Array of Int32
            private readonly ovrAvatar2NodeId* nodeIds; ///< Array of node ids

            public Int32 GetParentIndex(Int32 childIndex)
            {
                if (childIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        $"Index {childIndex} is out of range of pose parent array of size {jointCount}");
                }
                return GetParentIndex((UInt32)childIndex);
            }

            public Int32 GetParentIndex(UInt32 childIndex)
            {
                if (childIndex >= jointCount)
                {
                    throw new ArgumentOutOfRangeException(
                        $"Index {childIndex} is over range of pose parent array of size {jointCount}");
                }
                if (parents == null)
                {
                    throw new NullReferenceException("parents array is null");
                }
                unsafe { return parents[childIndex]; }
            }

            public ovrAvatar2NodeId GetNodeIdAtIndex(Int32 index)
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        $"Index {index} is out of range of pose nodeIds array of size {jointCount}");
                }
                return GetNodeIdAtIndex((UInt32)index);
            }
            public ovrAvatar2NodeId GetNodeIdAtIndex(UInt32 index)
            {
                if (index >= jointCount)
                {
                    throw new ArgumentOutOfRangeException(
                        $"Index {index} is over range of pose nodeIds array of size {jointCount}");
                }
                if (nodeIds == null)
                {
                    throw new NullReferenceException("nodeIds array is null");
                }
                Debug.Assert(index < jointCount);
                unsafe { return nodeIds[index]; }
            }
        };
        public enum ovrAvatar2Side : Int32
        {
            Left = 0,
            Right = 1,
            Count = 2
        }

        //-----------------------------------------------------------------
        //
        // Results
        //
        //

        public enum ovrAvatar2Result : Int32
        {
            Success = 0,
            Unknown = 1,
            OutOfMemory = 2,
            NotInitialized = 3,
            AlreadyInitialized = 4,
            BadParameter = 5,
            Unsupported = 6,
            NotFound = 7,
            AlreadyExists = 8,
            IndexOutOfRange = 9,
            InvalidEntity = 10,
            InvalidThread = 11,
            BufferTooSmall = 12,
            DataNotAvailable = 13,
            InvalidData = 14,
            SkeletonMismatch = 15,
            LibraryLoadFailed = 16,
            Pending = 17,
            MissingAccessToken = 18,
            MemoryLeak = 19,
            RequestCallbackNotSet = 20,
            UnmatchedLoadFilters = 21,
            DeserializationPending = 22,
            LegacyJointTypeFallback = 23,

            Count,
        }

        //-----------------------------------------------------------------
        //
        // Joints
        //

        public enum ovrAvatar2JointType : Int32
        {
            Invalid = -1,

            Root = 0,
            Hips = 1,
            LeftLegUpper = 2,
            LeftLegLower = 3,
            LeftFootAnkle = 4,
            LeftFootBall = 5,
            RightLegUpper = 6,
            RightLegLower = 7,
            RightFootAnkle = 8,
            RightFootBall = 9,
            SpineLower = 10,
            SpineMiddle = 11,
            SpineUpper = 12,
            Chest = 13,
            Neck = 14,
            Head = 15,
            LeftShoulder = 16,
            LeftArmUpper = 17,
            LeftArmLower = 18,
            LeftHandWrist = 19,
            RightShoulder = 20,
            RightArmUpper = 21,
            RightArmLower = 22,
            RightHandWrist = 23,
            LeftHandThumbTrapezium = 24,
            LeftHandThumbMeta = 25,
            LeftHandThumbProximal = 26,
            LeftHandThumbDistal = 27,
            LeftHandIndexMeta = 28,
            LeftHandIndexProximal = 29,
            LeftHandIndexIntermediate = 30,
            LeftHandIndexDistal = 31,
            LeftHandMiddleMeta = 32,
            LeftHandMiddleProximal = 33,
            LeftHandMiddleIntermediate = 34,
            LeftHandMiddleDistal = 35,
            LeftHandRingMeta = 36,
            LeftHandRingProximal = 37,
            LeftHandRingIntermediate = 38,
            LeftHandRingDistal = 39,
            LeftHandPinkyMeta = 40,
            LeftHandPinkyProximal = 41,
            LeftHandPinkyIntermediate = 42,
            LeftHandPinkyDistal = 43,
            RightHandThumbTrapezium = 44,
            RightHandThumbMeta = 45,
            RightHandThumbProximal = 46,
            RightHandThumbDistal = 47,
            RightHandIndexMeta = 48,
            RightHandIndexProximal = 49,
            RightHandIndexIntermediate = 50,
            RightHandIndexDistal = 51,
            RightHandMiddleMeta = 52,
            RightHandMiddleProximal = 53,
            RightHandMiddleIntermediate = 54,
            RightHandMiddleDistal = 55,
            RightHandRingMeta = 56,
            RightHandRingProximal = 57,
            RightHandRingIntermediate = 58,
            RightHandRingDistal = 59,
            RightHandPinkyMeta = 60,
            RightHandPinkyProximal = 61,
            RightHandPinkyIntermediate = 62,
            RightHandPinkyDistal = 63,

            Count
        }
    }
}
