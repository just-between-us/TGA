namespace TGA.Contract.DTOs;

public record ImportSummaryDto(
    int ChatsProcessed, 
    int MessagesImported, 
    int MessagesSkipped);