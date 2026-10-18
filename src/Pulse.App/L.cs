using Microsoft.UI.Xaml.Markup;
using Pulse.Infrastructure;

namespace Pulse.App;

internal static class L
{
    public static string SystemLanguage { get; } = System.Globalization.CultureInfo.CurrentUICulture.Name;
    public static LanguageCatalog Catalog { get; } = new(Path.Combine(AppContext.BaseDirectory, "languages"),
        (path, error) => new DiagnosticLog(((App)Microsoft.UI.Xaml.Application.Current).LaunchOptions.DataDirectory)
            .Write("error", "Invalid language pack: " + Path.GetFileName(path), error));
    public static string T(string text) => Catalog.Text(text);
    public static string F(FormattableString text) => Catalog.Format(text.Format, text.GetArguments());
    public static Microsoft.UI.Xaml.FlowDirection Direction => Catalog.Selected.RightToLeft
        ? Microsoft.UI.Xaml.FlowDirection.RightToLeft : Microsoft.UI.Xaml.FlowDirection.LeftToRight;
    public static void Options(Microsoft.UI.Xaml.Controls.ComboBox box) => box.ItemTemplate =
        (Microsoft.UI.Xaml.DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["LocalizedChoiceTemplate"];
    public static void Select(string code) => Catalog.Select(code, SystemLanguage);
}

public sealed class LanguageLabelConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => L.T(value?.ToString() ?? "");
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    protected override object ProvideValue() => L.T(Key);
}
