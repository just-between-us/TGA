using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IStatisticsService
{
    Task<StatisticsDto> GetAsync(int accountId, StatisticsRequestDto request, CancellationToken ct = default);
}
