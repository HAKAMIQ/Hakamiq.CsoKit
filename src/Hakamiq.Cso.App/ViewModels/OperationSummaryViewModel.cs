using Hakamiq.Cso.App.Localization;

namespace Hakamiq.Cso.App.ViewModels;

public sealed class OperationSummaryViewModel(
    string title,
    string message,
    string originalSizeText,
    string resultSizeText,
    string savedSizeText)
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string OriginalSizeText { get; } = originalSizeText;

    public string ResultSizeText { get; } = resultSizeText;

    public string SavedSizeText { get; } = savedSizeText;

    public static OperationSummaryViewModel CreateEmpty(UiText text)
    {
        return new OperationSummaryViewModel(
            text.Ready,
            text.NoResultsYet,
            "-",
            "-",
            "-");
    }
}