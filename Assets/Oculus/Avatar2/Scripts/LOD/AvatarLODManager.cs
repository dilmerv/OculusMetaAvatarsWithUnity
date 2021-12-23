using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;

#endif


namespace Oculus.Avatar2
{
    public enum AvatarLodZones : int
    {
        //TOOCLOSE,
        INTERACTION,
        HIGHAWARE,
        LOWAWARE,
        BACKGROUND,
        HINT
    }

    // This far too problematic in practice, doesn't seem worth it. Uncomment if you need this for development.
    // [ExecuteInEditMode]
    public class AvatarLODManager : OvrSingletonBehaviour<AvatarLODManager>
    {
        private const int MAX_AVATARS = 500;
        private const string logScope = "AvatarLODManager";

        private static Color[] _lodColorsCache = default;

        public static Color[] LOD_COLORS
        {
            get
            {
                if (_lodColorsCache == null)
                {
                    _lodColorsCache = new Color[] {
                        new Color(204f / 256f, 10f / 256f, 20f / 256f), // red
                        new Color(255f / 256f, 204f / 256f, 0f / 256f), // yellow
                        new Color(90f / 256f, 180f / 256f, 0f / 256f),  // green
                        new Color(0f / 256f, 80f / 256f, 255f / 256f),  // blue
                        new Color(153 / 256f, 51f / 256f, 255f / 256f), // purple
                        new Color(102f / 256f, 102f / 256f, 50f / 256f),// brown
                        new Color(102f / 256f, 51f / 256f, 0f / 256f),  // dark red
                        new Color(255f / 256f, 102f / 256f, 0f / 256f), // orange
                        new Color(51f / 256f, 204f / 256f, 204f / 256f) // cyan
                    };
                }

                return _lodColorsCache;
            }
        }

#if UNITY_EDITOR
        private EditorWindow lastWindow = null;
#endif
        [Header("Configuration")]

        [Tooltip("Maximum LOD level, 0 based. Lower LODs exhibit higher quality.")]
        public int MaxLodLevel = 4;

        [Header("Performance of the Manager (CPU Overhead)")]

        [Tooltip("Every time the manager reevaluates LODs it will do so for a subset according to this number. Set to 0 if all LODs meed reevaluation every frame.")]
        public int LODCountPerFrame = 8;

        [Tooltip("All LODs will be recalculated at this refresh period. Set to 0 to conduct manager refresh every frame.")]
        public float refreshSeconds = 0.0f;
        private float currentRefresh = 0.0f;

        [Tooltip("Cycles between the different sub functions of the manager and runs one per frame rather than all at once.")]
        public bool cycleProcessingOverFrames = true;

        private ContributingCamera currentCamera_ = new ContributingCamera() { affectsCulling = true, affectsLod = true };

        public Camera CurrentCamera => currentCamera_.camera;
        [Header("Camera Setup")]
        [UnityEngine.Serialization.FormerlySerializedAs("lodCamera")]
        [SerializeField] private Camera _lodCamera = null;

        public Camera ActiveLODCamera
        {
            get
            {
                if (!_lodCamera || !_lodCamera.isActiveAndEnabled)
                {
                    var mainCamera = Camera.main;
                    if (mainCamera)
                    {
                        _lodCamera = mainCamera;
                        OvrAvatarLog.LogDebug($"No LOD camera specified. Using `Camera.main`: {_lodCamera.name}", logScope, this);
                    }
                    else
                    {
                        _lodCamera = null;
                        OvrAvatarLog.LogWarning("No LOD camera specified. `Camera.main` is null.", logScope, this);
                    }
                }
                return _lodCamera;
            }
        }

        [Serializable]
        public class ContributingCamera
        {
            public Camera camera;
            public bool affectsCulling = true;
            public bool affectsLod = false;

            // camera properties to calculate once per update
            public Vector3 position;
            public Vector3 forward;
            public float fieldOfView;
            public readonly Plane[] frustumPlanes = new Plane[6];
        }

        [Tooltip("Extra cameras to calculate LOD distance, used for render camera to textures.")]
        [SerializeField] private List<ContributingCamera> extraCameras = new List<ContributingCamera>();

        [Tooltip("Empirically determined avatar bounding box to use for culling purposes.")]
        public Bounds frustumBounds_ = new Bounds(Vector3.zero, new Vector3(0.35f, 1f, 0.35f));

