using Microsoft.JSInterop;

namespace Datateal.Ui.Client.Services;

public sealed class ThemeService(IJSRuntime js) : IThemeService
{
    public event Action? ThemeChanged;

    public async Task<AppTheme> GetThemeAsync()
    {
        var stored = await js.InvokeAsync<string>("getStoredDatatealTheme");
        return stored switch
        {
            "dark" => AppTheme.Dark,
            "light" => AppTheme.Light,
            _ => AppTheme.Auto,
        };
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        var value = theme switch
        {
            AppTheme.Dark => "dark",
            AppTheme.Light => "light",
            _ => "auto",
        };
        await js.InvokeVoidAsync("setDatatealTheme", value);
        ThemeChanged?.Invoke();
    }
}
