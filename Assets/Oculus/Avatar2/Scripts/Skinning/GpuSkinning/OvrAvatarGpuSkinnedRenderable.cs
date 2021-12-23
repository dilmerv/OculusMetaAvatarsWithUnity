//#define OVR_EXPLICIT_CONVERT_VEC3

using System;
using System.Collections;
using System.Runtime.InteropServices;

using Oculus.Avatar2;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

/// @file OvrAvatarGpuSkinnedRenderable.cs

namespace Oculus.Skinning.GpuSkinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using the Avatar SDK
     * and uses the GPU. It performs skinning on every avatar
     * at each frame. It is used when the skinning configuration
     * is set to SkinningConfig.OVR_UNITY_GPU_FULL and motion smoothing
     * is *not* enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarGpuSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     * @see OvrAvatarGpuInterpolatedSkinnedRenderable
     */
    public class OvrAvatarGpuSkinnedRenderable : OvrAvatarSkinnedRenderable
    {
        public const string OVR_VERTEX_FETCH_TEXTURE_KEYWORD = "OVR_VERTEX_FETCH_TEXTURE";
        public const string OVR_VERTEX_FETCH_TEXTURE_UNORM_KEYWORD = "OVR_VERTEX_FETCH_TEXTURE_UNORM";

        public static readonly int U_ATTRIBUTE_TEXTURE_PROP_ID = Shader.PropertyToID("u_AttributeTexture");
        public static readonly int U_ATTRIBUTE_SCALE_BIAS_PROP_ID = Shader.PropertyToID("u_AttributeScaleBias");

        // TODO: Change the following parameter in the native implementation to match:
        // public static readonly int U_ATTRIBUTE_TEXEL_RECT_PROP_ID = Shader.PropertyToID("u_AttributeTexelRect");
        public static readonly int U_ATTRIBUTE_TEXEL_X_PROP_ID = Shader.PropertyToID("u_AttributeTexelX");
        public static readonly int U_ATTRIBUTE_TEXEL_Y_PROP_ID = Shader.PropertyToID("u_AttributeTexelY");
        public static readonly int U_ATTRIBUTE_TEXEL_W_PROP_ID = Shader.PropertyToID("u_AttributeTexelW");
        public static readonly int U_ATTRIBUTE_TEXEL_H_PROP_ID = Shader.PropertyToID("u_AttributeTexelH");
        public static readonly int U_ATTRIBUTE_TEXEL_SLICE_PROP_ID = Shader.PropertyToID("u_AttributeTexelSlice");

        // TODO: Change the following parameter in the native implementation to match:
        // public static readonly int U_ATTRIBUTE_TEX_INV_SIZE_PROP_ID = Shader.PropertyToID("u_AttributeTexInvSize");
        public static readonly int U_ATTRIBUTE_TEX_INV_SIZE_W_PROP_ID = Shader.PropertyToID("u_AttributeTexInvSizeW");
        public static readonly int U_ATTRIBUTE_TEX_INV_SIZE_H_PROP_ID = Shader.PropertyToID("u_AttributeTexInvSizeH");
        public static readonly int U_ATTRIBUTE_TEX_INV_SIZE_D_PROP_ID = Shader.PropertyToID("u_AttributeTexInvSizeD");

        private Renderer _meshRenderer => rendererComponent;

        protected float SkinnerLayoutSlice { get; private set; }

        protected virtual FilterMode SkinnerOutputFilterMode => FilterMode.Point;
        protected virtual int SkinnerOutputDepthTexelsPerSlice => 1;

        private protected SkinningOutputFrame SkinnerWriteDestination { get; set; } =
            SkinningOutputFrame.FrameZero;

        /// Specifies the skinning quality (many bones per vertex).
        public OvrSkinningTypes.SkinningQuality SkinningQuality
        {
            get => _skinningQuality;
            set
            {
                if (_skinningQuality != value)
                {
                    _skinningQuality = value;
                    UpdateSkinningQuality();
                }
            }
        }

        // This is technically configurable, but mostly just for debugging
        [SerializeField]
        [Tooltip("Configuration to override SkinningQuality, otherwise indicates which Quality was selected for this LOD")]
        private OvrSkinningTypes.SkinningQuality _skinningQuality = OvrSkinningTypes.SkinningQuality.Invalid;

        protected virtual void OnEnable()
        {
            if (_gpuCombiner != null)
            {
                OvrAvatarManager.Instance.GpuSkinningController.AddCombiner(_gpuCombiner);
            }

            if (_skinner != null)
            {
                OvrAvatarManager.Instance.GpuSkinningController.AddSkinner(_skinner);
            }
        }

        protected void OnDisable()
        {
            var mgr = (OvrAvatarManager.hasInstance && !OvrAvatarManager.shuttingDown)
                ? OvrAvatarManager.Instance.GpuSkinningController
                : null;
            if (mgr != null)
            {
                if (_skinner != null)
                {
                    mgr.RemoveSkinner(_skinner);
                }

                if (_gpuCombiner != null)
                {
                    mgr.RemoveCombiner(_gpuCombiner);
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                DestroyGpuSkinningObjects();
            }

            _bufferHandle.Dispose();

            base.Dispose(isDisposing);
        }

        private void DestroyGpuSkinningObjects()
        {
            _gpuCombiner?.Destroy();
            _gpuCombiner = null;
            _indirectionTex?.Destroy();
            _indirectionTex = null;
            _skinner?.Destroy();
            _skinner = null;
        }

        private void UpdateSkinningQuality()
        {
            if (_skinner is IOvrGpuJointSkinner jointSkinner)
            {
                jointSkinner.Quality = _skinningQuality;
            }
        }

        public override void ApplyMeshPrimitive(OvrAvatarPrimitive primitive)
        {
            // The base call adds a mesh filter already and material
            base.ApplyMeshPrimitive(primitive);

            try
            {
                if (_skinningQuality == OvrSkinningTypes.SkinningQuality.Invalid)
                {
                    _skinningQuality = GpuSkinningConfiguration.Instance.GetQualityForLOD(primitive.HighestQualityLODIndex);
                }

                AddGpuSkinningObjects(primitive);
                ActivateGpuSkinningInMaterial();
                ApplyGpuSkinningMaterial();
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError($"Exception applying primitive ({primitive}) - {e}", logScope, this);
            }
        }

        private void ActivateGpuSkinningInMaterial()
        {
            if (_skinner?.GetOutputTexGraphicFormat() == GraphicsFormat.R16G16B16A16_UNorm)
            {
                _meshRenderer.sharedMaterial.EnableKeyword(OVR_VERTEX_FETCH_TEXTURE_UNORM_KEYWORD);
                _meshRenderer.sharedMaterial.DisableKeyword(OVR_VERTEX_FETCH_TEXTURE_KEYWORD);
            }
            else
            {
                _meshRenderer.sharedMaterial.DisableKeyword(OVR_VERTEX_FETCH_TEXTURE_UNORM_KEYWORD);
                _meshRenderer.sharedMaterial.EnableKeyword(OVR_VERTEX_FETCH_TEXTURE_KEYWORD);
            }
        }

        private void ApplyGpuSkinningMaterial()
        {
            MaterialPropertyBlock matBlock = new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(matBlock);

            Texture outputTexture = null;
            CAPI.ovrTextureLayoutResult layout = new CAPI.ovrTextureLayoutResult();

            if (_skinner != null)
            {
                outputTexture = _skinner.GetOutputTex();
                layout = _skinner.GetLayoutInOutputTex(_handleInSkinner);
            }

            matBlock.SetTexture(U_ATTRIBUTE_TEXTURE_PROP_ID, outputTexture);

            if (_skinner?.GetOutputTexGraphicFormat() == GraphicsFormat.R16G16B16A16_UNorm)
            {
                var scale = GpuSkinningConfiguration.Instance.SkinnerUnormScale;
                var scaleBias = new Vector2(2.0f * scale, -scale);
                matBlock.SetVector(U_ATTRIBUTE_SCALE_BIAS_PROP_ID, scaleBias);
            }
            else
            {
                matBlock.SetVector(U_ATTRIBUTE_SCALE_BIAS_PROP_ID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            }

            matBlock.SetInt(U_ATTRIBUTE_TEXEL_X_PROP_ID, layout.x);
            matBlock.SetInt(U_ATTRIBUTE_TEXEL_Y_PROP_ID, layout.y);
            matBlock.SetInt(U_ATTRIBUTE_TEXEL_W_PROP_ID, layout.w);
            matBlock.SetInt(U_ATTRIBUTE_TEXEL_H_PROP_ID, layout.h);
            matBlock.SetFloat(U_ATTRIBUTE_TEXEL_SLICE_PROP_ID, layout.texSlice);

            SkinnerLayoutSlice = layout.texSlice;

            Debug.Assert(outputTexture != null, "No output texture for GPU skinning, avatars may not be able to move.");
            if (outputTexture != null)
            {
                matBlock.SetFloat(U_ATTRIBUTE_TEX_INV_SIZE_W_PROP_ID, 1.0f / outputTexture.width);
                matBlock.SetFloat(U_ATTRIBUTE_TEX_INV_SIZE_H_PROP_ID, 1.0f / outputTexture.height);

                if (outputTexture.dimension == TextureDimension.Tex3D)
                {
                    RenderTexture rt = outputTexture as RenderTexture;
                    if (rt)
                    {
                        matBlock.SetFloat(U_ATTRIBUTE_TEX_INV_SIZE_D_PROP_ID, 1.0f / rt.volumeDepth);
                    }
                }
            }

            _meshRenderer.SetPropertyBlock(matBlock);
        }

        public override void ApplySkeleton(Transform[] bones)
        {
            // No-op
        }

        public override IDisposableBuffer CheckoutMorphTargetBuffer(uint morphCount)
        {
            _bufferHandle.nativeSliceBuffer = _gpuCombiner.GetMorphBuffer(_handleInCombiner);
            return _bufferHandle;
        }

        public override void MorphTargetBufferUpdated(IDisposableBuffer buffer)
        {
            _gpuCombiner.FinishMorphUpdate(_handleInCombiner);
        }

        private const int Matrix4x4Size = 16 * sizeof(float);
        public override bool UpdateJointMatrices(CAPI.ovrAvatar2EntityId entityId, OvrAvatarPrimitive primitive, CAPI.ovrAvatar2PrimitiveRenderInstanceID primitiveInstanceId)
        {
            // TODO: Caller should be handling this check
            if (_skinner == null || !_skinner.HasJoints)
            {
                return false;
            }

            int jointsCount = primitive.joints.Length;
            UInt32 bufferSize = (UInt32)(OvrJointsData.JointDataSize * jointsCount);

            var transformsSlice = _skinner.GetJointTransformMatricesArray(_handleInSkinner);

            if (!transformsSlice.HasValue)
            {
                return false;
            }

            Debug.Assert(transformsSlice.Value.Length == jointsCount);
            IntPtr transformsPtr;
            unsafe { transformsPtr = (IntPtr)transformsSlice.Value.GetUnsafePtr(); }

            Profiler.BeginSample("GetSkinTransforms");
            var result =
                CAPI.ovrAvatar2Render_GetSkinTransforms(entityId, primitiveInstanceId, transformsPtr, bufferSize, true);
            Profiler.EndSample();

            if (result == CAPI.ovrAvatar2Result.Success)
            {
                _skinner.UpdateJointTransformMatrices(_handleInSkinner);
                _skinner.EnableBlockToRender(_handleInSkinner, SkinnerWriteDestination);
            }
            else
            {
                Debug.LogError($"[OvrAvatarEntity] Error: GetSkinTransforms ({primitive}) {result}");
            }

            return true;
        }

        private void AddGpuSkinningObjects(OvrAvatarPrimitive primitive)
        {
            // For now, just create source textures at runtime
            // TODO*: The texture creation should really be part of pipeline
            // and part of the input files from SDK and should be handled via
            // native plugin, but, for now, create via C#
            if (_mesh)
            {
                var gpuSkinningConfig = GpuSkinningConfiguration.Instance;

                int numMorphTargets = (int)primitive.morphTargetCount;
                int numBones = primitive.joints.Length;
                bool hasJoints = numBones > 0;
                int numAttributes = HasTangents ? 3 : 2;

                var metaData = primitive.gpuPrimitive.MetaData;

                int numAffectedVerts = (int)metaData.NumMorphTargetAffectedVerts;
                bool hasMorphTargets = numAffectedVerts > 0;

                OvrSkinningTypes.Handle handleInIndirectionTex = OvrSkinningTypes.Handle.kInvalidHandle;
                if (hasMorphTargets)
                {
                    var morphTextureFormat = gpuSkinningConfig.CombinedMorphFormat;
                    Vector2Int morphTargetCombinedDims = OvrGpuSkinningUtils.findMorphTargetCombinerSize(numAffectedVerts, numAttributes);

                    // TODO: This seems like it should be a call into gpuskinning?
                    Vector2Int morphDimensions = OvrGpuSkinningUtils.findOptimalTextureDimensions(
                        numAffectedVerts,
                        numAttributes,
                        (uint)Mathf.Max(morphTargetCombinedDims.x, morphTargetCombinedDims.y));

                    int morphTargetCombinerTexels = morphTargetCombinedDims.x * morphTargetCombinedDims.y;
                    Debug.Assert(morphTargetCombinerTexels > 0, this);
                    {
                        _gpuCombiner = new OvrGpuMorphTargetsCombiner(
                            "morphCombined(" + primitive.shortName + ")",
                            morphTargetCombinedDims.x,
                            morphTargetCombinedDims.y,
                            morphTextureFormat.GetGraphicsFormat(),
                            primitive.gpuPrimitive.MorphTargetSourceTex,
                            primitive.gpuPrimitive.MetaData.PositionRange,
                            primitive.gpuPrimitive.MetaData.NormalRange,
                            primitive.gpuPrimitive.MetaData.TangentRange,
                            HasTangents,
                            gpuSkinningConfig.SourceMorphFormat == GpuSkinningConfiguration.TexturePrecision.Snorm10,
                            GpuSkinningConfiguration.Instance.CombineMorphTargetsShader);

                        var layoutRect = metaData.LayoutInMorphTargetsTex.ToRectInt();
                        _handleInCombiner = _gpuCombiner.AddMorphTargetBlock(
                            layoutRect,
                            in morphDimensions,
                            (int)metaData.LayoutInMorphTargetsTex.texSlice,
                            numMorphTargets);

                        OvrAvatarLog.AssertParam(_handleInCombiner.IsValid()
                            , _gpuCombiner, _MorphTargetAssertStringBuilder, logScope, this);
                    }

                    var indirectTextureFormat = gpuSkinningConfig.IndirectionFormat;
                    Vector2Int indirectionDims = OvrGpuSkinningUtils.findIndirectionTextureSize(_mesh.vertexCount, numAttributes);

                    int indirectionTexels = indirectionDims.x * indirectionDims.y;
                    Debug.Assert(indirectionTexels > 0, this);
                    {
                        _indirectionTex = new OvrExpandableTextureArray(
                            "indirection(" + primitive.shortName + ")",
                            indirectionDims.x,
                            indirectionDims.y,
                            indirectTextureFormat);
                    }

                    CAPI.ovrTextureLayoutResult layout =
                        _gpuCombiner.GetLayoutInCombinedTex(_handleInCombiner);
                    // TODO: Figure out why checking combinedLayoutTexels breaks blinking... but not visemes?!?
                    //int combinedLayoutTexels = layout.x * layout.y;
                    //if (combinedLayoutTexels > 0)
                    {
                        handleInIndirectionTex = IndirectionTextureAddBlock(
                            ref _indirectionTex,
                            layout.ExtractRectiOnly(),
                            layout.texSlice,
                            (uint)_gpuCombiner.Width,
                            (uint)_gpuCombiner.Height,
                            _gpuCombiner.GetTexCoordForUnaffectedVertices(),
                            (uint)_mesh.vertexCount,
                            metaData.NumMorphTargetAffectedVerts,
                            metaData.MeshVertexToAffectedIndex
                        );
                        OvrAvatarLog.AssertParam(handleInIndirectionTex.IsValid(), name
                            , _IndirectionTextureAssertStringBuilder, logScope, this);
                    }
                }

                // Before we begin, check to see if we already have a skinner/morph target system set up:
                Debug.Assert(_skinner == null,
                    "Only one Skinning / Morph Target system can be created for Renderable.");

                var outputFormat = gpuSkinningConfig.SkinnerOutputFormat.GetGraphicsFormat();
                if (hasJoints)
                {
                    if (hasMorphTargets)
                    {
                        // Joints and morph targets
                        var fullSkinner = new OvrGpuSkinner(
                            metaData.LayoutInNeutralPoseTex.w,
                            metaData.LayoutInNeutralPoseTex.h,
                            outputFormat,
                            SkinnerOutputFilterMode,
                            SkinnerOutputDepthTexelsPerSlice,
                            primitive.gpuPrimitive.NeutralPoseTex,
                            primitive.gpuPrimitive.JointsTex,
                            _skinningQuality,
                            _indirectionTex,
                            _gpuCombiner,
                            GpuSkinningConfiguration.Instance.SkinToTextureShader);
                        _handleInSkinner = fullSkinner.AddBlock(
                            metaData.LayoutInNeutralPoseTex.w,
                            metaData.LayoutInNeutralPoseTex.h,
                            metaData.LayoutInNeutralPoseTex,
                            metaData.LayoutInJointsTex,
                            numBones,
                            _indirectionTex.GetLayout(handleInIndirectionTex));

                        _skinner = fullSkinner;
                    }
                    else
                    {
                        // Joints only
                        var jointsOnlySkinner = new OvrGpuSkinnerJointsOnly(
                            metaData.LayoutInNeutralPoseTex.w,
                            metaData.LayoutInNeutralPoseTex.h,
                            outputFormat,
                            SkinnerOutputFilterMode,
                            SkinnerOutputDepthTexelsPerSlice,
                            primitive.gpuPrimitive.NeutralPoseTex,
                            primitive.gpuPrimitive.JointsTex,
                            _skinningQuality,
                            GpuSkinningConfiguration.Instance.SkinToTextureShader);
                        _handleInSkinner = jointsOnlySkinner.AddBlock(
                            metaData.LayoutInNeutralPoseTex.w,
                            metaData.LayoutInNeutralPoseTex.h,
                            metaData.LayoutInNeutralPoseTex,
                            metaData.LayoutInJointsTex,
                            numBones);

                        _skinner = jointsOnlySkinner;
                    }
                }
                else
                {
                    // Morph targets only
                    var morphTargetsOnlySkinner = new OvrGpuSkinnerMorphTargetsOnly(
                        metaData.LayoutInNeutralPoseTex.w,
                        metaData.LayoutInNeutralPoseTex.h,
                        outputFormat,
                        SkinnerOutputFilterMode,
                        SkinnerOutputDepthTexelsPerSlice,
                        primitive.gpuPrimitive.NeutralPoseTex,
                        _indirectionTex,
                        _gpuCombiner,
                        GpuSkinningConfiguration.Instance.SkinToTextureShader);
                    _handleInSkinner = morphTargetsOnlySkinner.AddBlock(
                        metaData.LayoutInNeutralPoseTex.w,
                        metaData.LayoutInNeutralPoseTex.h,
                        metaData.LayoutInNeutralPoseTex,
                        metaData.LayoutInMorphTargetsTex);

                    _skinner = morphTargetsOnlySkinner;
                }

                if (isActiveAndEnabled)
                {
                    if (_gpuCombiner != null)
                    {
                        OvrAvatarManager.Instance.GpuSkinningController.AddCombiner(_gpuCombiner);
                    }

                    if (_skinner != null)
                    {
                        OvrAvatarManager.Instance.GpuSkinningController.AddSkinner(_skinner);
                    }
                }
            } // if has mesh
        }
        private static string _MorphTargetAssertStringBuilder(in OvrGpuMorphTargetsCombiner combiner)
            => $"Morph Target block unsuccesfully added to combiner {combiner}.";
        private static string _IndirectionTextureAssertStringBuilder(in string meshName)
            => $"Indirection block unsuccessfully added to indirection texture {meshName}.";


        private void DestroyTempTexture(Texture2D tempTex)
        {
            Texture2D.Destroy(tempTex);
        }


        // simple port of CAPI.ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays from native C++ to managed C#
        unsafe CAPI.ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays(
                    CAPI.ovrGpuSkinningRecti texelsInCombinedTex,
                    UInt32 combinedTexSlice,
                    UInt32 combinedTexWidth,
                    UInt32 combinedTexHeight,
                    ref CAPI.ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
                    UInt32 meshVertCount,
                    UInt32 morphTargetAffectedVertCount,
                    bool hasTangents,
                    UInt32 numAttributes,
                    int[] meshVertIndexToAffectedVertIndex,
                    IntPtr positionTexCoordsPtr,
                    IntPtr normalTexCoordsPtr,
                    IntPtr tangentTexCoordsPtr)
        {
#if !UNITY_EDITOR
            unchecked
#endif
            {
                float* positionTexCoords = (float*)positionTexCoordsPtr.ToPointer();
                float* normalTexCoords = (float*)normalTexCoordsPtr.ToPointer();
                float* tangentTexCoords = hasTangents ? (float*)tangentTexCoordsPtr.ToPointer() : null;

                float invTexWidth = 1.0f / combinedTexWidth;
                float invTwoTexWidth = invTexWidth * 0.5f;
                float invTexHeight = 1.0f / combinedTexHeight;
                float invTwoTexHeight = invTexHeight * 0.5f;

                CAPI.ovrAvatar2Vector3f firstTexelCenter = new CAPI.ovrAvatar2Vector3f();
                firstTexelCenter.x = (2.0f * texelsInCombinedTex.x + 1.0f) * invTwoTexWidth;
                firstTexelCenter.y = (2.0f * texelsInCombinedTex.y + 1.0f) * invTwoTexHeight;
                firstTexelCenter.z = combinedTexSlice;

                // Loop over mesh vertices, calculating the texture coordinate for each vert in the combined
                // morph target texture.
                UInt32 rowWidth = (uint)texelsInCombinedTex.w;
                Debug.Assert(rowWidth > 0);

                int kNumFloatsPerTexCoord = 3;
                for (Int32 vertIndex = 0, floatIndex = 0; vertIndex < meshVertCount;
                     vertIndex++, floatIndex += kNumFloatsPerTexCoord)
                {
                    // See if affected by morph targets
                    Int32 affectedVertIndex = meshVertIndexToAffectedVertIndex[vertIndex];

                    int kUnaffectedVertexIndex = -1;
                    if (affectedVertIndex == kUnaffectedVertexIndex)
                    {
                        // Not affected
                        positionTexCoords[floatIndex + 0] = unaffectedVertTexCoordInCombinedTex.x;
                        positionTexCoords[floatIndex + 1] = unaffectedVertTexCoordInCombinedTex.y;
                        positionTexCoords[floatIndex + 2] = unaffectedVertTexCoordInCombinedTex.z;

                        normalTexCoords[floatIndex + 0] = unaffectedVertTexCoordInCombinedTex.x;
                        normalTexCoords[floatIndex + 1] = unaffectedVertTexCoordInCombinedTex.y;
                        normalTexCoords[floatIndex + 2] = unaffectedVertTexCoordInCombinedTex.z;

                        if (hasTangents)
                        {
                            tangentTexCoords[floatIndex + 0] = unaffectedVertTexCoordInCombinedTex.x;
                            tangentTexCoords[floatIndex + 1] = unaffectedVertTexCoordInCombinedTex.y;
                            tangentTexCoords[floatIndex + 2] = unaffectedVertTexCoordInCombinedTex.z;
                        }
                    }
                    else
                    {
                        // Is affected by at least one morph target, calculate texture coordinate
                        // for each attribute (if needed)
                        UInt32 rowForPosition = (uint)affectedVertIndex / rowWidth;
                        UInt32 column = (uint)affectedVertIndex % rowWidth;

                        // Account for other rows for the other attributes (normals, tangents)
                        rowForPosition *= numAttributes;

                        // Convert from row/column to texel center texture coordinates
                        float texCoordX = firstTexelCenter.x + (column * invTexWidth);
                        positionTexCoords[floatIndex + 0] = texCoordX;
                        positionTexCoords[floatIndex + 1] = firstTexelCenter.y + (rowForPosition * invTexHeight);
                        positionTexCoords[floatIndex + 2] = firstTexelCenter.z;

                        normalTexCoords[floatIndex + 0] = texCoordX;
                        normalTexCoords[floatIndex + 1] = firstTexelCenter.y + ((rowForPosition + 1) * invTexHeight);
                        normalTexCoords[floatIndex + 2] = firstTexelCenter.z;

                        if (hasTangents)
                        {
                            tangentTexCoords[floatIndex + 0] = texCoordX;
                            tangentTexCoords[floatIndex + 1] =
                                firstTexelCenter.y + ((rowForPosition + 2) * invTexHeight);
                            tangentTexCoords[floatIndex + 2] = firstTexelCenter.z;
                        }
                    }
                }
            }

            return CAPI.ovrGpuSkinningResult.Success;
        }

        private OvrSkinningTypes.Handle IndirectionTextureAddBlock(
            ref OvrExpandableTextureArray texArray,
            CAPI.ovrGpuSkinningRecti texelsInCombinedRect,
            UInt32 combinedTexSlice,
            UInt32 combinedTexWidth,
            UInt32 combinedTexHeight,
            CAPI.ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
            UInt32 meshVertCount,
            UInt32 morphTargetAffectedVertCount,
            Int32[] meshVertIndexToAffectedVertIndex
        )
        {
            // Calculate the rectangle for adding the block into the indirection
            // texture. It cannot be larger than the texture array's largest dimension
            // so that it will fit
            int numAttributes = HasTangents ? 3 : 2;

            Vector2Int dimensions = OvrGpuSkinningUtils.findOptimalTextureDimensions(
                (int)meshVertCount,
                numAttributes,
                (uint)Mathf.Max(texArray.Width, texArray.Height));
            UInt32 texelWidth = (UInt32)dimensions.x;
            UInt32 texelHeight = (UInt32)dimensions.y;

            OvrSkinningTypes.Handle handleInIndirectionTex = _indirectionTex.AddEmptyBlock(texelWidth, texelHeight);
            if (!handleInIndirectionTex.IsValid())
            {
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            const int kNumFloatsPerTexCoord = 3;
            const int kNumFloatsPerTexel = 4;
            {
                int coordSizeFromCAPI = (int)CAPI.OvrGpuSkinning_IndirectionTextureInfoTexCoordsSizeInBytes();
                Debug.Assert(coordSizeFromCAPI == kNumFloatsPerTexCoord * sizeof(float));
                int numBytes = _mesh.vertexCount * coordSizeFromCAPI;

                IntPtr posDataPtr = Marshal.AllocHGlobal(numBytes);
                IntPtr normDataPtr = Marshal.AllocHGlobal(numBytes);
                IntPtr tanDataPtr = Marshal.AllocHGlobal(numBytes);

                // C# port from CAPI handles with or without tangents
                ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays(texelsInCombinedRect,
                    combinedTexSlice,
                    combinedTexWidth,
                    combinedTexHeight,
                    ref unaffectedVertTexCoordInCombinedTex,
                    meshVertCount,
                    morphTargetAffectedVertCount,
                    HasTangents,
                    (uint)numAttributes,
                    meshVertIndexToAffectedVertIndex,
                    posDataPtr, // float results in a raw byte array
                    normDataPtr, // float results in a raw byte array
                    tanDataPtr
                    )
                    .LogErrors("get indirection data", this);

                UInt32 texelSizeFromCAPI = CAPI.OvrGpuSkinning_IndirectionTextureInfoTexelSizeInBytes();
                Debug.Assert(texelSizeFromCAPI == kNumFloatsPerTexel * sizeof(float));
                int numResultBytes = texArray.Width * texArray.Height * (int)texelSizeFromCAPI;

                // next fill in the indirection texture
                {
                    CAPI.ovrTextureLayoutResult layout = _indirectionTex.GetLayout(handleInIndirectionTex);

                    Texture2D tempTex = new Texture2D(
                        layout.w,
                        layout.h,
                        _indirectionTex.Format,
                        _indirectionTex.HasMips,
                        _indirectionTex.IsLinear);

                    var texData = tempTex.GetRawTextureData<byte>();

                    Debug.Assert(texData.Length == numResultBytes);

                    IntPtr dataPtr;
                    unsafe { dataPtr = (IntPtr)texData.GetUnsafePtr(); }
                    var bufferSize = texData.GetBufferSize();

                    bool populatedTextureData;
                    if (!HasTangents)
                    {
                        populatedTextureData = CAPI.OvrGpuSkinning_IndirectionTextureInfoPopulateTextureData(
                            texelWidth,
                            texelHeight,
                            meshVertCount,
                            posDataPtr,
                            normDataPtr,
                            dataPtr,
                            bufferSize
                        );
                    }
                    else
                    {
                        populatedTextureData = CAPI.OvrGpuSkinning_IndirectionTextureInfoPopulateTextureDataWithTangents(
                            texelWidth,
                            texelHeight,
                            meshVertCount,
                            posDataPtr,
                            normDataPtr,
                            tanDataPtr,
                            dataPtr,
                            bufferSize
                        );
                    }
                    if (!populatedTextureData)
                    {
                        OvrAvatarLog.LogError("Failed to populate gpuskinning texture data", logScope, this);
                    }

                    tempTex.Apply(false, true);

                    _indirectionTex.CopyFromTexture(layout, tempTex);

                    DestroyTempTexture(tempTex);
                }

                Marshal.FreeHGlobal(posDataPtr);
                Marshal.FreeHGlobal(normDataPtr);
                Marshal.FreeHGlobal(tanDataPtr);
            }

            return handleInIndirectionTex;
        }

        private OvrExpandableTextureArray _indirectionTex = null;
        private OvrGpuMorphTargetsCombiner _gpuCombiner = null;
        private IOvrGpuSkinner _skinner = null;

        private OvrSkinningTypes.Handle _handleInCombiner;
        private OvrSkinningTypes.Handle _handleInSkinner;

        private class BufferHandle : IDisposableBuffer
        {
            public BufferHandle() { }

            public NativeSlice<float> nativeSliceBuffer;

            public IntPtr BufferPtr
            {
                get
                {
                    IntPtr bufferPtr = IntPtr.Zero;
                    if (nativeSliceBuffer.Length > 0)
                    {
                        unsafe { bufferPtr = (IntPtr)nativeSliceBuffer.GetUnsafePtr(); }
                    }
                    return bufferPtr;
                }
            }

            public void Dispose()
            {
                // Buffer is persistent, no need for cleanup
                // TODO: Reference count to cover race conditions
            }
        }

        private readonly BufferHandle _bufferHandle = new BufferHandle();

        private const string logScope = "OvrAvatarGpuSkinnedRenderable";

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            if (MyMeshFilter != null)
            {
                Mesh m = MyMeshFilter.sharedMesh;
                if (m != null)
                {
                    Gizmos.matrix = MyMeshFilter.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(m.bounds.center, m.bounds.size);
                }
            }
        }

        protected void OnValidate()
        {
            UpdateSkinningQuality();
        }
#endif
    }
}
