using System.Runtime.InteropServices;

namespace TileStart.Host.Compatibility;

internal static class NvidiaOverlayCompatibility
{
    private const string ProfileName = "TileStart";
    private const string ExecutableName = "tilestart.host.exe";
    private const uint OverlaySettingId = 0x809D5F60;
    private const uint OverlayDisallow = 0x10000000;
    private const int NvApiOk = 0;
    private const int NvApiSettingNotFound = -160;
    private const int NvApiProfileNotFound = -163;
    private const int NvApiProfileNameInUse = -164;
    private const int NvApiExecutableNotFound = -166;
    private const int NvApiExecutableAlreadyInUse = -167;
    private const int UnicodeStringLength = 2048;
    private const int SettingValueLength = 4100;

    public static bool TryApply(out string detail) => TryUpdate(remove: false, out detail);

    public static bool TryRemove(out string detail) => TryUpdate(remove: true, out detail);

    private static bool TryUpdate(bool remove, out string detail)
    {
        if (!NativeLibrary.TryLoad("nvapi64.dll", out var library))
        {
            detail = "NVIDIA NVAPI is not installed; no compatibility profile was needed.";
            return true;
        }

        IntPtr session = IntPtr.Zero;
        UnloadDelegate? unload = null;
        DestroySessionDelegate? destroySession = null;
        try
        {
            var queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(
                NativeLibrary.GetExport(library, "nvapi_QueryInterface"));
            var initialize = Resolve<InitializeDelegate>(queryInterface, 0x0150E828);
            unload = Resolve<UnloadDelegate>(queryInterface, 0xD22BDD7E);
            var createSession = Resolve<CreateSessionDelegate>(queryInterface, 0x0694D52E);
            destroySession = Resolve<DestroySessionDelegate>(queryInterface, 0xDAD9CFF8);
            var loadSettings = Resolve<LoadSettingsDelegate>(queryInterface, 0x375DBD6B);
            var saveSettings = Resolve<SaveSettingsDelegate>(queryInterface, 0xFCBC7E14);
            var findProfile = Resolve<FindProfileByNameDelegate>(queryInterface, 0x7E4A9A0B);
            var createProfile = Resolve<CreateProfileDelegate>(queryInterface, 0xCC176068);
            var findApplication = Resolve<FindApplicationByNameDelegate>(queryInterface, 0xEEE566B2);
            var createApplication = Resolve<CreateApplicationDelegate>(queryInterface, 0x4347A9DE);
            var setSetting = Resolve<SetSettingDelegate>(queryInterface, 0x8A2CF5F5, 0x577DD202);
            var deleteSetting = Resolve<DeleteProfileSettingDelegate>(
                queryInterface,
                0xD20D29DF,
                0xE4A26362);

            if (initialize is null || createSession is null || destroySession is null || loadSettings is null
                || saveSettings is null || findProfile is null || createProfile is null || findApplication is null
                || createApplication is null || setSetting is null || deleteSetting is null)
            {
                detail = "The installed NVIDIA driver does not expose the required DRS APIs.";
                return true;
            }

            var status = initialize();
            if (status != NvApiOk)
            {
                detail = $"NVIDIA NVAPI initialization was unavailable ({status}); no profile was changed.";
                return true;
            }

            status = createSession(ref session);
            if (status != NvApiOk)
            {
                detail = $"NVIDIA DRS session creation failed ({status}).";
                return false;
            }

            status = loadSettings(session);
            if (status != NvApiOk)
            {
                detail = $"NVIDIA DRS settings could not be loaded ({status}).";
                return false;
            }

            var profile = IntPtr.Zero;
            var application = CreateApplicationInfo();
            status = findApplication(session, ExecutableName, ref profile, ref application);
            if (status == NvApiExecutableNotFound)
            {
                status = findProfile(session, ProfileName, ref profile);
                if (remove && status == NvApiProfileNotFound)
                {
                    detail = "The TileStart NVIDIA profile was already absent.";
                    return true;
                }

                if (!remove && status == NvApiProfileNotFound)
                {
                    var profileInfo = CreateProfileInfo();
                    status = createProfile(session, ref profileInfo, ref profile);
                    if (status == NvApiProfileNameInUse)
                    {
                        status = findProfile(session, ProfileName, ref profile);
                    }
                }

                if (status != NvApiOk)
                {
                    detail = $"The TileStart NVIDIA profile could not be located or created ({status}).";
                    return false;
                }

                if (!remove)
                {
                    application = CreateApplicationInfo();
                    status = createApplication(session, profile, ref application);
                    if (status != NvApiOk && status != NvApiExecutableAlreadyInUse)
                    {
                        detail = $"TileStart could not be associated with its NVIDIA profile ({status}).";
                        return false;
                    }

                    if (status == NvApiExecutableAlreadyInUse)
                    {
                        profile = IntPtr.Zero;
                        application = CreateApplicationInfo();
                        status = findApplication(session, ExecutableName, ref profile, ref application);
                    }
                }
            }

            if (status != NvApiOk || profile == IntPtr.Zero)
            {
                detail = $"The TileStart NVIDIA application entry could not be resolved ({status}).";
                return false;
            }

            if (remove)
            {
                status = deleteSetting(session, profile, OverlaySettingId);
                if (status != NvApiOk && status != NvApiSettingNotFound)
                {
                    detail = $"The TileStart NVIDIA Overlay setting could not be removed ({status}).";
                    return false;
                }
            }
            else
            {
                var setting = CreateOverlaySetting();
                status = setSetting(session, profile, ref setting, 0, 0);
                if (status != NvApiOk)
                {
                    detail = $"The TileStart NVIDIA Overlay setting could not be applied ({status}).";
                    return false;
                }
            }

            status = saveSettings(session);
            if (status != NvApiOk)
            {
                detail = $"The TileStart NVIDIA profile could not be saved ({status}).";
                return false;
            }

            detail = remove
                ? "Removed the TileStart NVIDIA Overlay exclusion."
                : "Excluded TileStart from NVIDIA Overlay game detection.";
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException
                                              or EntryPointNotFoundException
                                              or BadImageFormatException
                                              or MarshalDirectiveException)
        {
            detail = $"NVIDIA profile compatibility was unavailable: {exception.Message}";
            return true;
        }
        finally
        {
            if (session != IntPtr.Zero)
            {
                destroySession?.Invoke(session);
            }

            unload?.Invoke();
            NativeLibrary.Free(library);
        }
    }

