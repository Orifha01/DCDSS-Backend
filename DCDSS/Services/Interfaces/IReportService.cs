using DCDSS.DTOs.Reports;

namespace DCDSS.Services.Interfaces;

public interface IReportService
{
    Task<MonthlyStatsDto> GetMonthlyStatsAsync(int month, int year);
}
