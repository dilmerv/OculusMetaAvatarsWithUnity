using AOT;
using System;

namespace Oculus.Avatar2
{
    ///
    /// C# wrapper around OvrBody stand alone solver
    ///
    public sealed class OvrAvatarBodyTrackingContext : OvrAvatarBodyTrackingContextBase, IOvrAvatarNativeBodyTracking
    {
        private IntPtr _context;
        private IOvrAvatarHandTrackingDelegate _handTrackingDelegate;
        private IOvrAvatarInputTrackingDelegate _inputTrackingDelegate;
        private IOvrAvatarInputControlDelegate _inputControlDelegate;
        private readonly CAPI.ovrAvatar2TrackingDataContext? _callbacks;
        private readonly OvrAvatarTrackingHandsState _handState = new OvrAvatarTrackingHandsState();
        private OvrAvatarInputControlState _inputControlState = new OvrAvatarInputControlState();
        private OvrAvatarInputTrackingState _inputTrackingState = new OvrAvatarInputTrackingState();
        private readonly CAPI.ovrAvatar2TrackingDataContextNative _nativeContext;

        CAPI.ovrAvatar2TrackingDataContextNative IOvrAvatarNativeBodyTracking.NativeDataContext
        {
            get => _nativeContext;
        }

        public IntPtr BodyTrackingContextPtr => _context;

        public IOvrAvatarHandTrackingDelegate HandTrackingDelegate
        {
            get => _handTrackingDelegate;
            set
            {
                _handTrackingDelegate = value ?? OvrAvatarManager.Instance.DefaultHandTrackingDelegate;

                if (_handTrackingDelegate is IOvrAvatarNativeHandDelegate nativeHandDelegate)
                {
                    var nativeContext = nativeHandDelegate.NativeContext;
                    var nativeResult = CAPI.ovrAvatar2Body_SetHandTrackingContextNative(_context, ref nativeContext);
                    if (nativeResult != CAPI.ovrAvatar2Result.Success)
                    {
                        OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetHandTrackingContextNative failed with {nativeResult}");
                    }
                }
                else
                {
                    // Set hand callbacks
                    var handContext = new CAPI.ovrAvatar2HandTrackingDataContext
                    {
                        context = new IntPtr(id),
                        handTrackingCallback = HandTrackingCallback
                    };
                    // Set an empty callback if there is no delegate
                    if (_handTrackingDelegate == null)
                    {
                        handContext = new CAPI.ovrAvatar2HandTrackingDataContext();
                    }

                    var result = CAPI.ovrAvatar2Body_SetHandTrackingContext(_context, ref handContext);
                    if (result != CAPI.ovrAvatar2Result.Success)
                    {
                        OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetHandTrackingContext failed with {result}");
                    }
                }
            }
        }

        public IOvrAvatarInputTrackingDelegate InputTrackingDelegate
        {
            get => _inputTrackingDelegate;
            set
            {
                _inputTrackingDelegate = value;

                {
                    var inputContext = new CAPI.ovrAvatar2InputTrackingContext
                    {
                        context = new IntPtr(id),
                        inputTrackingCallback = InputTrackingCallback
                    };

                    if (_inputTrackingDelegate == null)
                    {
                        inputContext = new CAPI.ovrAvatar2InputTrackingContext();
                    }

                    var result = CAPI.ovrAvatar2Body_SetInputTrackingContext(_context, ref inputContext);
                    if (result != CAPI.ovrAvatar2Result.Success)
                    {
                        OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetInputTrackingContext failed with {result}");
                    }
                }
            }
        }

        public OvrAvatarInputTrackingState InputTrackingState { get => _inputTrackingState; }

        public IOvrAvatarInputControlDelegate InputControlDelegate
        {
            get => _inputControlDelegate;
            set
            {
                _inputControlDelegate = value;

                {
                    var inputContext = new CAPI.ovrAvatar2InputControlContext
                    {
                        context = new IntPtr(id),
                        inputControlCallback = InputControlCallback
                    };

                    if (_inputControlDelegate == null)
                    {
                        inputContext = new CAPI.ovrAvatar2InputControlContext();
                    }

                    var result = CAPI.ovrAvatar2Body_SetInputControlContext(_context, ref inputContext);
                    if (result != CAPI.ovrAvatar2Result.Success)
                    {
                        OvrAvatarLog.LogError($"ovrAvatar2Tracking_SetInputControlContext failed with {result}");
                    }
                }
            }
        }

        public OvrAvatarInputControlState InputControlState { get => _inputControlState; }