        // TODO: Fetch this value from the actual camera
        public float CameraHeight = 1.6f;

        public Action<AvatarLOD, bool> CulledChangedEvent;   // events are also available from each AvatarLOD. Use this event when the avatarLOD is not known by the caller.

        public Action AvatarLODCountChangedEvent;            // the config is held outside the LOD system. When the number of AvatarLODs managed by AvatarLODManager changes, this events gives the player system a chance to update the config.
        public Action<AvatarLODManager> UpdateConfigEvent;   // the config is held outside the LOD system. Everytime the ssytem needs to update, this function must accomodate

        private bool prevDisplayAgeLabels_ = false;
        private bool prevDisplayLODLabels_ = false;
        private bool prevDisplayLodColors_ = false;

        public float screenPercentToUpdateImportanceCurvePower = 0.25f;
        public float screenPercentToUpdateImportanceCurveMultiplier = 1.5f;

        [Header("Joint Based Culling")]

        public bool cullingDisablesParentGameObject = false;
        public bool cullingDisablesChildrenGameObjects = true;

        [SerializeField] private CAPI.ovrAvatar2JointType _jointTypeToCenterOn = CAPI.ovrAvatar2JointType.Hips;

        [SerializeField] private CAPI.ovrAvatar2JointType[] _jointTypesToCullOn = { CAPI.ovrAvatar2JointType.Head, CAPI.ovrAvatar2JointType.LeftHandWrist, CAPI.ovrAvatar2JointType.RightHandWrist };

        public CAPI.ovrAvatar2JointType JointTypeToCenterOn => _jointTypeToCenterOn;
        public IReadOnlyList<CAPI.ovrAvatar2JointType> JointTypesToCullOn => _jointTypesToCullOn;

        [Header("Dynamic Performance")]

        [Tooltip("With dynamic performance, all LODs in front of the camera will be modulated based on a total sum of vertices.")]
        public bool enableDynamicPerformance = true;

        public float dynamicLodWantedLogScale = 1.3f;
        public int numDynamicLods = 2;
        [Tooltip("Number of rendered avatar triangles to target, may be exceeded when all avatars are at lowest quality LOD")]
        public int dynamicLodMaxTrianglesToRender = 90000;

        [Tooltip("Maximum number of avatar animation updates per frame")]
        public int maxActiveAvatars = 5;
        [Tooltip("Number of avatar vertices to skin per frame")]
        public int maxVerticesToSkin = 30000;

        [Header("Dynamic Streaming")]
        public bool enableDynamicStreaming = false;
        public long[] dynamicStreamLodBitsPerSecond = new long[OvrAvatarEntity.StreamLODCount] { 0, 0, 0, 0 };
        public long[] dynamicStreamLodMaxDistance = new long[OvrAvatarEntity.StreamLODCount - 1] { 1, 3, 9 };
        public long dynamicStreamLodMaxBitsPerSecond = 3000000;

        protected override void Initialize()
        {
            base.Initialize();

            var lods = UnityEngine.Object.FindObjectsOfType<AvatarLOD>();
            foreach (AvatarLOD lod in lods)
            {
                AddLOD(lod);
            }
        }

        private List<AvatarLOD> inactiveAvatarLods = new List<AvatarLOD>(MAX_AVATARS); // all avatars, declares capacity not size

        private List<AvatarLOD> avatarLods = new List<AvatarLOD>(MAX_AVATARS); // only active avatars, declares capacity not size

        private List<AvatarLOD> avatarLodsPerFrame; // this will only be used as a reference to either avatarLods or avatarLodsPerFrameSubList

        private readonly List<AvatarLOD> lodsCulled = new List<AvatarLOD>(MAX_AVATARS); // only active avatars, declares capacity not size
        private readonly List<AvatarLOD> lodsAppeared = new List<AvatarLOD>(MAX_AVATARS); // only active avatars, declares capacity not size
        private readonly List<AvatarLOD> lodsVisible = new List<AvatarLOD>(MAX_AVATARS); // only active avatars, declares capacity not size
        private readonly List<AvatarLOD> lodsToProcess = new List<AvatarLOD>(MAX_AVATARS); // only active avatars, declares capacity not size
        private int roundRobinLodIndex = 0;

