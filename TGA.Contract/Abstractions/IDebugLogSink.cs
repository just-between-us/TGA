using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IDebugLogSink
{
    IReadOnlyList<LogEntryDto> GetRecent(int count = 300);
    void Clear();
}