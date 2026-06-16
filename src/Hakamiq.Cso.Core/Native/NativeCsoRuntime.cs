using System.Runtime.InteropServices;

namespace Hakamiq.Cso.Core.Native;

public static class NativeCsoRuntime
{
    public const string DisableNativeEnvironmentVariable = "HAKAMIQ_CSO_DISABLE_NATIVE";

    private const string LibraryName = "Hakamiq.Cso.Native";

    public static NativeCsoRuntimeInfo GetInfo()
    {
        if (IsDisabledByEnvironment())
        {
            return CreateManagedFallback($"Native backend disabled by {DisableNativeEnvironmentVariable}.");
        }

        try
        {
            int probe = NativeMethods.hakamiq_cso_native_probe();
            if (probe != 0)
            {
                return CreateManagedFallback($"Native probe failed with status {probe}.");
            }

            HakamiqCsoNativeVersion version = default;
            int versionResult = NativeMethods.hakamiq_cso_native_get_version(ref version);

            if (versionResult != 0)
            {
                return CreateManagedFallback($"Native version query failed with status {versionResult}.");
            }

            string versionText = $"{version.Major}.{version.Minor}.{version.Patch} ABI {version.AbiVersion}";

            return new NativeCsoRuntimeInfo(
                IsAvailable: true,
                BackendName: "native",
                NativeVersion: versionText,
                FailureReason: null);
        }
        catch (DllNotFoundException ex)
        {
            return CreateManagedFallback(ex.Message);
        }
        catch (EntryPointNotFoundException ex)
        {
            return CreateManagedFallback(ex.Message);
        }
        catch (BadImageFormatException ex)
        {
            return CreateManagedFallback(ex.Message);
        }
    }

    public static bool IsDisabledByEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable(DisableNativeEnvironmentVariable);

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static NativeCsoRuntimeInfo CreateManagedFallback(string reason)
    {
        return new NativeCsoRuntimeInfo(
            IsAvailable: false,
            BackendName: "managed",
            NativeVersion: null,
            FailureReason: reason);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HakamiqCsoNativeVersion
    {
        public uint AbiVersion;
        public uint Major;
        public uint Minor;
        public uint Patch;
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_probe();

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_get_version(
            ref HakamiqCsoNativeVersion version);
    }
}