        private List<AvatarLOD> dynamicLodPriorityQueue = new List<AvatarLOD>(MAX_AVATARS); // working buffer for dynamic LOD priority queue

        public int AvatarLODsCount => avatarLods.Count;

        [Header("First Person Avatar")]
        public AvatarLOD firstPersonAvatarLod;
        public int firstPersonAvatarLodLevel = 0;
        public float firstPersonUpdateImportance = 10000;

        [Header("Debugging")]

        [Tooltip("This should reference a Unity prefab with a GUI canvas inside. This GUI is used to render numbers in the world space.")]
        [SerializeField] internal GameObject avatarLodDebugCanvas = null;

        [System.Serializable]
        public class Debug
        {
            public bool sceneViewCamera = true;
            public bool displayLODLabels = false;
            public bool displayAgeLabels = false;
            public bool displayLODColors = false;
            public Vector3 displayLODLabelOffset = new Vector3(0.3f, 0.0f, 0.3f);
        }

        [SerializeField] public AvatarLODManager.Debug debug = new AvatarLODManager.Debug();

        // Since all 3 of these fields are public, this function only remains for backwards compatibility.
        public void SetConfig(float refreshSec, int LODsPerFrame, int firstPersonLodLevel)
        {
            refreshSeconds = refreshSec;
            LODCountPerFrame = LODsPerFrame;
            firstPersonAvatarLodLevel = firstPersonLodLevel;
        }

        public void AddExtraCamera(Camera camera, bool affectsLod = false, bool affectsCulling = true)
        {
            if (camera && extraCameras.Find(x => x.camera == camera) == null)
            {
                extraCameras.Add(new ContributingCamera() { camera = camera, affectsCulling = affectsCulling, affectsLod = affectsLod });
            }
        }

        public void RemoveExtraCamera(Camera camera)
        {
            if (camera)
            {
                extraCameras.RemoveAll(x => x.camera == camera);
            }
        }

        private bool AvatarIsInactiveByParentHierarchy(AvatarLOD avatarLod)
        {
            return avatarLod.transform.parent != null && !avatarLod.transform.parent.gameObject.activeInHierarchy;
        }

        public void AddLOD(AvatarLOD lod)
        {
            if (inactiveAvatarLods.Count == MAX_AVATARS)
            {
                OvrAvatarLog.LogError($"Attempting to add more than {MAX_AVATARS} Avatars to LODManager", logScope, this);
                return;
            }

            if (AvatarIsInactiveByParentHierarchy(lod))
            {
                if (inactiveAvatarLods.Find(x => x == lod) == null)
                {
                    inactiveAvatarLods.Add(lod);
                    AvatarLODCountChangedEvent?.Invoke();
                }
            }
            else
            {
                if (avatarLods.Find(x => x == lod) == null)
                {
                    avatarLods.Add(lod);
                    lodsCulled.Add(lod);
                    AvatarLODCountChangedEvent?.Invoke();
                }
            }
        }

        public static void RemoveLOD(AvatarLOD lod)
        {
            if (!AvatarLODManager.shuttingDown && Instance)
            {
                Instance.inactiveAvatarLods.Remove(lod);
                Instance.avatarLods.Remove(lod);
                Instance.lodsCulled.Remove(lod);
                Instance.lodsVisible.Remove(lod);
                Instance.AvatarLODCountChangedEvent?.Invoke();
            }
        }

        public static void ParentStateChanged(AvatarLOD lod)
        {
            if (!AvatarLODManager.shuttingDown && Instance)
            {
                // Puts the LOD in the right list (active or inactive) based on parent
                RemoveLOD(lod);
                if (lod != null)
                {
                    Instance.AddLOD(lod);
                }
            }
        }

        private enum LodManagerFunction : int
        {
            FIRST,
            REFRESH_SCREEN_PERCENTS_AND_IMPORTANCE = FIRST,
            REFRESH_DYNAMIC_GEOMETRIC_LODS,
            REFRESH_DYNAMIC_STREAM_LODS,
            MAX
        }
        private LodManagerFunction cyclingFunction_;

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        public void UpdateInternal()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Profiler.BeginSample("AvatarLODManager::UpdateInternal");