        public static OvrAvatarBodyTrackingContext Create(bool runAsync)
        {
            OvrAvatarBodyTrackingContext context = null;
            try
            {
                context = new OvrAvatarBodyTrackingContext(runAsync);
            }
            catch (Exception)
            {
                context?.Dispose();
                context = null;
            }

            return context;
        }

        private OvrAvatarBodyTrackingContext(bool runAsync)
        {
            var result = CAPI.ovrAvatar2Body_CreateProvider(runAsync ? CAPI.ovrAvatar2BodyProviderCreateFlags.RunAsync : 0, out _context);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2Body_CreateProvider failed with {result}");
                // Not sure which exception type is best
                throw new Exception("Failed to create body tracking context");
            }


            HandTrackingDelegate = OvrAvatarManager.Instance.DefaultHandTrackingDelegate;


            _callbacks = CreateBodyDataContext();

            result = CAPI.ovrAvatar2Body_InitializeDataContextNative(_context, out var nativeContext);
            if (result == CAPI.ovrAvatar2Result.Success)
            {
                _nativeContext = nativeContext;
            }
            else
            {
                OvrAvatarLog.LogError($"ovrAvatar2Body_InitializeDataContextNative failed with {result}");
            }
        }

        public void SetTransformOffset(CAPI.ovrAvatar2BodyMarkerTypes type, ref CAPI.ovrAvatar2Transform offset)
        {
            var result = CAPI.ovrAvatar2Body_SetOffset(_context, type, ref offset);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2Body_SetOffset failed with {result}");
            }
        }

        private CAPI.ovrAvatar2TrackingDataContext? CreateBodyDataContext()
        {
            var trackingContext = new CAPI.ovrAvatar2TrackingDataContext();
            var result = CAPI.ovrAvatar2Body_InitializeDataContext(_context, ref trackingContext);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2Body_InitializeDataContext failed with {result}");
                return null;
            }

            return trackingContext;
        }

        private void ReleaseUnmanagedResources()
        {
            if (_context == IntPtr.Zero) return;
            // Release unmanaged resources here
            var result = CAPI.ovrAvatar2Body_DestroyProvider(_context);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2Body_DestroyProvider failed with {result}");
            }

            _context = IntPtr.Zero;
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            base.Dispose(disposing);
        }

        [MonoPInvokeCallback(typeof(CAPI.HandStateCallback))]
        private static bool HandTrackingCallback(out CAPI.ovrAvatar2HandTrackingState handsState, IntPtr context)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(context);
                if (bodyContext?._handTrackingDelegate != null &&
                    bodyContext._handTrackingDelegate.GetHandData(bodyContext._handState))
                {
                    handsState = bodyContext._handState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            handsState = new CAPI.ovrAvatar2HandTrackingState();
            return false;
        }


        [MonoPInvokeCallback(typeof(CAPI.InputTrackingCallback))]
        private static bool InputTrackingCallback(out CAPI.ovrAvatar2InputTrackingState trackingState, IntPtr userContext)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(userContext);
                if (bodyContext?._inputTrackingDelegate != null &&
                    bodyContext._inputTrackingDelegate.GetInputTrackingState(out bodyContext._inputTrackingState))
                {
                    trackingState = bodyContext._inputTrackingState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            trackingState = default;
            return false;
        }

        [MonoPInvokeCallback(typeof(CAPI.InputControlCallback))]
        private static bool InputControlCallback(out CAPI.ovrAvatar2InputControlState controlState, IntPtr userContext)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(userContext);
                if (bodyContext?._inputControlDelegate != null &&
                    bodyContext._inputControlDelegate.GetInputControlState(out bodyContext._inputControlState))
                {
                    controlState = bodyContext._inputControlState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            controlState = default;
            return false;
        }

        protected override bool GetBodyState(OvrAvatarTrackingBodyState bodyState)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodyStateCallback(out var nativeBodyState, cb.context))
                {
                    bodyState.FromNative(ref nativeBodyState);
                    return true;
                }
            }
            return false;
        }

        protected override bool GetBodySkeleton(ref OvrAvatarTrackingSkeleton skeleton)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodySkeletonCallback != null)
                {
                    var native = skeleton.GetNative();
                    var result = cb.bodySkeletonCallback(ref native, cb.context);
                    skeleton.CopyFromNative(ref native);
                    return result;
                }

            }

            return false;
        }

        protected override bool GetBodyPose(ref OvrAvatarTrackingPose pose)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodyPoseCallback != null)
                {
                    var native = pose.GetNative();
                    var result = cb.bodyPoseCallback(ref native, cb.context);
                    pose.CopyFromNative(ref native);
                    return result;
                }
            }

            return false;
        }
    }
}
