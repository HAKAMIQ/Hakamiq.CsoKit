using System.Security.Cryptography;
using Hakamiq.Cso.Core.Formats.Containers;
using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class ContainerDeepVerifierTests
{
    [Fact]
    public void Verify_WithCso2Lz4_DoesNotUseCso1StoredFlagSemantics()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 251))
            .ToArray();
        string path = CsoTestFileFactory.CreateTempCso2(original);

        try
        {
            using Cso2ContainerReader reader = new(path);
            CsoDeepVerifyResult result = new ContainerDeepVerifier().Verify(reader, computeSha256: true);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Equal((ulong)original.Length, result.BytesReconstructed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), result.Sha256);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithZsoLz4_DecodesEveryBlock()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(255 - (index % 251)))
            .ToArray();
        string path = CsoTestFileFactory.CreateTempZso(original);

        try
        {
            using ZsoContainerReader reader = new(path);
            CsoDeepVerifyResult result = new ContainerDeepVerifier().Verify(reader, computeSha256: true);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Equal((ulong)original.Length, result.BytesReconstructed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), result.Sha256);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsoDeepVerifier_WithCso2_ReturnsUnsupportedContainerInsteadOfMisreadingFlag()
    {
        byte[] original = new byte[4096];
        string path = CsoTestFileFactory.CreateTempCso2(original);

        try
        {
            CsoDeepVerifyResult result = new CsoDeepVerifier().Verify(path, computeSha256: false);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "UnsupportedContainer");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
