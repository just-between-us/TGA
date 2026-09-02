namespace TGA.UI.HelperServices;

public static class AvatarColorHelper
{
    private static readonly string[] Colors =
    [
        "#F44336", // Красный
        "#E91E63", // Розовый
        "#9C27B0", // Пурпурный
        "#673AB7", // Фиолетовый
        "#3F51B5", // Индиго
        "#2196F3", // Синий
        "#03A9F4", // Светло-синий
        "#00BCD4", // Бирюзовый
        "#009688", // Зеленый
        "#4CAF50", // Салатовый
        "#8BC34A", // Светло-зеленый
        "#CDDC39", // Лайм
        "#FFEB3B", // Желтый
        "#FFC107", // Янтарный
        "#FF9800", // Оранжевый
        "#FF5722"  // Глубокий оранжевый
    ];

    public static string GetColorForId(long id)
    {
        var index = Math.Abs(id) % Colors.Length;
        return Colors[index];
    }
}