namespace DCDSS.DTOs.Reports;

public class MonthlyStatsDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalVisits { get; set; }
    public int NewPatientRegistrations { get; set; }
    public int LabTestsOrdered { get; set; }
    public IList<DepartmentStatDto> DepartmentPerformance { get; set; } = [];
}
