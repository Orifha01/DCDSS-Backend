using DCDSS.Entities.Enums;
using DCDSS.Models;

namespace DCDSS.Services.Interfaces;

public interface IReferenceRangeParser
{
    ReferenceRangeParseResult ParseRange(string referenceRange);
    double? ParseNumericValue(string resultValue);
    LabResultStatus Classify(string resultValue, string referenceRange);
}
