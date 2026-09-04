namespace TGA.Contract.DTOs;

public record StatisticsDto(
    int TotalMessages,
    int IncomingMessages,
    int OutgoingMessages,
    int ActiveContacts,
    double AverageMessagesPerDay,
    IReadOnlyList<StatisticsBucketDto> Activity,
    IReadOnlyList<ContactActivityDto> Contacts,
    IReadOnlyList<StatisticsHeatmapRowDto> OutgoingByWeekdayAndHour,
    IReadOnlyList<ConversationStreakDto> ConversationStreaks);

public record StatisticsBucketDto(string Label, int Count);

public record ContactActivityDto(string ContactName, int Count);

public record StatisticsHeatmapRowDto(string Label, IReadOnlyList<int> Values);

public record ConversationStreakDto(
    string ContactName,
    int CurrentDays,
    int BestDays,
    DateTime LastActiveDate,
    byte[]? AvatarData);
