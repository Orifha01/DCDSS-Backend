using DCDSS.Entities;

namespace DCDSS.Models.Cds;

public static class LabResultSelector
{
    public static IList<LabResult> LatestPerTest(IEnumerable<LabResult> results) =>
        results
            .GroupBy(r => r.TestName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.DateOrdered).First())
            .ToList();
}
