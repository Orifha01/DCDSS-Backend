using System.Text.RegularExpressions;
using DCDSS.Entities.Enums;
using DCDSS.Models;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class ReferenceRangeParser : IReferenceRangeParser
{
    private static readonly Regex _rangePattern =
        new(@"^([\d.]+)\s*-\s*([\d.]+)(?:\s*(.+))?$", RegexOptions.Compiled);

    private static readonly Regex _lessThanPattern =
        new(@"^<\s*([\d.]+)(?:\s*(.+))?$", RegexOptions.Compiled);

    private static readonly Regex _greaterThanPattern =
        new(@"^>\s*([\d.]+)(?:\s*(.+))?$", RegexOptions.Compiled);

    private static readonly Regex _qualifyingValuePattern =
        new(@"^[<>]\s*\d", RegexOptions.Compiled);

    private static readonly Regex _numericValuePattern =
        new(@"^[<>]\s*([\d.]+)", RegexOptions.Compiled);

    public double? ParseNumericValue(string resultValue)
    {
        var trimmed = resultValue.Trim();
        var match = _numericValuePattern.Match(trimmed);
        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var val))
                return val;
        }

        if (double.TryParse(trimmed,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var plain))
            return plain;

        return null;
    }

    public ReferenceRangeParseResult ParseRange(string referenceRange)
    {
        var trimmed = referenceRange.Trim();

        var rangeMatch = _rangePattern.Match(trimmed);
        if (rangeMatch.Success)
        {
            double.TryParse(rangeMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var min);
            double.TryParse(rangeMatch.Groups[2].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var max);
            var unit = rangeMatch.Groups[3].Value.Trim();
            return new ReferenceRangeParseResult
            {
                Min = min,
                Max = max,
                Unit = string.IsNullOrEmpty(unit) ? null : unit
            };
        }

        var ltMatch = _lessThanPattern.Match(trimmed);
        if (ltMatch.Success)
        {
            double.TryParse(ltMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var max);
            var unit = ltMatch.Groups[2].Value.Trim();
            return new ReferenceRangeParseResult
            {
                Min = null,
                Max = max,
                Unit = string.IsNullOrEmpty(unit) ? null : unit
            };
        }

        var gtMatch = _greaterThanPattern.Match(trimmed);
        if (gtMatch.Success)
        {
            double.TryParse(gtMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var min);
            var unit = gtMatch.Groups[2].Value.Trim();
            return new ReferenceRangeParseResult
            {
                Min = min,
                Max = null,
                Unit = string.IsNullOrEmpty(unit) ? null : unit
            };
        }

        return new ReferenceRangeParseResult { Min = null, Max = null, Unit = null };
    }

    public LabResultStatus Classify(string resultValue, string referenceRange)
    {
        // Step 1: threshold-form values (e.g., "<5", ">200") are Indeterminate
        if (_qualifyingValuePattern.IsMatch(resultValue.Trim()))
            return LabResultStatus.Indeterminate;

        // Step 2: parse numeric value
        var val = ParseNumericValue(resultValue);
        if (val == null)
            return LabResultStatus.Indeterminate;

        // Step 3: parse reference range and classify
        var parsed = ParseRange(referenceRange);

        if (parsed.Min.HasValue && parsed.Max.HasValue)
        {
            if (val < parsed.Min) return LabResultStatus.Low;
            if (val > parsed.Max) return LabResultStatus.High;
            return LabResultStatus.Normal;
        }

        if (parsed.Max.HasValue)
        {
            return val >= parsed.Max ? LabResultStatus.High : LabResultStatus.Normal;
        }

        if (parsed.Min.HasValue)
        {
            return val <= parsed.Min ? LabResultStatus.Low : LabResultStatus.Normal;
        }

        return LabResultStatus.Indeterminate;
    }
}
