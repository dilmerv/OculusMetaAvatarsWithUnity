using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Application = UnityEngine.Application;

/**
 * @file OvrAvatarEntity.cs
 * This is the main file for a partial class.
 * Other functionality is split out into the files below:
 * - OvrAvatarEntity_Color.cs
 * - OvrAvatarEntity_Debug.cs
 * - OvrAvatarEntity_Loading.cs
 * - OvrAvatarEntity_LOD.cs
 * - OvrAvatarEntity_Rendering.cs
 * - OvrAvatarEntity_Streaming.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Describes a single avatar with multiple levels of detail.
     *
     * An avatar entity encapsulates all of the assets associated with it.
     * It provides access to the avatar's skeleton, meshes and materials
     * and integrates input from tracking components to drive the
     * avatar's motion. This base class provides defaults for all of these.
     * To customize avatar behavior you must derive from OvrAvatarEntity.
     * Then you can supply your application's preferences.
     *
     * # Loading an Avatar #
     * An avatar may have many representations varying in complexity
     * and fidelity. You can select among these capabilities
     * by setting _creationInfo flags before calling [CreateEntity()](@ref OvrAvatarEntity.CreateEntity).
     *
     * You can load the avatar's assets from LoadUser to download the user's custom avatar.
     * You can also load from [zip files](@ref LoadAssetsFromZipSource),
     * [streaming assets](@ref LoadAssetsFromStreamingAssets] or
     * [directly from memory](@ref LoadAssetsFromData) from within your subclass
     * to provide application-specific avatar loading.
     *
     * The avatar's assets are loaded in the background so your application
     * will not display the avatar immediately after load. The @ref IsPendingAvatar
     * flag is set when loading starts and cleared when it has finished.
     */
    public partial class OvrAvatarEntity : MonoBehaviour
    {


        // If any variable is used across these files, it should be placed in this file

        //:: Constants
        private const string logScope = "entity";

        protected const CAPI.ovrAvatar2EntityFeatures UPDATE_POSE_FEATURES =
            CAPI.ovrAvatar2EntityFeatures.AnalyticIk |
            CAPI.ovrAvatar2EntityFeatures.Animation;
        protected const CAPI.ovrAvatar2EntityFeatures UPDATE_MOPRHS_FEATURES =
            CAPI.ovrAvatar2EntityFeatures.Animation |
            CAPI.ovrAvatar2EntityFeatures.UseDefaultFaceAnimations;

        //:: Internal Structs & Classes
        protected readonly struct SkeletonJoint : IComparable<SkeletonJoint>
        {
            public readonly string name;
            public readonly Transform transform;
            public readonly int parentIndex;
            public readonly CAPI.ovrAvatar2NodeId nodeId;

            public SkeletonJoint(string jointName, Transform tx, int parentIdx, CAPI.ovrAvatar2NodeId nodeIdentifier)
            {
                name = jointName;
                transform = tx;
                parentIndex = parentIdx;
                nodeId = nodeIdentifier;
            }

            public SkeletonJoint(in SkeletonJoint oldJoint, string newName, int parentIdx)
                : this(newName, oldJoint.transform, parentIdx, oldJoint.nodeId)
            { }

            public int CompareTo(SkeletonJoint other)
            {
                return other.parentIndex - parentIndex;
            }
        }

        // TODO: This should just include LodCost?
        protected sealed class PrimitiveRenderData : IDisposable
        {
            public PrimitiveRenderData(CAPI.ovrAvatar2NodeId mshNodeId
                , CAPI.ovrAvatar2Id primId
                , CAPI.ovrAvatar2PrimitiveRenderInstanceID renderInstId
                , OvrAvatarRenderable ovrRenderable, OvrAvatarPrimitive ovrPrim)
            {
                meshNodeId = mshNodeId;
                primitiveId = primId;
                instanceId = renderInstId;
                renderable = ovrRenderable;
                skinnedRenderable = ovrRenderable as OvrAvatarSkinnedRenderable;
                primitive = ovrPrim;
            }

            public readonly CAPI.ovrAvatar2NodeId meshNodeId;
            public readonly CAPI.ovrAvatar2Id primitiveId;
            public readonly CAPI.ovrAvatar2PrimitiveRenderInstanceID instanceId;
            public readonly OvrAvatarRenderable renderable;
            public readonly OvrAvatarSkinnedRenderable skinnedRenderable;
            public readonly OvrAvatarPrimitive primitive;

            public bool IsValid => instanceId != CAPI.ovrAvatar2PrimitiveRenderInstanceID.Invalid;

            public void Dispose()
            {
                if (renderable != null) { renderable.Dispose(); }
            }

            public override string ToString()
            {
                return $"nodeId:{meshNodeId},primId:{primitiveId},instId:{instanceId},renderable:{renderable.name},prim:{primitive.name}";
            }
        }

        //:: Public Properties

        public bool HasJoints => SkeletonJointCount > 0;

        // TODO: Remove and replace IsCreated and IsLoading with corresponding checks of LoadState?
        public bool IsCreated => entityId != CAPI.ovrAvatar2EntityId.Invalid;

        // Disable obsolete parameter warning.  TODO need to create a different way to implement this
#pragma warning disable 618
        /// This is poorly named.
        /// This means that Unity meshes/gameobjects are currently being
        /// built, AFTER assets were successfully loaded from disk.
        /// @see IsPendingAvatar if you want to know if the entire load is in progress
        public bool IsLoading => LoadState == LoadingState.Loading;
#pragma warning restore 618

        /// True if the assets for an avatar are still loading.
        public bool IsPendingAvatar
        {
            get => IsPendingCdnAvatar || IsPendingZipAvatar;
        }

        /// True if the avatar is still loading the default avatar.
        public bool IsPendingDefaultModel { get; private set; }

        /// True if the avatar is still loading assets from a ZIP file.
        public bool IsPendingZipAvatar { get; private set; }

        /// True if the avatar is still loading assets from a CDN.
        public bool IsPendingCdnAvatar { get; private set; }

        /// True if the avatar has any avatar loaded other than the default avatar.
        public bool HasNonDefaultAvatar { get; private set; }

        /// Number of joints in the avatar skeleton.
        public int SkeletonJointCount => _skeleton.Length;

        // TODO: what does "active" mean?
        /// True if this entity is active.
        public bool EntityActive
        {
            get => _lastActive;
            set => _nextActive = value;
        }

        // External classes or overrides can add to this Action as needed.
        public event Action<bool> OnCulled;

        //:: Serialized Fields
        /**
         * Avatar creation configuration to use when creating this avatar.
         * Specifies overall level of detail, body parts to display,
         * rendering and animation characteristics and the avatar's point of view.
         *
         * If you are instantiating the same avatar asset more than once in the same
         * scene, you must use the same *renderFilter* for both. Using different
         * render filters on the same avatar asset is not supported.
         *
         * @see CAPI.ovrAvatar2EntityCreateInfo
         * @see CAPI.ovrAvatar2EntityLODFlags
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CAPI.ovrAvatar2EntityFilters
         */
        [Tooltip("Include the largest set of options that will be needed at runtime.")]
        [SerializeField]
        protected CAPI.ovrAvatar2EntityCreateInfo _creationInfo = new CAPI.ovrAvatar2EntityCreateInfo()
        {
            features = CAPI.ovrAvatar2EntityFeatures.Preset_Default,
            renderFilters = new CAPI.ovrAvatar2EntityFilters
            {
                lodFlags = CAPI.ovrAvatar2EntityLODFlags.All,
                manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Half,
                viewFlags = CAPI.ovrAvatar2EntityViewFlags.All
            }
        };

        [SerializeField] private CAPI.ovrAvatar2EntityViewFlags _activeView = CAPI.ovrAvatar2EntityViewFlags.FirstPerson;
        [SerializeField]
        private CAPI.ovrAvatar2EntityManifestationFlags _activeManifestation = CAPI.ovrAvatar2EntityManifestationFlags.Half;

        // Tracking
        [Header("Tracking Input")]
        [SerializeField] private OvrAvatarBodyTrackingBehavior _bodyTracking;
        [SerializeField] private OvrAvatarLipSyncBehavior _lipSync;

        [Header("Skeleton Output")]

        [Tooltip("Joints which will always have their Unity.Transform updated")]
        [SerializeField] protected CAPI.ovrAvatar2JointType[] _criticalJointTypes = Array.Empty<CAPI.ovrAvatar2JointType>();

        private uint[] _unityUpdateJointIndices = Array.Empty<uint>(); // names with missing/inactive joints removed

        //:: Protected Variables
        protected internal virtual Transform _baseTransform => transform;

        protected CAPI.ovrAvatar2EntityId entityId { get; private set; } = CAPI.ovrAvatar2EntityId.Invalid;

        protected UInt64 _userId = 0;

        //:: Private Variables

        // Coroutine is started on OvrAvatarManager to prevent early stops from disabling the Monobehavior
        private Coroutine _loadingRoutineBacking;
        private Coroutine _loadingRoutine
        {
            get { return _loadingRoutineBacking; }
            set
            {
                if (_loadingRoutineBacking != null)
                {
                    if (value != null)
                    {
                        OvrAvatarLog.LogError("More than one loading function running simultaneously!", logScope, this);
                    }
                    StopCoroutine(_loadingRoutineBacking);
                }

                _loadingRoutineBacking = value;
            }
        }

        // Mappings
        protected PrimitiveRenderData[][] _visiblePrimitiveRenderers = Array.Empty<PrimitiveRenderData[]>();

        // TODO: This likely has no utility anymore? It's a mirror of _meshNodes w/ a different key
        protected readonly Dictionary<CAPI.ovrAvatar2PrimitiveRenderInstanceID, PrimitiveRenderData[]> _primitiveRenderables
            = new Dictionary<CAPI.ovrAvatar2PrimitiveRenderInstanceID, PrimitiveRenderData[]>();

        // Render Data
        private SkeletonJoint[] _skeleton = Array.Empty<SkeletonJoint>();

        private readonly Dictionary<CAPI.ovrAvatar2NodeId, uint> _nodeToIndex = new Dictionary<CAPI.ovrAvatar2NodeId, uint>();
        private readonly Dictionary<CAPI.ovrAvatar2JointType, CAPI.ovrAvatar2NodeId> _jointTypeToNodeId = new Dictionary<CAPI.ovrAvatar2JointType, CAPI.ovrAvatar2NodeId>();

        private bool _lastActive = true;
        private bool _nextActive = true;

        // Suppress "never used" warning in non-editor builds
#pragma warning disable 0169
        // Suppress "never assigned to" warning
#pragma warning disable 0649
        [System.Serializable]
        private struct DebugDrawing
        {
            public bool drawTrackingPose;
            public bool drawBoneNames;
            public bool drawSkelHierarchy;
            public bool drawSkelHierarchyInGame;
            public bool drawSkinTransformsInGame;
            public bool drawCriticalJoints;
            public Color skeletonColor;
        }

        [SerializeField]
        private DebugDrawing _debugDrawing = new DebugDrawing
        { skeletonColor = Color.red };
#pragma warning restore 0649
#pragma warning restore 0169

        public bool TrackingPoseValid
        {
            get
            {
                if (IsCreated)
                {
                    var result = CAPI.ovrAvatar2Tracking_GetPoseValid(entityId, out var isValid);
                    if (result == CAPI.ovrAvatar2Result.Success)
                    {
                        return isValid;
                    }
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_GetPoseValid failed with {result}");
                    return false;
                }
                return false;
            }
        }

        /////////////////////////////////////////////////
        //:: Core Unity Functions

        #region Core Unity Functions

        protected virtual void Awake()
        {
            CreateEntity();

#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Camera.onPostRender += OnCameraPostRender;
#endif
            InitAvatarLOD();
        }

        public void UpdateLODInternal()
        {
            if (!isActiveAndEnabled || !IsCreated || IsLoading)
            {
                return;
            }

            Profiler.BeginSample("OvrAvatarEntity::UpdateLODInternal");

            UpdateAvatarLODOverride();

            SendImportanceAndCost();

            Profiler.EndSample();
        }

        // TODO: This should only be called from one place
        private bool getEntityActive(out bool isActive)
        {
            return CAPI.ovrAvatar2Entity_GetActive(entityId, out isActive)
                    .EnsureSuccess("ovrAvatar2Entity_GetActive", logScope, this);
        }

        // Non-virtual update run before virtual method
        internal void PreSDKUpdateInternal()
        {
            // State validation
            // Validate sync of tracked state
            OvrAvatarLog.AssertConstMessage(getEntityActive(out var queryActive) && queryActive == _lastActive
                , "Inconsistent activity state", logScope, this);

            // Apply C# active/inactive state to nativeSDK
            if (_lastActive != _nextActive)
            {
                if (CAPI.ovrAvatar2Entity_SetActive(entityId, _nextActive)
                    .EnsureSuccess("ovrAvatar2Entity_SetActive", logScope, this))
                {
                    _lastActive = _nextActive;
                }
            }

            // If active, apply C# state changes to nativeSDK
            if (_nextActive)
            {
                CAPI.ovrAvatar2Entity_SetRoot(entityId, _baseTransform.ToWorldOvrTransform().ConvertSpace());
            }
        }

        internal void PostSDKUpdateInternal()
        {
            // Apply nativeSDK active state changes
            if (getEntityActive(out var queryActive))
            {
                // TODO: Notify other systems of state change?
                _lastActive = _nextActive = queryActive;
            }
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        internal void UpdateInternal(float dT)
        {
            if (!isActiveAndEnabled || !IsCreated) { return; }

            Profiler.BeginSample("OvrAvatarEntity::UpdateInternal");

            if (IsLoading || IsPendingAvatar)
            {
                Profiler.BeginSample("OvrAvatarEntity::UpdateLoadingStatus");
                // TODO: What other errors should we be checking for?
                var result = entityStatus;

                // Occurs when spec download fails
                if (result == CAPI.ovrAvatar2Result.DataNotAvailable
                // Occurs when CDN download fails
                    || result == CAPI.ovrAvatar2Result.Unknown)
                {
#pragma warning disable 618
                    LoadState = LoadingState.Failed;
#pragma warning restore 618
                }
                Profiler.EndSample();
            }

            if (!IsLoading)
            {
                // Skip active render state updates if the entity is not active
                if (EntityActive)
                {
                    Profiler.BeginSample("OvrAvatarEntity::EntityActiveRenderUpdate");
                    EntityActiveRenderUpdate();
                    Profiler.EndSample();
                }

                // Only apply render updates if created and not loading
                if (IsCreated && !IsLoading)
                {
                    Profiler.BeginSample("OvrAvatarEntity::PerFrameRenderUpdate");
                    PerFrameRenderUpdate(dT);
                    Profiler.EndSample();
                }

                if (!IsLocal && useRenderLods)
                {
                    Profiler.BeginSample("OvrAvatarEntity::ComputeNetworkLod");
                    ComputeNetworkLod();
                    Profiler.EndSample();
                }
            }

            // accessing obsolete members
#pragma warning disable 618
            if (_lastInvokedLoadState != LoadState)
            {
                _lastInvokedLoadState = LoadState;

                LoadingStateChanged.Invoke(LoadState);
                EntityLoadingStateChanged.Invoke(this);
            }
#pragma warning restore 618

            Profiler.EndSample(); // "OvrAvatarEntity::UpdateInternal"
        }

        private void OnDestroy()
        {
            Teardown();
            OnDestroyCalled();

#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Camera.onPostRender -= OnCameraPostRender;
#endif
        }

        protected virtual void OnDestroyCalled() { }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Allows the tracking to up updated in the editor.
            SetBodyTracking(_bodyTracking);
            SetLipSync(_lipSync);
            if (IsCreated)
            {
                SetActiveManifestation(_activeManifestation);
                SetActiveView(_activeView);
                Hidden = Hidden;
            }
        }
#endif // UNITY_EDITOR

        #endregion

        /////////////////////////////////////////////////
        //:: Protected Functions

        #region Life Cycle

        /**
         * Initializes this OvrAvatarEntity instance.
         * An avatar may have many representations varying in complexity
         * and fidelity. These are described by @ref CAPI.ovrAvatar2EntityCreateInfo.
         * The value of @ref OvrAvatarEntity._creationInfo specifies the
         * avatar configuration to create this entity.
         *
         * You can customize entity creation by setting
         * *_creationInfo* flags before calling [CreateEntity()](@ref OvrAvatarEntity.CreateEntity)
         * It is necessary to set all of the members of @ref ovrAvatar2EntityCreateInfo,
         * the default values are unspecified.
         *
         * @code
         * class MyAvatar : public OvrAvatarEntity
         * {
         * public void CreateAvatar()
         * {
         *      var createInfo = new CAPI.ovrAvatar2EntityCreateInfo
         *      {
         *          features = CAPI.ovrAvatar2EntityFeatures.Preset_Default,
         *          renderFilters = new CAPI.ovrAvatar2EntityFilters
         *          {
         *               lodFlags = CAPI.ovrAvatar2EntityLODFlags.All,
         *               manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Half,
         *               viewFlags = CAPI.ovrAvatar2EntityViewFlags.All
         *          }
         *      };
         *     _creationInfo = createInfo;
         *     base.CreateEntity();
         * }
         * };
         * @endcode
         * @see CAPI.ovrAvatar2EntityCreateInfo
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see CAPI.ovrAvatar2EntityViewFlags
         */
        protected void CreateEntity()
        {
            if (IsCreated)
            {
                OvrAvatarLog.LogWarning("Setup called on an entity that has already been set up.", logScope, this);
                return;
            }
            if (_material == null)
            {
                _material = new OvrAvatarMaterial();
            }
            IsPendingDefaultModel = HasAllFeatures(CAPI.ovrAvatar2EntityFeatures.UseDefaultModel);
            ValidateSkinningType();
            SetRequiredFeatures();

            entityId = CreateNativeEntity(in _creationInfo);
            if (entityId == CAPI.ovrAvatar2EntityId.Invalid)
            {
                OvrAvatarLog.LogError("Failed to create entity pointer.", logScope, this);
                IsPendingDefaultModel = false;
                return;
            }

            OvrAvatarManager.Instance.AddTrackedEntity(this);

#pragma warning disable 618
            LoadState = LoadingState.Created;
#pragma warning restore 618

            CurrentState = AvatarState.Created;
            InvokeOnCreated();

            SetActiveView(_activeView);
            SetActiveManifestation(_activeManifestation);

            if (!IsLocal)
            {
                SetStreamingPlayback(true);
            }

            OvrAvatarLog.LogDebug($"Entity {entityId} created as a {(IsLocal ? "local" : "remote")} avatar", logScope, this);

            SetBodyTracking(_bodyTracking);
            SetLipSync(_lipSync);

            // For right now, only GPU skinning supports motion smoothing
            if (UseGpuSkinning && UseMotionSmoothingRenderer)
            {
                var entityAnimator = new EntityAnimatorMotionSmoothing(this);

                _entityAnimator = entityAnimator;
                _interpolationValueProvider = entityAnimator;
            }
            else
            {
                _entityAnimator = new EntityAnimatorDefault(this);
            }

            if (UseGpuSkinning)
            {
                if (UseMotionSmoothingRenderer)
                {
                    _jointMonitor = OvrAvatarManager.Instance.UseCriticalJointJobs
                        ? new OvrAvatarEntitySmoothingJointJobMonitor(this, _interpolationValueProvider)
                        : new OvrAvatarEntitySmoothingJointMonitor(this, _interpolationValueProvider);
                }
                else
                {
                    _jointMonitor = new OvrAvatarEntityJointMonitor(this);
                }
            }
        }

        private void PerFrameRenderUpdate(float dT)
        {
            Debug.Assert(IsCreated && !IsLoading);

            Profiler.BeginSample("OvrAvatarEntity::UpdateAnimationTime");
            _entityAnimator.UpdateAnimationTime(dT);
            Profiler.EndSample();

            Profiler.BeginSample("OvrAvatarEntity::JointMonitor::UpdateJoints");
            _jointMonitor?.UpdateJoints(dT);
            Profiler.EndSample();
        }

        private void EntityActiveRenderUpdate()
        {
            Debug.Assert(EntityActive && IsCreated && !IsLoading);

            // Check that our pose is valid
            if (!QueryEntityPose(out var entityPose, out var hierarchyVersion)) { return; }
            OvrAvatarLog.Assert(hierarchyVersion != CAPI.ovrAvatar2HierarchyVersion.Invalid, logScope, this);

            if (!QueryEntityRenderState(out var renderState)) { return; }
            OvrAvatarLog.Assert(renderState.allNodesVersion != CAPI.ovrAvatar2EntityRenderStateVersion.Invalid, logScope, this);
            OvrAvatarLog.Assert(renderState.visibleNodesVersion != CAPI.ovrAvatar2EntityRenderStateVersion.Invalid, logScope, this);
            OvrAvatarLog.AssertConstMessage(hierarchyVersion == renderState.hierarchyVersion
                , "hierarchyVersion mismatch", logScope, this);

            // TODO: Checking this isn't really part of "RenderUpdate", move outside this method
            if (_currentHierarchyVersion != hierarchyVersion)
            {
                // TODO: This is not really doing a partial update, it's doing a teardown+replace
                // Refresh current version to match new version
                QueueUpdateSkeleton(hierarchyVersion);

                // We can not update our current render objects w/ the nativeSDK state (out of sync)
                // Leave previous state (freeze render updates)
                return;
            }

            // TODO: We shouldn't need to stall here, this matches "legacy" behavior more accurately though
            if (_currentVisibleNodesVersion != renderState.visibleNodesVersion)
            {
                LoadSync_CheckForNewRenderables_Internal(in renderState, out var result);

                if (result.newPrimitiveIds != null)
                {
                    // Something went wrong or we're loading primitives, block for primitive update
                    QueueBuildPrimitives();

                    // Leave previous state (freeze render updates)
                    return;
                }

                // TODO: Build Renderables before primitives finish loading?
                if (result.newRenderIndices != null)
                {
                    // Build new renderables synchronously and render this frame
                    // TODO: Might be worth timeslicing?
                    using (var newIdxArray = new NativeArray<UInt32>(result.newRenderIndices.Count
                        , Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        newIdxArray.CopyFrom(result.newRenderIndices);

                        LoadSync_BuildPrimitives_Internal(in renderState
                            , newIdxArray, result.targetVisVersion);
                    }
                }

                // Update visibility flags
                UpdateVisibility(in renderState);
            }

            // Clean up nodes which are no longer in use
            // TODO: This likely isn't the best timing for this?
            UpdateAllNodes(in renderState);

            Profiler.BeginSample("OvrAvatarEntity::OnActiveRender");
            OnActiveRender(in renderState, in entityPose, hierarchyVersion);
            Profiler.EndSample();
        }

        private void QueueUpdateSkeleton(CAPI.ovrAvatar2HierarchyVersion hierarchyVersion)
        {
            _targetHierarchyVersion = hierarchyVersion;

            OvrAvatarLog.Assert(!IsLoading);
            // Reset load state
#pragma warning disable 618
            LoadState = LoadingState.Loading;
#pragma warning restore 618

            OvrAvatarManager.Instance.QueueLoadAvatar(this, () =>
            {
                _loadingRoutine = OvrAvatarManager.Instance.StartCoroutine(LoadAsync_BuildSkeletonAndPrimitives());
            });
        }
        private void QueueBuildPrimitives()
        {
            OvrAvatarLog.Assert(!IsLoading);
            // Reset load state
#pragma warning disable 618
            LoadState = LoadingState.Loading;
#pragma warning restore 618

            OvrAvatarManager.Instance.QueueLoadAvatar(this, () =>
            {
                _loadingRoutine = OvrAvatarManager.Instance.StartCoroutine(LoadAsync_BuildPrimitives());
            });
        }

        // TODO*: Better naming when the lifecycle of the entity is fixed properly
        private void OnActiveRender(in CAPI.ovrAvatar2EntityRenderState renderState, in CAPI.ovrAvatar2Pose entityPose, CAPI.ovrAvatar2HierarchyVersion hierVersion)
        {
            OvrAvatarLog.AssertConstMessage(IsCreated, "OnActiveRender while not created", logScope, this);
            OvrAvatarLog.AssertConstMessage(!IsLoading, "OnActiveRender while loading", logScope, this);

            OvrAvatarLog.Assert(_currentHierarchyVersion == hierVersion, logScope, this);
            _entityAnimator.AddNewAnimationFrame(Time.time, Time.deltaTime, entityPose, renderState);


            // Call obsolete function
#pragma warning disable 618
            Profiler.BeginSample("OvrAvatarEntity::DidRender");
            DidRender();
            Profiler.EndSample();
#pragma warning restore 618
        }

        [ObsoleteAttribute("DidRender is obsolete.", false)]
        protected virtual void DidRender() { }

        /**
         * Tear down this entity, stop all tracking and rendering
         * and free associated memory.
         * @see CreateEntity
         */
        public void Teardown()
        {
            if (CurrentState != AvatarState.None)
            {
                InvokePreTeardown();
            }

            // If not, we are shutting down and will skip some steps
            bool hasManagerInstance = OvrAvatarManager.hasInstance;

            OvrAvatarLog.LogDebug($"Tearing down entity {entityId} ", logScope, this);
            if (_loadingRoutine != null)
            {
                StopCoroutine(_loadingRoutine);
                _loadingRoutine = null;

                if (hasManagerInstance)
                {
                    OvrAvatarManager.Instance.FinishedAvatarLoad();
                }
            }

            if (hasManagerInstance)
            {
                OvrAvatarManager.Instance.RemoveLoadRequests(this);
                if (IsLoading)
                {
                    OvrAvatarManager.Instance.RemoveQueuedLoad(this);
                }

                if (IsCreated)
                {
                    if (!IsLocal)
                    {
                        SetStreamingPlayback(false);
                    }

                    OvrAvatarManager.Instance.RemoveTrackedEntity(this);
                }
            }

            if (IsCreated)
            {
                var didDestroy = DestroyNativeEntity();
                if (!didDestroy)
                {
                    OvrAvatarLog.LogWarning("Failed to destroy native entity", logScope, this);
                }
            }

#pragma warning disable 618
            LoadState = LoadingState.NotCreated;
#pragma warning restore 618
            CurrentState = AvatarState.None;

            ResetLODRange();
            TeardownLodCullingPoints();

            ShutdownAvatarLOD();

            // TODO: These will get destoryed w/ LODObjects - though being explicit would be nice
            // This trips an error in Unity currently,
            /* "Can't destroy Transform component of 'S0_L2_M1_V1_optimized_geom,0'.
             * If you want to destroy the game object, please call 'Destroy' on the game object instead.
             * Destroying the transform component is not allowed." */
            //for (int i = 0; i < _primitiveRenderables.Length; ++i)
            //{
            //    ref readonly var primRend = ref _primitiveRenderables[i];
            //    DestroyRenderable(in primRend);
            //}
            _visiblePrimitiveRenderers = Array.Empty<PrimitiveRenderData[]>();
            _skinnedRenderables.Clear();
            _meshNodes.Clear();

            _jointMonitor?.Dispose();
            _jointMonitor = null;

            DestroySkeleton();

            _currentVisibleNodesVersion = _targetVisibleNodesVersion = CAPI.ovrAvatar2EntityRenderStateVersion.Invalid;
            _currentAllNodesVersion = _targetAllNodesVersion = CAPI.ovrAvatar2EntityRenderStateVersion.Invalid;
            _currentHierarchyVersion = _targetHierarchyVersion = CAPI.ovrAvatar2HierarchyVersion.Invalid;
        }

        #endregion

        #region Asset Loading Requests

        // TODO: Rename? This is also how you load assets from cdn if it can't find it from a zip source
        /**
         * Load avatar assets from a Zip file from Unity streaming assets.
         * This function loads all levels of detail.
         * @param string[]   array of strings containing asset directories to search.
         * @see LoadAssetsFromData
         * @see LoadAssetsFromStreamingAssets
         */
        protected void LoadAssetsFromZipSource(string[] assetPaths)
        {
            LoadAssetsFromZipSource(assetPaths, CAPI.ovrAvatar2EntityLODFlags.All);
        }

        /**
         * Load avatar assets from a Zip file from Unity streaming assets.
         * @param string[]   array of strings containing asset directories to search.
         * @param lodFilter  level of detail(s) to load.
         * @see LoadAssetsFromData
         * @see LoadAssetsFromStreamingAssets
         * @see CAPI.ovrAvatar2EntityLODFlags
         */
        protected void LoadAssetsFromZipSource(string[] assetPaths, CAPI.ovrAvatar2EntityLODFlags lodFilter)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }


            bool didLoadZipAsset = false;
            foreach (var path in assetPaths)
            {
                CAPI.ovrAvatar2Result result = CAPI.OvrAvatarEntity_LoadUriWithLODFilter(entityId, $"zip://{path}", lodFilter, out var loadRequestId);
                if (result.IsSuccess())
                {
                    didLoadZipAsset = true;
                    OvrAvatarManager.Instance.RegisterLoadRequest(this, loadRequestId);
                }
                else
                {
                    OvrAvatarLog.LogError($"Failed to load asset. {result} at path: {path}", logScope, this);
                }
            }

            OvrAvatarLog.Assert(didLoadZipAsset);
            IsPendingZipAvatar = didLoadZipAsset;
            if (didLoadZipAsset)
            {
                ClearFailedLoadState();
            }
        }

        /**
        * Load avatar assets from Unity streaming assets.
        * @param string[]   array of strings containing asset directories
        *                   to search relative to *Application.streamingAssetsPath*.
        * @see LoadAssetsFromData
        * @see LoadAssetsFromZipSource
        */
        protected void LoadAssetsFromStreamingAssets(string[] assetPaths)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }

            string prefix = $"file://{Application.streamingAssetsPath}/";
            foreach (var path in assetPaths)
            {
                CAPI.ovrAvatar2Result result = CAPI.OvrAvatarEntity_LoadUri(entityId, prefix + path, out var loadRequestId);
                if (result.IsSuccess())
                {
                    ClearFailedLoadState();
                    OvrAvatarManager.Instance.RegisterLoadRequest(this, loadRequestId);
                }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Failed to load asset from streaming assets. {result} at path: {prefix + path}"
                        , logScope, this);
                }
            }
        }

        /**
         * Load avatar assets from a block of data.
         * @param data         C++ pointer to asset data.
         * @param size         byte size of asset data block.
         * @param callbackName name of function to call after data has loaded.
         * @see LoadAssetsFromZipSource
         */
        protected void LoadAssetsFromData(IntPtr data, UInt32 size, string callbackName)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }

            CAPI.ovrAvatar2Result result = CAPI.OvrAvatarEntity_LoadMemory(entityId, data, size, callbackName, out var loadRequestId);
            if (result.IsSuccess())
            {
                ClearFailedLoadState();
                OvrAvatarManager.Instance.RegisterLoadRequest(this, loadRequestId);
            }
            else
            {
                OvrAvatarLog.LogError($"Failed to load asset from data of size {size}. {result}", logScope, this);
            }
        }

        /**
         * Load avatar assets from a block of data.
         * @param data         byte array containing asset data.
         * @param callbackName name of function to call after data has loaded
         * @see LoadAssetsFromZipSource
         */
        protected void LoadAssetsFromData(byte[] data, string callbackName)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            LoadAssetsFromData(handle.AddrOfPinnedObject(), (UInt32)data.Length, callbackName);
            handle.Free();
        }

        #endregion

        #region Getters/Setters

        /**
         * Gets the current avatar view flags (first person, third person).
         * It is possible for multiple flags to be true at once.
         * @returns CAPI.ovrAvatar2EntityViewFlags designating avatar viewpoint.
         * @see CAPI.ovrAvatar2EntityViewFlags
         * @see SetActiveView
         */
        protected CAPI.ovrAvatar2EntityViewFlags GetActiveView() => _activeView;

        /**
         * Selects the avatar viewpoint (first person, third person).
         * @param CAPI.ovrAvatar2EntityViewFlags designating avatar viewpoint.
         * @see CAPI.ovrAvatar2EntityViewFlags
         * @see GetActiveView
         */
        protected void SetActiveView(CAPI.ovrAvatar2EntityViewFlags view)
        {

            var result = CAPI.ovrAvatar2Entity_SetViewFlags(entityId, _hidden ? CAPI.ovrAvatar2EntityViewFlags.None : view);
            if (result.IsSuccess())
            {
                _activeView = view;
            }
            else
            {
                OvrAvatarLog.LogError($"SetViewFlags Failed: {result}");
            }
        }

        /**
         * Gets the body parts included for this avatar.
         * It is possible for multiple manifestations to be active at once
         * @returns CAPI.ovrAvatar2EntityManifestationFlags designating body parts.
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see SetActiveManifestation
         */
        protected CAPI.ovrAvatar2EntityManifestationFlags GetActiveManifestation() => _activeManifestation;

        /**
         * Selects the body parts to include for this avatar.
         * Loading the changes will be triggered on the next Update
         * @param CAPI.ovrAvatar2EntityManifestationFlags designating avatar body parts to include
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see GetActiveManifestation
         */
        protected void SetActiveManifestation(CAPI.ovrAvatar2EntityManifestationFlags manifestation)
        {
            var result = CAPI.ovrAvatar2Entity_SetManifestationFlags(entityId, manifestation);
            if (result.IsSuccess())
            {
                _activeManifestation = manifestation;
            }
            else
            {
                OvrAvatarLog.LogError($"SetManifestationFlags Failed: {result}");
            }
        }

        #endregion

        #region Misc
        // TODO: Remove pose requirement
        protected string GetNameForNode(CAPI.ovrAvatar2NodeId nodeId, in CAPI.ovrAvatar2Pose tempPose)
        {
            return CAPI.OvrAvatar2Entity_GetNodeName(entityId, nodeId);
        }

        protected CAPI.ovrAvatar2NodeId GetNodeForType(CAPI.ovrAvatar2JointType type)
        {
            if (!_jointTypeToNodeId.TryGetValue(type, out var nodeId))
            {
                return CAPI.ovrAvatar2NodeId.Invalid;
            }
            return nodeId;
        }

        protected uint GetIndexForNode(CAPI.ovrAvatar2NodeId nodeId)
        {
            if (!_nodeToIndex.TryGetValue(nodeId, out var idx))
            {
                OvrAvatarLog.LogError($"Unable to find index for nodeId '{nodeId}'", logScope, this);
                idx = uint.MaxValue;
            }
            return idx;
        }

        protected SkeletonJoint GetSkeletonJoint(int index)
        {
            Debug.Assert(index >= 0,
                $"Index {index} out of range for Skeleton joints. Joint count is {SkeletonJointCount}");

            return GetSkeletonJoint((uint)index);
        }

        protected SkeletonJoint GetSkeletonJoint(uint index)
        {
            Debug.Assert(index < SkeletonJointCount,
                $"Index {index} out of range for Skeleton joints. Joint count is {SkeletonJointCount}");

            return _skeleton[index];
        }

        protected SkeletonJoint? GetSkeletonJoint(CAPI.ovrAvatar2JointType jointType)
        {
            var nodeId = GetNodeForType(jointType);
            if (nodeId == CAPI.ovrAvatar2NodeId.Invalid) { return null; }

            var idx = GetIndexForNode(nodeId);
            return GetSkeletonJoint(idx);
        }

        protected Transform GetSkeletonTransformByType(CAPI.ovrAvatar2JointType jointType)
        {
            // If joint monitor is disabled due to using Unity Skinning,
            // we can still provide the transform from the entity skeleton hierarchy
            if (_jointMonitor != null && _jointMonitor.TryGetTransform(jointType, out var tx))
            {
                return tx;
            }
            else
            {
                return GetSkeletonJoint(jointType)?.transform;
            }
        }

        // NOTE: This does NOT retrieve transforms for Critical Joints when Optimize Critical Joints is enabled
        protected Transform GetSkeletonTxByIndex(int index)
        {
            OvrAvatarLog.Assert(index >= 0);
            return GetSkeletonTxByIndex((uint)index);
        }

        // NOTE: This does NOT retrieve transforms for Critical Joints when Optimize Critical Joints is enabled
        protected Transform GetSkeletonTxByIndex(uint index)
        {
            if (_jointMonitor != null)
            {
                OvrAvatarLog.LogWarning(
                    "Optimize Critical Joints is enabled. GetSkeletonTxByIndex will always return null. Use GetSkeletonTransformByType instead",
                    logScope, this);
                return null;
            }

            OvrAvatarLog.Assert(index < _skeleton.Length);
            var tx = _skeleton[index].transform;
            OvrAvatarLog.Assert(tx != null, logScope, this);
            return tx;
        }

        /**
         * Get the current focal point that the avatar is looking at.
         * @returns 3D vector with gaze position.
         */
        public Vector3? GetGazePosition()
        {
            var gazePos = new CAPI.ovrAvatar2Vector3f();
            var result = CAPI.ovrAvatar2Behavior_GetGazePos(entityId, ref gazePos);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                return null;
            }

            return gazePos.ConvertSpace();
        }

        /**
         * Determines if this avatar has one or more of the given features.
         *
         * The avatar features are specified when the avatar is created.
         * @param features set of features to check for.
         * @returns true if one of the specified features is set for this avatar.
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CreateEntity
         * @see CAPI.ovrAvatar2EntityCreateInfo
         */
        public bool HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            return (_creationInfo.features & features) != 0;
        }


        /**
         * Determines if this avatar has all of the given features.
         *
         * The avatar features are specified when the avatar is created.
         * @param features set of features to check for.
         * @returns true if all of the specified features are set for this avatar.
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CreateEntity
         * @see CAPI.ovrAvatar2EntityCreateInfo
         */
        public bool HasAllFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            return (_creationInfo.features & features) == features;
        }

        private bool ForceEnableFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            var newFeatures = _creationInfo.features | features;
            bool didAdd = newFeatures != _creationInfo.features;
            if (didAdd)
            {
                OvrAvatarLog.LogVerbose($"Force enabling features {features & ~_creationInfo.features}", logScope, this);

                _creationInfo.features = newFeatures;
            }
            return didAdd;
        }

        private bool ForceDisableFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            var newFeatures = _creationInfo.features & ~features;
            bool didRemove = newFeatures != _creationInfo.features;
            if (didRemove)
            {
                OvrAvatarLog.LogVerbose($"Force disabling features {features & _creationInfo.features}", logScope, this);

                _creationInfo.features = newFeatures;
            }
            return didRemove;
        }

        #endregion

        /////////////////////////////////////////////////
        //:: Private Functions

        #region Private Helpers

        private bool QueryEntityPose(out CAPI.ovrAvatar2Pose entityPose, out CAPI.ovrAvatar2HierarchyVersion poseVersion)
        {
            var result = CAPI.ovrAvatar2Entity_GetPose(entityId, out entityPose, out poseVersion);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError($"Entity_GetPose {result}", logScope, this);
                return false;
            }

            return true;
        }

        private bool QueryEntityRenderState(out CAPI.ovrAvatar2EntityRenderState renderState)
        {
            if (!HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures.Rendering_Prims))
            {
                renderState = default;
                return false;
            }

            var result = CAPI.ovrAvatar2Render_QueryRenderState(entityId, out renderState);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError($"QueryRenderState Error: {result}", logScope, this);
                return false;
            }

            return true;
        }

        private bool QueryPrimitiveRenderState(int index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (index < 0 || index >= primitiveRenderCount)
            {
                renderState = default;
                OvrAvatarLog.LogError(
                    $"IndexOutOfRange. Tried {index} when _primitiveRenderCount is {primitiveRenderCount}");
                return false;
            }

            return QueryPrimitiveRenderState_Direct(index, out renderState);
        }

        // No range checking, used while building primitives
        private bool QueryPrimitiveRenderState_Direct(int index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (index < 0)
            {
                OvrAvatarLog.LogError($"GetPrimitiveRenderStateByIndex Invalid Index: {index}", logScope, this);
                renderState = default;
                return false;
            }
            return QueryPrimitiveRenderState_Direct((uint)index, out renderState);
        }

        private bool QueryPrimitiveRenderState_Direct(uint index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (!HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures.Rendering_Prims))
            {
                renderState = default;
                return false;
            }

            var result = CAPI.ovrAvatar2Render_GetPrimitiveRenderStateByIndex(entityId, (UInt32)index, out renderState);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"GetPrimitiveRenderStateByIndex Error: {result} at index {index}", logScope, this);
                return false;
            }

            return true;
        }

        #endregion

        #region Tracking

        /**
         * Select the body tracker to use for this avatar.
         *
         * The input body tracker has a *TrackingContext* member
         * which implements the interface between the Avatar SDK
         * and the body tracking implementation.
         *
         * @param bodyTrackingBehavior body tracking implementation.
         * @see OvrAvatarBodyTrackingBehavior
         * @see OvrAvatarBodyTrackingContextBase
         */
        public void SetBodyTracking(OvrAvatarBodyTrackingBehavior bodyTrackingBehavior)
        {
            _bodyTracking = bodyTrackingBehavior;
            if (IsCreated)
            {
                SetBodyTracking(_bodyTracking == null ? null : _bodyTracking.TrackingContext);
            }
        }


        /**
         * Select the lip sync behavior to use for this avatar.
         *
         * The input body tracker has a *LipSyncContext* member
         * which implements the interface between the Avatar SDK
         * and the lip tracking implementation.
         *
         * @param lipSyncBehavior lip sync implementation.
         * @see OvrAvatarLipSyncBehavior
         * @see OvrAvatarLipSyncContextBase
         */
        public void SetLipSync(OvrAvatarLipSyncBehavior lipSyncBehavior)
        {
            _lipSync = lipSyncBehavior;
            if (IsCreated)
            {
                SetLipSync(_lipSync == null ? null : _lipSync.LipSyncContext);
            }
        }

        private void SetBodyTracking(OvrAvatarBodyTrackingContextBase newContext)
        {
            // Special case to reduce overhead
            if (newContext is IOvrAvatarNativeBodyTracking bodyTracking)
            {
                var nativeContext = bodyTracking.NativeDataContext;
                var result2 = CAPI.ovrAvatar2Tracking_SetBodyTrackingContextNative(entityId, ref nativeContext);
                if (result2 != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetBodyTrackingContextNative failed with {result2}");
                }
            }
            else
            {
                var dataContext = newContext?.DataContext ?? new CAPI.ovrAvatar2TrackingDataContext();
                var result = CAPI.ovrAvatar2Tracking_SetBodyTrackingContext(entityId, ref dataContext);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetBodyTrackingContext failed with {result}");
                }
            }
        }


        private void SetLipSync(OvrAvatarLipSyncContextBase newContext)
        {
            // Special case to reduce overhead
            if (newContext is OvrAvatarVisemeContext visemeContext)
            {
                var ncb = visemeContext.NativeCallbacks;
                var result2 = CAPI.ovrAvatar2Tracking_SetLipSyncContextNative(entityId, ref ncb);
                if (result2 != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetLipSyncContext failed with {result2}");
                }
            }
            else
            {
                var dataContext = newContext?.DataContext ?? new CAPI.ovrAvatar2LipSyncContext();
                var result = CAPI.ovrAvatar2Tracking_SetLipSyncContext(entityId, ref dataContext);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetLipSyncContext failed with {result}");
                }
            }
        }

        #endregion
    }
}
