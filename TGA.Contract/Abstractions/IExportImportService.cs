using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IExportImportService
{
    Task<List<ChatPreviewDto>> ParseAsync(string filePath, int accountId, CancellationToken ct = default);

    Task<ImportSummaryDto> ImportAsync(
        List<ChatPreviewDto> chats, int accountId, IProgress<string>? progress = null, CancellationToken ct = default);
}