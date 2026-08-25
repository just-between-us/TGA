using TGA.Domain.Enums;

namespace TGA.Contract.DTOs.Llm;

public record TriageResult(TriageAction Action, string? Reason);