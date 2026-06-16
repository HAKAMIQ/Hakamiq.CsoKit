using System.Runtime.InteropServices;

namespace Hakamiq.Cso.Core.Native;

public static class NativeCsoRuntime
{
    public const string DisableNativeEnvironmentVariable = "HAKAMIQ_CSO_DISABLE_NATIVE";

    private const string LibraryName = "Hakamiq.Cso.Native";
    private const int NativeStatusOk = 0;
    private const int NativeStatusCodecUnavailable = 4;

    static NativeCsoRuntime()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(NativeCsoRuntime).Assembly,
            ResolveNativeLibrary);
    }

    public static NativeCsoRuntimeInfo GetInfo()
    {
        if (IsDisabledByEnvironment())
        {
            return CreateManagedFallback($"Native backend disabled by {DisableNativeEnvironmentVariable}.");
        }

        try
        {
            int probe = NativeMethods.hakamiq_cso_native_probe();
            if (probe != NativeStatusOk)
            {
                return CreateManagedFallback($"Native probe failed with status {probe}.");
            }

            HakamiqCsoNativeVersion version = default;
            int versionResult = NativeMethods.hakamiq_cso_native_get_version(ref version);

            if (versionResult != NativeStatusOk)
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

    public static bool TryDeflateZopfli(
        ReadOnlySpan<byte> input,
        int iterations,
        out byte[] compressed)
    {
        compressed = [];

        if (input.IsEmpty ||
            iterations < 1 ||
            iterations > 100 ||
            IsDisabledByEnvironment())
        {
            return false;
        }

        byte[] inputBuffer = input.ToArray();
        byte[] outputBuffer = new byte[inputBuffer.Length];

        try
        {
            int result = NativeMethods.hakamiq_cso_native_deflate_zopfli(
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                iterations,
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            if (result != NativeStatusOk)
            {
                return false;
            }

            ulong rawOutputSize = outputSize.ToUInt64();

            if (rawOutputSize > (ulong)outputBuffer.Length)
            {
                return false;
            }

            int compressedLength = checked((int)rawOutputSize);

            if (compressedLength != outputBuffer.Length)
            {
                Array.Resize(ref outputBuffer, compressedLength);
            }

            compressed = outputBuffer;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public static bool TryDeflateRaw(
        NativeCsoRawCodec codec,
        int level,
        int strategy,
        ReadOnlySpan<byte> input,
        out byte[] compressed)
    {
        compressed = [];

        if (input.IsEmpty || IsDisabledByEnvironment())
        {
            return false;
        }

        byte[] inputBuffer = input.ToArray();
        byte[] outputBuffer = new byte[inputBuffer.Length];

        try
        {
            int result = NativeMethods.hakamiq_cso_native_deflate_raw(
                (int)codec,
                level,
                strategy,
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            if (result != NativeStatusOk)
            {
                return false;
            }

            ulong rawOutputSize = outputSize.ToUInt64();

            if (rawOutputSize > (ulong)outputBuffer.Length)
            {
                return false;
            }

            int compressedLength = checked((int)rawOutputSize);

            if (compressedLength <= 0)
            {
                return false;
            }

            if (compressedLength != outputBuffer.Length)
            {
                Array.Resize(ref outputBuffer, compressedLength);
            }

            compressed = outputBuffer;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public static bool TryInflateRaw(
        ReadOnlySpan<byte> compressed,
        int expectedBytes,
        out byte[] restored)
    {
        restored = [];

        if (compressed.IsEmpty ||
            expectedBytes <= 0 ||
            IsDisabledByEnvironment())
        {
            return false;
        }

        byte[] inputBuffer = compressed.ToArray();
        byte[] outputBuffer = new byte[expectedBytes];

        try
        {
            int result = NativeMethods.hakamiq_cso_native_inflate_raw(
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            if (result == NativeStatusCodecUnavailable)
            {
                return false;
            }

            if (result != NativeStatusOk)
            {
                return false;
            }

            ulong rawOutputSize = outputSize.ToUInt64();

            if (rawOutputSize != (ulong)expectedBytes)
            {
                return false;
            }

            restored = outputBuffer;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public static NativeCsoCapabilities GetCapabilities()
    {
        if (IsDisabledByEnvironment())
        {
            return NativeCsoCapabilities.ManagedFallback;
        }

        try
        {
            HakamiqCsoNativeCapabilities capabilities = default;
            int result = NativeMethods.hakamiq_cso_native_get_capabilities(ref capabilities);

            if (result != NativeStatusOk)
            {
                return NativeCsoCapabilities.ManagedFallback;
            }

            return new NativeCsoCapabilities(
                capabilities.AbiVersion,
                capabilities.HasZlib != 0,
                capabilities.HasLibDeflate != 0,
                capabilities.HasZopfli != 0,
                capabilities.HasSevenZipDeflate != 0,
                capabilities.HasLz4 != 0);
        }
        catch (DllNotFoundException)
        {
            return NativeCsoCapabilities.ManagedFallback;
        }
        catch (EntryPointNotFoundException)
        {
            return NativeCsoCapabilities.ManagedFallback;
        }
        catch (BadImageFormatException)
        {
            return NativeCsoCapabilities.ManagedFallback;
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

    private static IntPtr ResolveNativeLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in EnumerateNativeLibraryCandidates())
        {
            if (File.Exists(candidate))
            {
                return NativeLibrary.Load(candidate);
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateNativeLibraryCandidates()
    {
        string fileName = OperatingSystem.IsWindows()
            ? $"{LibraryName}.dll"
            : OperatingSystem.IsMacOS()
                ? $"lib{LibraryName}.dylib"
                : $"lib{LibraryName}.so";

        foreach (string root in EnumerateAncestorDirectories(AppContext.BaseDirectory)
            .Concat(EnumerateAncestorDirectories(Environment.CurrentDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(root, "artifacts", "native-build", "win-x64", "Release", fileName);
            yield return Path.Combine(root, "artifacts", "native-build", "win-x64", "Debug", fileName);
        }

        yield return Path.Combine(AppContext.BaseDirectory, fileName);
        yield return Path.Combine(Environment.CurrentDirectory, fileName);
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string start)
    {
        DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(start));

        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HakamiqCsoNativeCapabilities
    {
        public uint AbiVersion;
        public uint HasZlib;
        public uint HasLibDeflate;
        public uint HasZopfli;
        public uint HasSevenZipDeflate;
        public uint HasLz4;
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_probe();

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_get_version(
            ref HakamiqCsoNativeVersion version);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_get_capabilities(
            ref HakamiqCsoNativeCapabilities capabilities);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_deflate_raw(
            int codec,
            int level,
            int strategy,
            byte[] input,
            UIntPtr inputSize,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_inflate_raw(
            byte[] input,
            UIntPtr inputSize,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int hakamiq_cso_native_deflate_zopfli(
            byte[] input,
            UIntPtr inputSize,
            int iterations,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);
    }
}