            currentRefresh += Time.deltaTime;
            if (currentRefresh > refreshSeconds)
            {
                RefreshImportanceBudget();
                RefreshCameras();

                CullAndCreateProcessListForFrame();

                // The following 3 functions are either all called sequentially every frame,
                // or if "cycleProcessingOverFrames" is true, they are spread over frames.
                // Under initial measurements, each of these 3 functions takes about the same amount of time.
                // However, RefreshScreenPercentsAndImportance can be modulated be setting LODCountPerFrame.
                if (!cycleProcessingOverFrames || cyclingFunction_ == LodManagerFunction.REFRESH_SCREEN_PERCENTS_AND_IMPORTANCE)
                {
                    RefreshScreenPercentsAndImportance();
                }
                if (!cycleProcessingOverFrames || cyclingFunction_ == LodManagerFunction.REFRESH_DYNAMIC_GEOMETRIC_LODS)
                {
                    RefreshDynamicGeometricLods();
                }
                if (!cycleProcessingOverFrames || cyclingFunction_ == LodManagerFunction.REFRESH_DYNAMIC_STREAM_LODS)
                {
                    RefreshDynamicStreamLods();
                }
                if (cycleProcessingOverFrames)
                {
                    cyclingFunction_++;
                    if(cyclingFunction_ >= LodManagerFunction.MAX)
                    {
                        cyclingFunction_ = LodManagerFunction.FIRST;
                    }
                }

                RefreshDebugDisplays();

                currentRefresh = 0.0f;
            }

            Profiler.EndSample();   // "AvatarLODManager::UpdateInternal"
        }

        private static float GetScreenPercent(float height, float distance, float fieldOfView)
        {
            return (Mathf.Atan((height / 2) / distance) * 2 * Mathf.Rad2Deg) / fieldOfView;
        }

        public bool CullAvatar(AvatarLOD avatarLod)
        {
            // Disable the bodies of all avatars that are behind all Cameras in the scene
            // Can update this to use angle or frustum WorldToScreenPoint for more accuracy
            bool inFront = IsLodInFrontAnyCamera(avatarLod);

            // TODO: This should happen in avatarLod?
            if (cullingDisablesParentGameObject)
            {
                avatarLod.gameObject.SetActive(inFront);
            }

            bool culled = !inFront;

            bool culledChanged = avatarLod.SetCulled(culled);
            if (culledChanged)
            {
                CulledChangedEvent?.Invoke(avatarLod, culled);
            }

            return culled;
        }

