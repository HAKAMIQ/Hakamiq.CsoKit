using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Tests.Native;

public sealed class NativeCsoRuntimeTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("YES")]
    public void GetInfo_WhenNativeDisabledByEnvironment_ReturnsManagedFallback(string value)
    {
        string? previous = Environment.GetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable, value);

            NativeCsoRuntimeInfo info = NativeCsoRuntime.GetInfo();

            Assert.False(info.IsAvailable);
            Assert.Equal("managed", info.BackendName);
            Assert.Null(info.NativeVersion);
            Assert.Contains(NativeCsoRuntime.DisableNativeEnvironmentVariable, info.FailureReason);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable, previous);
        }
    }
}
