using System.Collections.Concurrent;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;

namespace TGA.Infrastructure.Diagnostics;

public class InMemoryLogSink : IDebugLogSink
{
    private const int Capacity = 500;
    private readonly ConcurrentQueue<LogEntryDto> _entries = new();

    public void Add(LogEntryDto entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > Capacity)
            _entries.TryDequeue(out _);
    }

    public IReadOnlyList<LogEntryDto> GetRecent(int count = 300) =>
        _entries.Reverse().Take(count).ToList();

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}