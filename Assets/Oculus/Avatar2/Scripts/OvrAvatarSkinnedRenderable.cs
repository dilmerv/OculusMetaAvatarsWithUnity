#define OVR_AVATAR_PRIMITIVE_HACK_BOUNDING_BOX

using System;
using Oculus.Avatar2;
using UnityEngine;

/// @file OvrAvatarSkinnedRenderable.cs

public abstract class OvrAvatarSkinnedRenderable : OvrAvatarRenderable
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component can only be added to game objects that
     * have a Unity Mesh, a Mesh filter and a SkinnedRenderer.
     *
     * In addition to vertex positions, texture coordinates and
     * colors, a vertex in a skinned mesh can be driven by up
     * to 4 bones in the avatar skeleton. Each frame the transforms
     * of these bones are multiplied by the vertex weights for
     * the bone and applied to compute the final vertex position.
     * This can be done by Unity on the CPU or the GPU, or by
     * the Avatar SDK using the GPU. Different variations of this
     * class are provided to allow you to select which implementation
     * best suits your application.
     *
     * @see OvrAvatarPrimitive
     * @see ApplyMeshPrimitive
     * @see OvrAvatarUnitySkinnedRenderable
     */
    private const string logScope = "SkinnedRenderable";

    // TODO: Move to CpuSkinnedRenderable class - or a UnitySkinnedRenderable
    public override void ApplyMeshPrimitive(OvrAvatarPrimitive primitive)
    {
        CheckDefaultRenderer();

        base.ApplyMeshPrimitive(primitive);

        // TODO: May not need to copy for all SkinnedRenderables - or can share copies between some.
        // For now we need to ensure mats aren't getting shared accross invalid contexts
        CopyMaterial();

        // Initialize for the SkinnedMeshRenderer, not used for GPU Skinning
        var skinnedMeshRenderer = rendererComponent as SkinnedMeshRenderer;
        if (skinnedMeshRenderer != null)
        {
            // Initialize duplicate mesh structure to operate multiple avatars with different poses
            skinnedMeshRenderer.sharedMesh = _mesh;
            skinnedMeshRenderer.localBounds = primitive.hasBounds ? primitive.mesh.bounds : FixedBounds;
        }
    }

    ///
    /// Apply the given bone transforms from the avatar skeleton
    /// to the Unity skinned mesh renderer.
    /// @param bones    Array of Transforms for the skeleton bones.
    ///                 These must be in the order the Unity SkinnedRenderer expects.
    ///
    public abstract void ApplySkeleton(Transform[] bones);

    public abstract IDisposableBuffer CheckoutMorphTargetBuffer(uint morphCount);
    public abstract void MorphTargetBufferUpdated(IDisposableBuffer buffer);

    public virtual void UpdateSkinningOrigin(CAPI.ovrAvatar2Transform skinningOrigin)
    {
        // Default implementation is just to apply to transform
        transform.ApplyOvrTransform(skinningOrigin);
    }

    public abstract bool UpdateJointMatrices(CAPI.ovrAvatar2EntityId entityId, OvrAvatarPrimitive primitive, CAPI.ovrAvatar2PrimitiveRenderInstanceID primitiveInstanceId);

    // TODO: Reference count
    public interface IDisposableBuffer : IDisposable
    {
        IntPtr BufferPtr { get; }
    }

    #region Bounds support
    /// Bounding box for the skinned avatar, should encompass arms reaching in all dimensions.
    [Tooltip("This must be found empirically and encompass the arms reach in all 3 dimensions.")]
    [SerializeField] private Bounds FixedBounds = new Bounds(new Vector3(0f, 0.5f, 0.0f), new Vector3(2.0f, 2.0f, 2.0f));

    #endregion

}
