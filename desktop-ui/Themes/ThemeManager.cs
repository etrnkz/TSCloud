using System;
using System.Windows;
using System.Windows.Media;

namespace TSCloud.Desktop.Themes
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Light;
        
        public static event EventHandler<AppTheme>? ThemeChanged;
        
        public static AppTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme(value);
                    ThemeChanged?.Invoke(null, value);
                }
            }
        }

        public static void ApplyTheme(AppTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            // Clear existing theme resources
            app.Resources.MergedDictionaries.Clear();

            // Add the selected theme
            var themeUri = theme switch
            {
                AppTheme.Dark => new Uri("pack://application:,,,/Themes/DarkTheme.xaml"),
                AppTheme.Light => new Uri("pack://application:,,,/Themes/LightTheme.xaml"),
                _ => new Uri("pack://application:,,,/Themes/LightTheme.xaml")
            };

            try
            {
                var themeDict = new ResourceDictionary { Source = themeUri };
                app.Resources.MergedDictionaries.Add(themeDict);
            }
            catch (Exception ex)
            {
                // Fallback to default theme if loading fails
                System.Diagnostics.Debug.WriteLine($"Failed to load theme: {ex.Message}");
            }
        }

        public static void ToggleTheme()
        {
            CurrentTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        }

        public static void InitializeTheme()
        {
            // Try to load saved theme preference
            var savedTheme = Properties.Settings.Default.Theme;
            if (Enum.TryParse<AppTheme>(savedTheme, out var theme))
            {
                CurrentTheme = theme;
            }
            else
            {
                CurrentTheme = AppTheme.Light;
            }
        }

        public static void SaveThemePreference()
        {
            Properties.Settings.Default.Theme = CurrentTheme.ToString();
            Properties.Settings.Default.Save();
        }
    }
}