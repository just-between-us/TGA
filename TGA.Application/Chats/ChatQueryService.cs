using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;

namespace TGA.Application.Chats;

public class ChatQueryService(IMessageStorageService storage, IAccountStorageService accounts)
{
    public async Task<List<MessageDto>> GetMessagesForActiveAccountAsync(string? contact = null)
    {
        var active = await accounts.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        return await storage.GetMessagesAsync(active.Id, contact);
    }

    public async Task<List<string>> GetContactsForActiveAccountAsync()
    {
        var active = await accounts.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        return await storage.GetContactsAsync(active.Id);
    }
}