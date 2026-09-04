using System.Globalization;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;

namespace TGA.Application.Statistics;

public class StatisticsService(
    IMessageStorageService messageStorage,
    IContactStorageService contactStorage) : IStatisticsService
{
    public async Task<StatisticsDto> GetAsync(
        int accountId, StatisticsRequestDto request, CancellationToken ct = default)
    {
        if (request.To < request.From)
            throw new ArgumentException("Дата окончания не может быть раньше даты начала.");

        var from = request.From.Date;
        var toExclusive = request.To.Date.AddDays(1);
        var messages = await messageStorage.GetStatisticsMessagesAsync(accountId, from, toExclusive, ct);
        var contactDtos = await contactStorage.GetAllAsync(accountId);
        var avatarsByPeer = contactDtos.ToDictionary(contact => contact.PeerUserId, contact => contact.AvatarData);

        var activity = BuildActivity(messages, from, request.To.Date, request.Grouping);
        var contactGroups = messages
            .GroupBy(m => string.IsNullOrWhiteSpace(m.ContactName) ? $"User {m.PeerUserId}" : m.ContactName)
            .OrderByDescending(g => g.Count())
            .Select(g => new ContactActivityDto(g.Key, g.Count()))
            .ToList();
        var contacts = contactGroups.Take(8).ToList();
        var otherCount = contactGroups.Skip(8).Sum(item => item.Count);
        if (otherCount > 0)
            contacts.Add(new ContactActivityDto("Остальные", otherCount));

        var days = Math.Max(1, (request.To.Date - from).Days + 1);
        return new StatisticsDto(
            messages.Count,
            messages.Count(m => !m.IsOutgoing),
            messages.Count(m => m.IsOutgoing),
            messages.Select(m => m.PeerUserId).Distinct().Count(),
            Math.Round(messages.Count / (double)days, 1),
            activity,
            contacts,
            BuildHeatmap(messages),
            BuildConversationStreaks(messages, avatarsByPeer));
    }

    private static IReadOnlyList<StatisticsBucketDto> BuildActivity(
        IReadOnlyList<MessageStatisticsSourceDto> messages,
        DateTime from,
        DateTime to,
        StatisticsGrouping grouping)
    {
        var culture = new CultureInfo("ru-RU");
        var buckets = new List<StatisticsBucketDto>();
        var cursor = grouping switch
        {
            StatisticsGrouping.Week => StartOfWeek(from),
            StatisticsGrouping.Month => new DateTime(from.Year, from.Month, 1),
            _ => from
        };

        while (cursor <= to)
        {
            var next = grouping switch
            {
                StatisticsGrouping.Week => cursor.AddDays(7),
                StatisticsGrouping.Month => cursor.AddMonths(1),
                _ => cursor.AddDays(1)
            };
            var count = messages.Count(m => m.Time >= cursor && m.Time < next);
            var label = grouping switch
            {
                StatisticsGrouping.Week => $"{cursor:dd.MM}",       
                StatisticsGrouping.Month => cursor.ToString("MMMM yyyy", culture), 
                _ => $"{cursor.Day} {cursor.ToString("MMM", culture)}" 
            };
            buckets.Add(new StatisticsBucketDto(label, count));
            cursor = next;
        }

        return buckets;
    }

    private static IReadOnlyList<StatisticsHeatmapRowDto> BuildHeatmap(
        IReadOnlyList<MessageStatisticsSourceDto> messages)
    {
        var rows = new[]
        {
            (DayOfWeek.Monday, "Пн"), (DayOfWeek.Tuesday, "Вт"),
            (DayOfWeek.Wednesday, "Ср"), (DayOfWeek.Thursday, "Чт"),
            (DayOfWeek.Friday, "Пт"), (DayOfWeek.Saturday, "Сб"),
            (DayOfWeek.Sunday, "Вс")
        };

        return rows.Select(row => new StatisticsHeatmapRowDto(
            row.Item2,
            Enumerable.Range(0, 24)
                .Select(hour => messages.Count(m =>
                    m.IsOutgoing && ToMondayFirst(m.Time.DayOfWeek) == row.Item1 && m.Time.Hour == hour))
                .ToList())).ToList();
    }

    private static IReadOnlyList<ConversationStreakDto> BuildConversationStreaks(
        IReadOnlyList<MessageStatisticsSourceDto> messages,
        IReadOnlyDictionary<long, byte[]?> avatarsByPeer)
    {
        return messages
            .GroupBy(message => message.PeerUserId)
            .Select(group =>
            {
                var days = group.Select(message => message.Time.Date).Distinct().OrderBy(day => day).ToList();
                var streaks = new List<int>();
                var current = 0;

                for (var index = 0; index < days.Count; index++)
                {
                    current = index > 0 && days[index] == days[index - 1].AddDays(1) ? current + 1 : 1;
                    streaks.Add(current);
                }

                var name = group
                    .Select(message => message.ContactName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    ?? $"User {group.Key}";

                return new ConversationStreakDto(
                    name,
                    streaks[^1],
                    streaks.Max(),
                    days[^1],
                    avatarsByPeer.GetValueOrDefault(group.Key));
            })
            .Where(streak => streak.BestDays >= 3)
            .OrderByDescending(streak => streak.CurrentDays >= 3)
            .ThenByDescending(streak => streak.CurrentDays)
            .ThenByDescending(streak => streak.BestDays)
            .ThenBy(streak => streak.ContactName)
            .ToList();
    }

    private static DayOfWeek ToMondayFirst(DayOfWeek day) => day == DayOfWeek.Sunday ? DayOfWeek.Sunday : day;

    private static DateTime StartOfWeek(DateTime date)
    {
        var difference = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-difference);
    }
}
