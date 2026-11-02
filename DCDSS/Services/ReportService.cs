using DCDSS.DTOs.Reports;
using DCDSS.Entities;
using DCDSS.Exceptions;
using DCDSS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Services;

public class ReportService : IReportService
{
    private readonly IRepository<MedicalRecord> _recordRepo;
    private readonly IRepository<Patient> _patientRepo;
    private readonly IRepository<LabResult> _labRepo;

    public ReportService(
        IRepository<MedicalRecord> recordRepo,
        IRepository<Patient> patientRepo,
        IRepository<LabResult> labRepo)
    {
        _recordRepo = recordRepo;
        _patientRepo = patientRepo;
        _labRepo = labRepo;
    }

    public async Task<MonthlyStatsDto> GetMonthlyStatsAsync(int month, int year)
    {
        if (month < 1 || month > 12)
            throw new BusinessRuleException("Month must be between 1 and 12.");
        if (year < 2000)
            throw new BusinessRuleException("Year must be 2000 or later.");

        var totalVisits = await _recordRepo.Query()
            .CountAsync(mr => mr.VisitDate.Year == year && mr.VisitDate.Month == month);

        var newPatients = await _patientRepo.Query()
            .CountAsync(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month);

        var labTests = await _labRepo.Query()
            .CountAsync(lr => lr.DateOrdered.Year == year && lr.DateOrdered.Month == month);

        var deptStats = await _recordRepo.Query()
            .Include(mr => mr.Doctor)
            .Where(mr => mr.VisitDate.Year == year && mr.VisitDate.Month == month)
            .GroupBy(mr => mr.Doctor.Department ?? "Unassigned")
            .Select(g => new DepartmentStatDto
            {
                Department = g.Key,
                VisitCount = g.Count()
            })
            .ToListAsync();

        return new MonthlyStatsDto
        {
            Month = month,
            Year = year,
            TotalVisits = totalVisits,
            NewPatientRegistrations = newPatients,
            LabTestsOrdered = labTests,
            DepartmentPerformance = deptStats
        };
    }
}
