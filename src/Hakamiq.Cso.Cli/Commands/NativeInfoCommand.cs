using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Cli.Commands;

internal static class NativeInfoCommand
{
    public static int Run()
    {
        NativeCsoRuntimeInfo info = NativeCsoRuntime.GetInfo();

        Console.WriteLine($"Backend: {info.BackendName}");
        Console.WriteLine($"Native available: {info.IsAvailable}");

        if (!string.IsNullOrWhiteSpace(info.NativeVersion))
        {
            Console.WriteLine($"Native version: {info.NativeVersion}");
        }

        if (!string.IsNullOrWhiteSpace(info.FailureReason))
        {
            Console.WriteLine($"Fallback reason: {info.FailureReason}");
        }

        return CliExitCodes.Success;
    }
}
