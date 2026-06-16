using Hakamiq.Cso.Core.Formats.Cso;
using Hakamiq.Cso.Tests.Formats.Iso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoRepairerTests
{
    [Fact]
    public void Repair_WithAlignedIso_RebuildsGameSafeCso()
    {
        byte[] iso = PspIsoValidatorTests.CreateMinimalPspIso(includeRequiredPaths: true);
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, iso);

            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.Equal("RawIso", repair.InputFormat);
            Assert.True(File.Exists(csoPath));

            CsoDeepVerifyResult deep = new CsoDeepVerifier().Verify(csoPath, computeSha256: false);
            Assert.True(deep.Success);

            CsoDecompressResult decompress = new CsoDecompressor().Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompress.Success, decompress.ErrorMessage);
            Assert.Equal(iso, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Fact]
    public void Repair_WithUnalignedIsoAndNoPadding_ReturnsIsoNotSectorAligned()
    {
        byte[] alignedIso = PspIsoValidatorTests.CreateMinimalPspIso(includeRequiredPaths: true);
        byte[] unalignedIso = new byte[alignedIso.Length + 1];
        alignedIso.CopyTo(unalignedIso, 0);
        unalignedIso[^1] = 0x01;
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, unalignedIso);

            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.False(repair.Success);
            Assert.Equal("IsoNotSectorAligned", repair.ErrorCode);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Repair_WithCorruptCso_ReturnsReDumpRequiredAndNoOutput()
    {
        byte[] original = new byte[4096];
        string inputCsoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputCsoPath = CreateTempPath(".repaired.cso");

        try
        {
            CsoVerificationResult shallow = new CsoVerifier().Verify(inputCsoPath);
            Assert.True(shallow.Success);

            using (FileStream output = new(inputCsoPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                output.Position = checked((long)shallow.Entries[0].Offset);
                output.Write([0xFF, 0xFF, 0xFF, 0xFF]);
            }

            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(inputCsoPath, outputCsoPath, ForceOverwrite: false));

            Assert.False(repair.Success);
            Assert.Equal("ReDumpRequired", repair.ErrorCode);
            Assert.False(File.Exists(outputCsoPath));
        }
        finally
        {
            File.Delete(inputCsoPath);
            File.Delete(outputCsoPath);
        }
    }

    [Theory]
    [InlineData("zso")]
    [InlineData("dax")]
    [InlineData("cso2")]
    public void Repair_WithUnsupportedContainerVariant_ReturnsClearErrorAndNoOutput(string kind)
    {
        string inputPath = kind switch
        {
            "zso" => CsoTestFileFactory.CreateUnsupportedZsoHeader(),
            "dax" => CsoTestFileFactory.CreateUnsupportedDaxHeader(),
            "cso2" => CsoTestFileFactory.CreateUnsupportedCso2Header(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string outputCsoPath = CreateTempPath(".normalized.cso");

        try
        {
            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(inputPath, outputCsoPath, ForceOverwrite: false));

            Assert.False(repair.Success);
            Assert.False(File.Exists(outputCsoPath));
            Assert.Contains(repair.ErrorCode, new[] { "UnsupportedContainer", "InvalidBlockSize", "RepairNotPossible" });
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
        }
    }

    [Theory]
    [InlineData("zso")]
    [InlineData("dax")]
    [InlineData("cso2")]
    public void Repair_WithSupportedContainerSample_RebuildsCso1AndDeepVerifies(string kind)
    {
        byte[] original = new byte[4096];
        string inputPath = kind switch
        {
            "zso" => CsoTestFileFactory.CreateTempZso(original),
            "dax" => CsoTestFileFactory.CreateTempDax(original),
            "cso2" => CsoTestFileFactory.CreateTempCso2(original),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string outputCsoPath = CreateTempPath(".normalized.cso");
        string outputIsoPath = CreateTempPath(".normalized.iso");

        try
        {
            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(
                    inputPath,
                    outputCsoPath,
                    ForceOverwrite: false,
                    DeepVerify: true));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.True(File.Exists(outputCsoPath));

            CsoDeepVerifyResult deep = new CsoDeepVerifier().Verify(outputCsoPath, computeSha256: false);
            Assert.True(deep.Success);

            CsoDecompressResult decompress = new CsoDecompressor().Decompress(
                new CsoDecompressOptions(outputCsoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompress.Success, decompress.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
            File.Delete(outputIsoPath);
        }
    }


    [Theory]
    [InlineData("zso")]
    [InlineData("cso2")]
    public void Repair_WithCorruptLz4Container_ReturnsReDumpRequiredAndNoOutput(string kind)
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 251))
            .ToArray();
        string inputPath = kind == "zso"
            ? CsoTestFileFactory.CreateTempZso(original)
            : CsoTestFileFactory.CreateTempCso2(original);
        string outputCsoPath = CreateTempPath(".corrupt.normalized.cso");

        try
        {
            CorruptFirstPayloadByte(inputPath);

            CsoRepairResult repair = new CsoRepairer().Repair(
                new CsoRepairOptions(inputPath, outputCsoPath, ForceOverwrite: false));

            Assert.False(repair.Success);
            Assert.Equal("ReDumpRequired", repair.ErrorCode);
            Assert.False(File.Exists(outputCsoPath));
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputCsoPath);
        }
    }

    private static void CorruptFirstPayloadByte(string path)
    {
        const int headerSize = 24;
        const int indexEntryCountFor4096BytesAt2048BlockSize = 3;
        const int firstPayloadOffset = headerSize + (indexEntryCountFor4096BytesAt2048BlockSize * sizeof(uint));

        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.Position = firstPayloadOffset;
        // Force a structurally invalid LZ4 sequence. Changing 0xF0 to 0xFF can still
        // decode as the same literal-only block because the low nibble is ignored when
        // the literal run consumes the whole payload. 0x00 makes the following bytes
        // become an invalid match offset before any literal bytes are emitted.
        stream.WriteByte(0x00);
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}{extension}");
    }
}
