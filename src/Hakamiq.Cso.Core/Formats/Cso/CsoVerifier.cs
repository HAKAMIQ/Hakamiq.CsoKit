namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoVerifier
{
    private readonly CsoHeaderReader headerReader = new();
    private readonly CsoIndexReader indexReader = new();

    public CsoVerificationResult Verify(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoVerificationResult.Fail(
                null,
                Array.Empty<CsoIndexEntry>(),
                [new CsoVerificationIssue("InvalidInputPath", "Input path is empty.")]);
        }

        if (!File.Exists(inputPath))
        {
            return CsoVerificationResult.Fail(
                null,
                Array.Empty<CsoIndexEntry>(),
                [new CsoVerificationIssue("InputNotFound", "Input file was not found.")]);
        }

        FileInfo fileInfo = new(inputPath);

        CsoHeaderReadResult headerResult = headerReader.Read(inputPath);

        if (!headerResult.Success || headerResult.Header is null)
        {
            return CsoVerificationResult.Fail(
                null,
                Array.Empty<CsoIndexEntry>(),
                [new CsoVerificationIssue(
                    headerResult.ErrorCode ?? "HeaderReadFailed",
                    headerResult.ErrorMessage ?? "Failed to read CSO header.")]);
        }

        CsoHeader header = headerResult.Header;
        CsoIndexReadResult indexResult = indexReader.Read(inputPath, header);

        if (!indexResult.Success)
        {
            return CsoVerificationResult.Fail(
                header,
                indexResult.Entries,
                [new CsoVerificationIssue(
                    indexResult.ErrorCode ?? "IndexReadFailed",
                    indexResult.ErrorMessage ?? "Failed to read CSO index table.")]);
        }

        List<CsoVerificationIssue> issues = ValidateIndex(header, indexResult.Entries, fileInfo.Length);

        if (issues.Count != 0)
        {
            return CsoVerificationResult.Fail(header, indexResult.Entries, issues);
        }

        return CsoVerificationResult.Ok(header, indexResult.Entries);
    }

    private static List<CsoVerificationIssue> ValidateIndex(
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries,
        long fileLength)
    {
        List<CsoVerificationIssue> issues = [];

        if (entries.Count != header.IndexEntryCount)
        {
            issues.Add(new CsoVerificationIssue(
                "IndexEntryCountMismatch",
                $"Expected {header.IndexEntryCount:N0} index entries, got {entries.Count:N0}."));

            return issues;
        }

        long indexEnd = checked((long)header.HeaderSize + header.IndexTableSizeBytes);

        if (entries[0].Offset < (ulong)indexEnd)
        {
            issues.Add(new CsoVerificationIssue(
                "FirstDataOffsetBeforeIndexEnd",
                $"First CSO data offset points before the end of the index table. Data offset: {entries[0].Offset:N0}, index end: {indexEnd:N0}.",
                0));
        }

        for (int i = 0; i < entries.Count; i++)
        {
            CsoIndexEntry entry = entries[i];

            if (entry.Offset > (ulong)fileLength)
            {
                issues.Add(new CsoVerificationIssue(
                    "IndexOffsetPastEndOfFile",
                    $"Index entry {i:N0} points past the end of the file. Offset: {entry.Offset:N0}, file size: {fileLength:N0}.",
                    i));
            }

            if (i == 0)
            {
                continue;
            }

            CsoIndexEntry previous = entries[i - 1];

            if (entry.Offset < previous.Offset)
            {
                issues.Add(new CsoVerificationIssue(
                    "IndexOffsetsNotMonotonic",
                    $"Index entry {i:N0} offset is smaller than the previous entry. Previous: {previous.Offset:N0}, current: {entry.Offset:N0}.",
                    i));
            }
        }

        CsoIndexEntry finalEntry = entries[^1];

        if (finalEntry.Offset > (ulong)fileLength)
        {
            issues.Add(new CsoVerificationIssue(
                "FinalOffsetPastEndOfFile",
                $"Final CSO offset points past the end of the file. Offset: {finalEntry.Offset:N0}, file size: {fileLength:N0}.",
                entries.Count - 1));
        }

        return issues;
    }
}