        private bool IsLodInFrontAnyCamera(AvatarLOD avatarLod)
        {
            // The VR camera (or possible Scene cam in Editor)
            if (currentCamera_.camera != null && AreAnyPointsInFrustum(currentCamera_, avatarLod))
            {
                return true;
            }

            // Extra cameras
            foreach (var cc in extraCameras)
            {
                if (cc.affectsCulling && cc.camera != currentCamera_.camera)
                {
                    if (AreAnyPointsInFrustum(cc, avatarLod))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPointInFrustum(ContributingCamera cc, Vector3 point)
        {
            frustumBounds_.center = point;
            return GeometryUtility.TestPlanesAABB(cc.frustumPlanes, frustumBounds_);
        }

        private bool AreAnyPointsInFrustum(ContributingCamera camera, AvatarLOD avatarLod)
        {
            bool areAnyPointsInCameraFrustum = false;

            // first test the centerXform point, as that is the most common and fallback in case no more are set
            frustumBounds_.center = avatarLod.centerXform.position;
            areAnyPointsInCameraFrustum |= GeometryUtility.TestPlanesAABB(camera.frustumPlanes, frustumBounds_);

            // next, check all other culling points that will be typically found at the extreme leaf nodes of the skeleton
            if (!areAnyPointsInCameraFrustum && avatarLod.extraXforms != null)
            {
                foreach (var extraXform in avatarLod.extraXforms)
                {
                    frustumBounds_.center = extraXform.position;
                    areAnyPointsInCameraFrustum |= GeometryUtility.TestPlanesAABB(camera.frustumPlanes, frustumBounds_);
                    if (areAnyPointsInCameraFrustum)
                    {
                        break;
                    }
                }
            }

            return areAnyPointsInCameraFrustum;
        }

        private void RefreshScreenPercentsAndImportance()
        {
            Profiler.BeginSample("AvatarLODManager::RefreshScreenPercentsAndImportance");

            if (!(currentCamera_.camera is null))
            {
                foreach (var avatarLod in avatarLodsPerFrame)
                {
                    avatarLod.distance = (Vector3.Distance(currentCamera_.position, avatarLod.centerXform.position));

                    float realHeight = CameraHeight * avatarLod.transform.lossyScale.x;
                    float screenPercent = GetScreenPercent(realHeight, avatarLod.distance, currentCamera_.fieldOfView);

                    foreach (ContributingCamera cc in extraCameras)
                    {
                        if (cc.camera != currentCamera_.camera && cc.affectsLod)
                        {
                            float extraCameraDist = (Vector3.Distance(cc.position, avatarLod.centerXform.position));
                            float extraCameraScreenPercent = GetScreenPercent(realHeight, extraCameraDist, cc.fieldOfView);

                            if (extraCameraScreenPercent > screenPercent)
                            {
                                screenPercent = extraCameraScreenPercent;
                            }
                        }
                    }

                    avatarLod.screenPercent = screenPercent;

                    // convert screenPercent to animation importance scale using:
                    //  importance = screenPercent ^ CurvePower * CurveMultiplier
                    avatarLod.updateImportance = Mathf.Pow(screenPercent, screenPercentToUpdateImportanceCurvePower) *
                                                 screenPercentToUpdateImportanceCurveMultiplier;

                    var avatarEntity = avatarLod.GetComponentInParent<OvrAvatarEntity>();
                    avatarEntity.UpdateAvatarLODOverride();
                    avatarEntity.SendImportanceAndCost();
                    avatarEntity.TrackUpdateAge();
                }
            }
            else
            {
                foreach (var avatarLod in avatarLodsPerFrame)
                {
                    avatarLod.distance = 0;
                    avatarLod.screenPercent = 0;
                    avatarLod.updateImportance = 0;

                    var avatarEntity = avatarLod.GetComponentInParent<OvrAvatarEntity>();
                    avatarEntity.UpdateAvatarLODOverride();
                    avatarEntity.SendImportanceAndCost();
                    avatarEntity.TrackUpdateAge();
                }
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshScreenPercentsAndImportance
        }

        private void RefreshDebugDisplays()
        {
            Profiler.BeginSample("AvatarLODManager::RefreshDebugDisplays");

            if (debug.displayLODLabels || debug.displayLODLabels != prevDisplayLODLabels_)
            {
                foreach (var avatarLod in avatarLods)
                {
                    avatarLod.UpdateDebugLabel();
                }

                prevDisplayLODLabels_ = debug.displayLODLabels;
            }

            if (debug.displayLODColors != prevDisplayLodColors_)
            {
                foreach (var avatarLod in avatarLods)
                {
                    avatarLod.ForceUpdateLOD<AvatarLODActionGroup>();
                }

                prevDisplayLodColors_ = debug.displayLODColors;
            }

            if (debug.displayAgeLabels || debug.displayAgeLabels != prevDisplayAgeLabels_)
            {
                foreach (var avatarLod in avatarLods)
        {
                    avatarLod.UpdateDebugLabel();
                }

                prevDisplayAgeLabels_ = debug.displayAgeLabels;
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshDebugDisplays
        }

        public void CullAndCreateProcessListForFrame()
        {
            Profiler.BeginSample("AvatarLODManager::CullAndCreateProcessListForFrame");

            if (LODCountPerFrame <= 0)
            {
                avatarLodsPerFrame = lodsToProcess;

                lodsToProcess.Clear();
                foreach (var avatarLod in avatarLods)
                {
                    bool avatarCulled = CullAvatar(avatarLod);
                    if (avatarCulled)
                    {
                        if (cullingDisablesChildrenGameObjects)
                        {
                            avatarLod.Level = -1;
                        }
                    }
                    else
                    {
                        lodsToProcess.Add(avatarLod);
                    }
                }
        }
            else
            {
                avatarLodsPerFrame = lodsToProcess;

                // Each frame that passes we need to fill up lodsToProcess from scratch
                int processCountPerFrame = Math.Min(LODCountPerFrame, avatarLods.Count);
                lodsToProcess.Clear();
                lodsAppeared.Clear();

                // first fill up lodsToProcess with anything newly appeared
                for(int i = lodsCulled.Count-1; i>=0; i--)
                {
                    var avatarLod = lodsCulled[i];
                    bool lodCulled = CullAvatar(avatarLod);
                    // if (cullingDisablesChildrenGameObjects)
                    if (!lodCulled) // if it became visible
                    {
                        lodsCulled.Remove(avatarLod);
                        lodsAppeared.Add(avatarLod);
                        lodsToProcess.Add(avatarLod);   // always add newly unculled LODs to the process list
                        processCountPerFrame--;
                    }
                }

                // if any space is left in the list for processing, we'll fill it up with round robin members from the lodsVisible list
                processCountPerFrame = Math.Min(processCountPerFrame, lodsVisible.Count);
                if (roundRobinLodIndex >= lodsVisible.Count)
                {
                    roundRobinLodIndex = 0;
                }
                int lodsVisibleToConsume = lodsVisible.Count;
                while (processCountPerFrame > 0 && lodsVisibleToConsume > 0)
                {
                    var avatarLod = lodsVisible[roundRobinLodIndex % lodsVisible.Count];
                    bool lodCulled = CullAvatar(avatarLod);
                    if (lodCulled)
                    {
                        // if (cullingDisablesChildrenGameObjects)
                        avatarLod.Level = -1;
                        lodsVisible.Remove(avatarLod);
                        lodsCulled.Add(avatarLod);
                    }
                    else
                    {
                        lodsToProcess.Add(avatarLod);
                        roundRobinLodIndex++;
                        processCountPerFrame--;
                    }
                    lodsVisibleToConsume--;
                }

                // at this point, the lodsToProcess list is ready for this frame, but insert the lodsAppeared into the lodsVisible for next frame
                if (lodsVisible.Count > 0 && roundRobinLodIndex > 0)
                {
                    int lastSpace = (roundRobinLodIndex - 1) % lodsVisible.Count;
                    foreach (var avatarLod in lodsAppeared)
                    {
                        lodsVisible.Insert(lastSpace, avatarLod);
                        lastSpace++;
                    }
                }
                else
                {
                    foreach (var avatarLod in lodsAppeared)
                    {
                        lodsVisible.Add(avatarLod);
                    }
                }

                // Now decide if the round robin index will be set before of after those just appeared
                roundRobinLodIndex += lodsAppeared.Count;
            }

            Profiler.EndSample();   // AvatarLODManager::CullAndCreateProcessListForFrame
        }

        private void RefreshImportanceBudget()
        {
            Profiler.BeginSample("AvatarLODManager::RefreshImportanceBudget");

            if (OvrAvatarManager.initialized)
            {
                if (enableDynamicPerformance)
                {
                    CAPI.ovrAvatar2Importance_SetBudget((UInt32)maxActiveAvatars, (UInt32)maxVerticesToSkin);
                }
                else
                {
                    CAPI.ovrAvatar2Importance_SetBudget(0, 0);
                }
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshImportanceBudget
        }

        private void RefreshDynamicGeometricLods()
        {
            Profiler.BeginSample("AvatarLODManager::RefreshDynamicGeometricLods");

            dynamicLodPriorityQueue.Clear();

            int trianglesToRender = 0;
            foreach (var avatarLod in avatarLods) // cannot use avatarLodsPerFrame in loop since it is summing all avatars in front of camera
            {
                if (avatarLod.triangleCounts.Count <= 0)
                {
                    continue;
                }

                avatarLod.wantedLevel = -1;
                avatarLod.lodImportance = -1;
                avatarLod.dynamicLevel = -1;

                if (ReferenceEquals(avatarLod, firstPersonAvatarLod))
                {
                    avatarLod.wantedLevel = firstPersonAvatarLodLevel;
                    avatarLod.Level = firstPersonAvatarLodLevel;
                    avatarLod.updateImportance = firstPersonUpdateImportance;
                    trianglesToRender += avatarLod.triangleCounts[firstPersonAvatarLodLevel];
                    continue;
                }

                if (cullingDisablesChildrenGameObjects && CullAvatar(avatarLod))
                {
                    avatarLod.Level = -1;
                    continue;
                }

                var lodFactor = -Mathf.Log(Mathf.Clamp(dynamicLodWantedLogScale * avatarLod.screenPercent, float.Epsilon, 1f));
                var dir = Vector3.Normalize(avatarLod.centerXform.position - currentCamera_.position);
                var gazeBoost = Mathf.Clamp(Vector3.Dot(currentCamera_.forward, dir), float.Epsilon, 1f);
                const float gazeEpsilon = 1e-3f;
                avatarLod.lodImportance = (1f + gazeBoost) / Mathf.Max(lodFactor, gazeEpsilon);

                avatarLod.wantedLevel = Mathf.Clamp(Mathf.RoundToInt(lodFactor), avatarLod.minLodLevel, avatarLod.maxLodLevel);
                avatarLod.dynamicLevel = avatarLod.CalcAdjustedLod(avatarLod.wantedLevel + numDynamicLods);
                if (avatarLod.dynamicLevel == -1)
                {
                    avatarLod.Level = -1;
                    continue;
                }

                // NOTE numDynamicLods currently assumes that all LOD levels are loaded
                trianglesToRender += avatarLod.triangleCounts[avatarLod.dynamicLevel];

                if (avatarLod.dynamicLevel > avatarLod.wantedLevel)
                {
                    dynamicLodPriorityQueue.AddSorted(avatarLod, AvatarLodImportanceComparer.Instance);
                }
                else
                {
                    avatarLod.Level = avatarLod.dynamicLevel;
                }
            }

            int lastTrisToRender = trianglesToRender;

            // increase level starting with most import until wantedLevel is reached
            while (trianglesToRender < dynamicLodMaxTrianglesToRender)
            {
                for (int i = 0; i < dynamicLodPriorityQueue.Count; i++)
                {
                    var avatarLod = dynamicLodPriorityQueue[i];
                    if (avatarLod is null)
                    {
                        continue;
                    }

                    var prevLod = avatarLod.GetPreviousLod(avatarLod.dynamicLevel);
                    if (prevLod != -1 && prevLod >= (avatarLod.wantedLevel - numDynamicLods))
                    {
                        var nextLevelTriIncrease = avatarLod.triangleCounts[prevLod] - avatarLod.triangleCounts[avatarLod.dynamicLevel];
                        if (trianglesToRender + nextLevelTriIncrease <= dynamicLodMaxTrianglesToRender)
                        {
                            trianglesToRender += nextLevelTriIncrease;
                            avatarLod.dynamicLevel = prevLod;
                            continue;
                        }
                    }

                    // no longer a candidate for adjustment
                    avatarLod.Level = avatarLod.dynamicLevel;
                    dynamicLodPriorityQueue[i] = null;
                }

                if (lastTrisToRender == trianglesToRender)
                {
                    break;
                }

                lastTrisToRender = trianglesToRender;
            }

            foreach (var avatarLod in dynamicLodPriorityQueue)
            {
                if (!(avatarLod is null))
                {
                    avatarLod.Level = avatarLod.dynamicLevel;
                }
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshDynamicGeometricLods
        }

        private void RefreshDynamicStreamLods()
        {
            if (!enableDynamicStreaming) { return; }

            Profiler.BeginSample("AvatarLODManager::RefreshDynamicStreamLods");

            dynamicLodPriorityQueue.Clear();

            long bandwidthRequired = 0;
            foreach (var avatarLod in avatarLods)
            {
                var avatar = avatarLod.GetComponentInParent<OvrAvatarEntity>();
                if (avatar.IsLocal)
                {
                    avatarLod.dynamicStreamLod = OvrAvatarEntity.StreamLOD.Full;
                    continue;
                }

                avatarLod.dynamicStreamLod = OvrAvatarEntity.StreamLOD.Low;

                if (avatarLod.Level == -1 || avatarLod.distance > dynamicStreamLodMaxDistance[(int)OvrAvatarEntity.StreamLOD.Medium])
                {
                    // default to low
                    bandwidthRequired += dynamicStreamLodBitsPerSecond[(int)OvrAvatarEntity.StreamLOD.Low];
                    avatar.ForceStreamLod(OvrAvatarEntity.StreamLOD.Low);
                }
                else
                {
                    dynamicLodPriorityQueue.AddSorted(avatarLod, AvatarLodImportanceComparer.Instance);
                }
            }

            for (int prevStreamLod = (int)OvrAvatarEntity.StreamLOD.Medium; prevStreamLod >= (int)OvrAvatarEntity.StreamLOD.High; --prevStreamLod)
            {
                var bandwidthIncrease = dynamicStreamLodBitsPerSecond[prevStreamLod] - dynamicStreamLodBitsPerSecond[prevStreamLod + 1];
                if (bandwidthRequired + bandwidthIncrease > dynamicStreamLodMaxBitsPerSecond)
                {
                    break;
                }

                for (int i = 0; i < dynamicLodPriorityQueue.Count; i++)
                {
                    var avatarLod = dynamicLodPriorityQueue[i];

                    if (avatarLod.distance < dynamicStreamLodMaxDistance[prevStreamLod])
                    {
                        avatarLod.dynamicStreamLod = (OvrAvatarEntity.StreamLOD)prevStreamLod;
                        bandwidthRequired += bandwidthIncrease;

                        if (bandwidthRequired + bandwidthIncrease > dynamicStreamLodMaxBitsPerSecond)
                        {
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < dynamicLodPriorityQueue.Count; i++)
            {
                var avatarLod = dynamicLodPriorityQueue[i];
                var avatar = avatarLod.GetComponentInParent<OvrAvatarEntity>();
                avatar.ForceStreamLod(avatarLod.dynamicStreamLod);
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshDynamicStreamLods
        }

        private Camera FindVRCamera()
        {
            for (int i = 0; i < Camera.allCamerasCount; i++)
            {
                if (Camera.allCameras[i].stereoTargetEye == StereoTargetEyeMask.Both)
                    return Camera.allCameras[i];
            }

            return null;
        }

        private void RefreshCameras()
        {
            Profiler.BeginSample("AvatarLODManager::RefreshCameras");

            // This is all the explicit cameras we tell the system to look at (active or not)
            extraCameras.RemoveAll(x => (x.camera == null));

            // If running editor, allow Scene camera to drive LODs when it has focus
#if UNITY_EDITOR
            if (debug.sceneViewCamera)
            {
                if (EditorWindow.focusedWindow != null && EditorWindow.focusedWindow != lastWindow)
                {
                    if (EditorWindow.focusedWindow is UnityEditor.SceneView)
                    {
                        currentCamera_.camera = UnityEditor.SceneView.lastActiveSceneView.camera;
                    }
                    else if (EditorWindow.focusedWindow.titleContent.text == "Game")
                    {
                        currentCamera_.camera = null; // cam will be set below
                    }

                    /*
                     * else if (EditorWindow.focusedWindow is UnityEditor.GameView) {
                     * Dammit UnityEditor.GameView is internal, can't reference class!
                     * How can I switch when a certain view gets focus without GameView?
                     * Test the title above (not great, but should be OK because it only happens when
                     * a new window gets focus and only ever in Editor)
                    }
                    */
                    lastWindow = EditorWindow.focusedWindow;
                }
            }
            // Scene view camera is always considered inactive, so we'll make an editor-only exception here.
            if (!currentCamera_.camera || (!currentCamera_.camera.isActiveAndEnabled && currentCamera_.camera != UnityEditor.SceneView.lastActiveSceneView.camera))
            {
#endif
                currentCamera_.camera = ActiveLODCamera;

                if (!currentCamera_.camera)
                {
                    currentCamera_.camera = FindVRCamera();
                }
#if UNITY_EDITOR
            }
#endif

            ComputeCameraProperties(currentCamera_);

            for (int i = 0; i < extraCameras.Count; i++)
            {
                ComputeCameraProperties(extraCameras[i]);
            }

            Profiler.EndSample(); // AvatarLODManager::RefreshCameras
        }

        private void ComputeCameraProperties(ContributingCamera cc)
        {
            if (cc.camera != null)
            {

                if (cc.affectsLod)
                {
                    cc.fieldOfView = cc.camera.fieldOfView;
                    cc.position = cc.camera.transform.position;
                    cc.forward = cc.camera.transform.forward;
                }

                if (cc.affectsCulling)
                {
                    GeometryUtility.CalculateFrustumPlanes(cc.camera, cc.frustumPlanes);
                }
            }
        }

        private sealed class AvatarLodImportanceComparer : IComparer<AvatarLOD>
        {
            private static AvatarLodImportanceComparer _instance = default;
            public static AvatarLodImportanceComparer Instance
                => _instance ?? (_instance = new AvatarLodImportanceComparer());

            public int Compare(AvatarLOD x, AvatarLOD y)
            {
                var xVal = x != null ? x.lodImportance : float.NaN;
                var yVal = y != null ? y.lodImportance : float.NaN;
                return yVal.CompareTo(xVal);
            }
        }
    }
}
