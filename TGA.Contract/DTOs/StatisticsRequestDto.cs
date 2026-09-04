namespace TGA.Contract.DTOs;

public enum StatisticsGrouping
{
    Day,
    Week,
    Month
}

public record StatisticsRequestDto(
    DateTime From,
    DateTime To,
    StatisticsGrouping Grouping);
