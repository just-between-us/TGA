namespace TGA.UI.HelperServices;

public class ThemeService
{
    private bool _isDarkMode = true;

    public event Action? OnThemeChanged;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public void ToggleTheme() => IsDarkMode = !IsDarkMode;
}