    private static T? Resolve<T>(QueryInterfaceDelegate queryInterface, params uint[] ids) where T : Delegate
    {
        foreach (var id in ids)
        {
            var address = queryInterface(id);
            if (address != IntPtr.Zero)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
        }

        return null;
    }

    private static DrsProfile CreateProfileInfo() => new()
    {
        Version = MakeVersion<DrsProfile>(1),
        ProfileName = ProfileName,
    };

    private static DrsApplication CreateApplicationInfo() => new()
    {
        Version = MakeVersion<DrsApplication>(4),
        AppName = ExecutableName,
        UserFriendlyName = ProfileName,
        Launcher = string.Empty,
        FileInFolder = string.Empty,
        CommandLine = string.Empty,
    };

    private static DrsSetting CreateOverlaySetting()
    {
        var currentValue = new DrsSettingValue { RawData = new byte[SettingValueLength] };
        BitConverter.GetBytes(OverlayDisallow).CopyTo(currentValue.RawData, 0);
        return new DrsSetting
        {
            Version = MakeVersion<DrsSetting>(1),
            SettingName = string.Empty,
            SettingId = OverlaySettingId,
            SettingType = DrsSettingType.Dword,
            SettingLocation = DrsSettingLocation.CurrentProfile,
            PredefinedValue = new DrsSettingValue { RawData = new byte[SettingValueLength] },
            CurrentValue = currentValue,
        };
    }

    private static uint MakeVersion<T>(uint version) where T : struct =>
        (uint)Marshal.SizeOf<T>() | (version << 16);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterfaceDelegate(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UnloadDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateSessionDelegate(ref IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DestroySessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SaveSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int FindProfileByNameDelegate(IntPtr session, string profileName, ref IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateProfileDelegate(IntPtr session, ref DrsProfile profileInfo, ref IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int FindApplicationByNameDelegate(
        IntPtr session,
        string appName,
        ref IntPtr profile,
        ref DrsApplication application);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateApplicationDelegate(IntPtr session, IntPtr profile, ref DrsApplication application);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetSettingDelegate(
        IntPtr session,
        IntPtr profile,
        ref DrsSetting setting,
        uint reserved1,
        uint reserved2);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DeleteProfileSettingDelegate(IntPtr session, IntPtr profile, uint settingId);

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct DrsProfile
    {
        public uint Version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string ProfileName;

        public uint GpuSupport;
        public uint IsPredefined;
        public uint ApplicationCount;
        public uint SettingCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct DrsApplication
    {
        public uint Version;
        public uint IsPredefined;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string UserFriendlyName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string Launcher;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string FileInFolder;

        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string CommandLine;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct DrsSetting
    {
        public uint Version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringLength)]
        public string SettingName;

        public uint SettingId;
        public DrsSettingType SettingType;
        public DrsSettingLocation SettingLocation;
        public uint IsCurrentPredefined;
        public uint IsPredefinedValid;
        public DrsSettingValue PredefinedValue;
        public DrsSettingValue CurrentValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = SettingValueLength)]
    private struct DrsSettingValue
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SettingValueLength)]
        public byte[] RawData;
    }

    private enum DrsSettingType
    {
        Dword,
    }

    private enum DrsSettingLocation
    {
        CurrentProfile,
    }
}
