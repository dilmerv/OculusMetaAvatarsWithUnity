
using UnityEngine;

namespace Oculus.Avatar2
{
    public static class OvrAvatarConversions
    {
        // TODO: Make internal, used by BodyAnimTracking.Update()
        public static CAPI.ovrAvatar2Transform ConvertSpace(this in CAPI.ovrAvatar2Transform from)
        {
            var converted = from;
            converted.orientation = from.orientation.ConvertSpace();
            converted.position = from.position.ConvertSpace();
            return converted;
        }

        // TODO: Make internal, used by BodyAnimTracking.Start()
        public static CAPI.ovrAvatar2Quatf ConvertSpace(this in CAPI.ovrAvatar2Quatf from)
        {
            var converted = from;
            converted.x = -converted.x;
            converted.y = -converted.y;
            return converted;
        }

        // TODO: Make internal, used by BodyAnimTracking.Start()
        public static CAPI.ovrAvatar2Vector3f ConvertSpace(this in CAPI.ovrAvatar2Vector3f from)
        {
            var converted = from;
            converted.z = -converted.z;
            return converted;
        }

        internal static void ApplyWorldOvrTransform(this Transform transform, in CAPI.ovrAvatar2Transform from)
        {
            transform.position = from.position;
            transform.rotation = from.orientation;
            transform.localScale = from.scale;
        }

        internal static void ApplyOvrTransform(this Transform transform, in CAPI.ovrAvatar2Transform from)
        {
            // NOTE: We could route this to the * version, but `fixed` has non-trivial overhead
            transform.localPosition = from.position;
            transform.localRotation = from.orientation;
            transform.localScale = from.scale;
        }

        internal unsafe static void ApplyOvrTransform(this Transform transform, CAPI.ovrAvatar2Transform* from)
        {
            transform.localPosition = from->position;
            transform.localRotation = from->orientation;
            transform.localScale = from->scale;
        }

        internal static CAPI.ovrAvatar2Transform ToWorldOvrTransform(this Transform t)
        {
            return new CAPI.ovrAvatar2Transform(t.position, t.rotation, t.localScale);
        }

        internal static Matrix4x4 ToMatrix(this in CAPI.ovrAvatar2Transform t)
        {
            return Matrix4x4.TRS(t.position, t.orientation, t.scale);
        }
    }

    public static class OvrAvatarUtility
    {
        public static CAPI.ovrAvatar2Transform CombineOvrTransforms(
            in CAPI.ovrAvatar2Transform parent, in CAPI.ovrAvatar2Transform child)
        {
            var scaledChildPose = new CAPI.ovrAvatar2Vector3f
            {
                x = child.position.x * parent.scale.x,
                y = child.position.y * parent.scale.y,
                z = child.position.z * parent.scale.z
            };

            var result = new CAPI.ovrAvatar2Transform
            {
                position =
                  parent.position + (CAPI.ovrAvatar2Vector3f) ((Quaternion) parent.orientation * scaledChildPose),

                orientation = (Quaternion)parent.orientation * child.orientation,

                scale = new CAPI.ovrAvatar2Vector3f
                {
                    x = parent.scale.x * child.scale.x,
                    y = parent.scale.y * child.scale.y,
                    z = parent.scale.z * child.scale.z
                }
            };

            return result;
        }

        public static string GetAsString(this CAPI.ovrAvatar2Transform transform, int decimalPlaces = 2)
        {
            string format = "F" + decimalPlaces;
            return $"{((Vector3)transform.position).ToString(format)}, {((Quaternion)transform.orientation).eulerAngles.ToString(format)}, {((Vector3)transform.scale).ToString(format)}";
        }

        public static bool IsNaN(this CAPI.ovrAvatar2Vector3f v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
        }

        public static bool IsNaN(this CAPI.ovrAvatar2Quatf q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        public static bool IsNan(this CAPI.ovrAvatar2Transform t)
        {
            return t.position.IsNaN() || t.orientation.IsNaN() || t.scale.IsNaN();
        }
    }
}
