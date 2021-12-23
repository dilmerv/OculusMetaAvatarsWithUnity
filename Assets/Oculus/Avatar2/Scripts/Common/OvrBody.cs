using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    internal static partial class OvrBody
    {
        internal enum OvrBodyLoadLibraryResult : Int32
        {
            Success = 0,
            Failure = 1,
            Unknown = 2
        }

        internal static OvrBodyLoadLibraryResult LoadLibrary()
        {
            OvrBodyLoadLibraryResult loadResult;
            try
            {
                ovrAvatar2_forceLibraryLoad();
                // This call should have failed
                loadResult = OvrBodyLoadLibraryResult.Unknown;
            }
            catch (Exception e)
            {
                loadResult = !(e is DllNotFoundException) ? OvrBodyLoadLibraryResult.Success : OvrBodyLoadLibraryResult.Failure;
                if (!(e is EntryPointNotFoundException))
                {
                    OvrAvatarLog.LogError($"Unexpected exception, {e.ToString()}", logScope);
                }
            }
            if (loadResult != OvrBodyLoadLibraryResult.Success)
            {
                OvrAvatarLog.LogError("Unable to find libovrbody!", logScope);
            }
            return loadResult;
        }

        private const string logScope = "ovrBody";
        private const string OvrBodyLibFile = "libovrbody";

        // This method *should* not exist -
        // we are using it to trigger an expected exception and force DLL load in older Unity versions
        [DllImport(OvrBodyLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ovrAvatar2_forceLibraryLoad();
    }

} // namespace Oculus.Avatar2
