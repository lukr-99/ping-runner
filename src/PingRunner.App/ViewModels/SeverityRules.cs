using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>
/// When a figure reads as good, worth a look or bad, in one place for the live figures and the
/// history: loss above 1 % warns and above 5 % is bad; call quality follows its rating.
/// </summary>
public static class SeverityRules
{
    public static Severity Loss(double? fraction) => fraction switch
    {
        null => Severity.Neutral,
        0 => Severity.Good,
        <= 0.01 => Severity.Neutral,
        <= 0.05 => Severity.Warning,
        _ => Severity.Bad,
    };

    public static Severity Quality(CallQualityRating? rating) => rating switch
    {
        null => Severity.Neutral,
        CallQualityRating.Excellent or CallQualityRating.Good => Severity.Good,
        CallQualityRating.Fair => Severity.Neutral,
        CallQualityRating.Poor => Severity.Warning,
        _ => Severity.Bad,
    };
}
