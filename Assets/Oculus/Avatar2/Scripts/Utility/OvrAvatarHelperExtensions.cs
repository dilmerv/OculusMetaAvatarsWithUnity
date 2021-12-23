using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Oculus.Avatar2
{
    public static class OvrAvatarHelperExtensions
    {
        public static bool IsSuccess(this CAPI.ovrAvatar2Result result)
        {
            return result == CAPI.ovrAvatar2Result.Success;
        }
        public static bool IsFailure(this CAPI.ovrAvatar2Result result)
        {
            return result != CAPI.ovrAvatar2Result.Success;
        }

        #region Containers
        public static T[] NullSafeToArray<T>(this HashSet<T> set)
        {
            return set != null ? set.ToArray() : Array.Empty<T>();
        }

        public static T[] ToArray<T>(this HashSet<T> set)
        {
            var copyArray = set.Count > 0 ? new T[set.Count] : Array.Empty<T>();
            set.CopyTo(copyArray);
            return copyArray;
        }

        public static UInt32 GetBufferSize<T>(in this NativeArray<T> array) where T : struct
        {
            return array.GetBufferSize(Marshal.SizeOf<T>());
        }

        public static UInt32 GetBufferSize<T>(in this NativeArray<T> array, int elementSize) where T : struct
        {
            Debug.Assert(elementSize != 0);
            Debug.Assert(elementSize == Marshal.SizeOf<T>());
            return (UInt32)(array.Length * elementSize);
        }

        public static UInt32 GetBufferSize(in this NativeArray<byte> array)
        {
            return (UInt32)array.Length;
        }

        public static UInt32 GetEnumBufferSize<T>(in this NativeArray<T> array) where T : struct, Enum
        {
            var underlyingType = Enum.GetUnderlyingType(typeof(T));
            return array.GetEnumBufferSize((uint)Marshal.SizeOf(underlyingType));
        }

        public static UInt32 GetEnumBufferSize<T>(in this NativeArray<T> array, UInt32 explicitSize) where T : struct, Enum
        {
            Debug.Assert(Marshal.SizeOf(Enum.GetUnderlyingType(typeof(T))) == explicitSize);
            return (UInt32)(array.Length * explicitSize);
        }

        /* Lengths must match */
        // NOTE: ref is invalid for `array` when used in `using` statement, eat 1 copy :(
        public static void CopyFrom<T>(this NativeArray<T> array, HashSet<T> hashSet) where T : struct
        {
            OvrAvatarLog.Assert(array.Length == hashSet.Count);
            int copyIdx = 0;
            foreach (var value in hashSet)
            {
                array[copyIdx++] = value;
            }
        }

        public static IntPtr GetIntPtr<T>(in this NativeArray<T> array) where T : struct
        {
            unsafe
            {
                return (IntPtr)array.GetUnsafePtr();
            }
        }

        public static unsafe T* GetPtr<T>(in this NativeArray<T> array) where T : unmanaged
        {
            return (T*)array.GetUnsafePtr();
        }

        public static unsafe T* GetReadonlyPtr<T>(in this NativeArray<T> array) where T : unmanaged
        {
            return (T*)array.GetUnsafeReadOnlyPtr();
        }

        /* If the target array `IsCreated`, `Dispose` it and reset the reference to default */
        public static void Reset<T>(ref this NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                array = default;
            }
        }

        /* Calling dispose on an unallocated NativeArray throws an exception
         * This makes code handling optional NativeArrays rather clunky
         * This wrapper simply adds an `IsCreated` check before forwarding the `Dispose` call*/
        public struct NativeArrayDisposeWrapper<T> : IDisposable, IEnumerable<T>, System.Collections.IEnumerable where T : struct
        {
            public NativeArrayDisposeWrapper(in NativeArray<T> wrappedArray)
                => array = wrappedArray;

            public NativeArray<T> array;
            public bool IsCreated => array.IsCreated;

            public void Dispose()
            {
                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }

            public T[] ToArray() => array.IsCreated ? array.ToArray() : Array.Empty<T>();

            public static implicit operator NativeArrayDisposeWrapper<T>(in NativeArray<T> natArr)
                => natArr.GetDisposeSafe();

            // C# is truly a flawless language...
            public NativeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => array.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
                => array.GetEnumerator();
        }

        public static NativeArrayDisposeWrapper<T> GetDisposeSafe<T>(this in NativeArray<T> array) where T : struct
        {
            return new NativeArrayDisposeWrapper<T>(in array);
        }

        public static Dictionary<K, V> Copy<K, V>(this Dictionary<K, V> dict)
        {
            return new Dictionary<K, V>(dict);
        }

        public static void CopyFrom<K, V>(this Dictionary<K, V> dest, Dictionary<K, V> source)
        {
            dest.Clear();
            foreach (var kvp in source)
            {
                dest.Add(kvp.Key, kvp.Value);
            }
        }

        public static void CopyFrom<K, V>(this Dictionary<K, V[]> dest, Dictionary<K, List<V>> source)
        {
            dest.Clear();
            foreach (var kvp in source)
            {
                dest.Add(kvp.Key, kvp.Value.ToArray());
            }
        }

        public static bool Contains<T>(this T[] array, in T value)
        {
            return Array.IndexOf(array, value) >= 0;
        }


        public static bool Contains<T>(this IReadOnlyList<T> list, in T value, int searchLength) where T : class
        {
            for (int idx = 0; idx < searchLength; ++idx)
            {
                if (list[idx] == value)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Contains(this Array array, in object value)
        {
            Int32 index = Array.IndexOf(array, value);
            // Yay .NET https://docs.microsoft.com/en-us/dotnet/api/system.array.indexof?view=netcore-3.1
            return array.GetLowerBound(0) <= index && index < Int32.MaxValue;
        }

        // Helpers for swapping List in to replace LinkedList (which makes lots of GC.Allocs)
        public static T? First<T>(this List<T> list) where T : struct
        {
            return list.Count > 0 ? list[0] : (T?)null;
        }

        public static void AddFirst<T>(this List<T> list, T element)
        {
            list.Insert(0, element);
        }

        public static void AddLast<T>(this List<T> list, T element)
        {
            list.Add(element);
        }

        public static void RemoveFirst<T>(this List<T> list)
        {
            list.RemoveAt(0);
        }

        #endregion

        #region Logging

        private const string DefaultContext = "no context";
        private const string DefaultScope = "ovrAvatar2.CAPI";
        public static bool EnsureSuccess(this CAPI.ovrAvatar2Result result
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            bool wasSuccess = result.IsSuccess();
            if (!wasSuccess)
            {
                result.LogError(msgContext, logScope, unityContext);
            }
            return wasSuccess;
        }

        // TODO: Expand `EnsureSuccessOrLog` so it can handle `EnsureSuccessOrWarning` as expected
        // May be easier to just update every call to `EnsureSuccessOrWarning`
        // to include `to resolve this warning` text
        private const string DefaultWarningSuggestion = "verify call";
        internal static bool EnsureSuccessOrWarning(this CAPI.ovrAvatar2Result result
            , CAPI.ovrAvatar2Result succeedWithWarningResult
            , string warningSuggestion = DefaultWarningSuggestion
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            bool wasSuccessOrWarn = result.IsSuccess();
            if (!wasSuccessOrWarn)
            {
                wasSuccessOrWarn = result == succeedWithWarningResult;
                if (wasSuccessOrWarn)
                {
                    OvrAvatarLog.LogWarning(
                        $"Operation ({msgContext}) succeeded with warning ({result})\n - {warningSuggestion} to resolve this warning"
                        , logScope, unityContext);
                }
                else
                {
                    result.LogError(msgContext, logScope, unityContext);
                }
            }
            return wasSuccessOrWarn;
        }

        private const string DefaultLogVerboseContext = "no action required";
        // TODO: System.Diagnostic.Conditional
        internal static bool EnsureSuccessOrLogVerbose(this CAPI.ovrAvatar2Result result
            , CAPI.ovrAvatar2Result succeedWithLogVerboseResult
            , string logDebugContext = DefaultLogVerboseContext
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            return EnsureSuccessOrLog(result, succeedWithLogVerboseResult,
                logDebugContext, msgContext, logScope, unityContext,
                OvrAvatarLog.ELogLevel.Verbose);
        }

        private const string DefaultLogDebugContext = "this should be a transient issue";
        // TODO: System.Diagnostic.Conditional
        internal static bool EnsureSuccessOrLogDebug(this CAPI.ovrAvatar2Result result
            , CAPI.ovrAvatar2Result succeedWithLogDebugResult
            , string logDebugContext = DefaultLogDebugContext
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            return EnsureSuccessOrLog(result, succeedWithLogDebugResult,
                logDebugContext, msgContext, logScope, unityContext,
                OvrAvatarLog.ELogLevel.Debug);
        }

        private const string DefaultLogInfoContext = "verify no unwanted side effects";
        // TODO: System.Diagnostic.Conditional
        internal static bool EnsureSuccessOrLogInfo(this CAPI.ovrAvatar2Result result
            , CAPI.ovrAvatar2Result succeedWithLogDebugResult
            , string logDebugContext = DefaultLogInfoContext
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            return EnsureSuccessOrLog(result, succeedWithLogDebugResult,
                logDebugContext, msgContext, logScope, unityContext,
                OvrAvatarLog.ELogLevel.Info);
        }

        // TODO: System.Diagnostic.Conditional
        public static void LogError(this CAPI.ovrAvatar2Result result
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            OvrAvatarLog.Assert(!result.IsSuccess());
            OvrAvatarLog.LogError($"Operation ({msgContext}) failed with result ({result})"
                , logScope, unityContext);
        }

        // TODO: System.Diagnostic.Conditional
        public static void LogAssert(this CAPI.ovrAvatar2Result result
            , string msgContext = DefaultContext
            , string logScope = DefaultScope
            , UnityEngine.Object unityContext = null)
        {
            OvrAvatarLog.AssertTwoParams(result.IsSuccess()
                , msgContext, in result
                , _CachedLogAssertBuilder
                , logScope, unityContext);
        }
        // This ensures Mono doesn't helpfully allocate a wrapper every time this is called...
        private static readonly OvrAvatarLog.AssertMessageBuilder<string, CAPI.ovrAvatar2Result>
            _CachedLogAssertBuilder = _LogAssertBuilder;
        private static string _LogAssertBuilder(in string msgCtx, in CAPI.ovrAvatar2Result r)
            => $"{msgCtx} failed with {r}";


        private static bool EnsureSuccessOrLog(this CAPI.ovrAvatar2Result result
            , CAPI.ovrAvatar2Result succeedWithLogResult
            , string logDebugContext
            , string msgContext
            , string logScope
            , UnityEngine.Object unityContext
            , OvrAvatarLog.ELogLevel logLevel)
        {
            bool wasSuccessOrLog = result.IsSuccess();
            if (!wasSuccessOrLog)
            {
                wasSuccessOrLog = result == succeedWithLogResult;
                if (wasSuccessOrLog)
                {
                    OvrAvatarLog.Log(logLevel
                        , $"Operation ({msgContext}) succeeded with log ({result})\n - {logDebugContext}"
                        , logScope, unityContext);
                }
                else
                {
                    result.LogError(msgContext, logScope, unityContext);
                }
            }
            return wasSuccessOrLog;
        }
        #endregion //Logging
    }
} // namespace Oculus.Avatar2
