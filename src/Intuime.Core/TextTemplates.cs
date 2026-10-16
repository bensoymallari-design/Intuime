using System.Globalization;
using System.Text.RegularExpressions;

namespace Intuime.Core;

public static class TextTemplates
{
    private static readonly Regex Token = new(@"\{\{\s*([A-Za-z0-9_\-]+)\s*\}\}", RegexOptions.Compiled);

    public static string Expand(string? template, ExperienceDocument document, DateTime? now = null)
    {
        if (string.IsNullOrEmpty(template))
            return "";
        var clock = now ?? DateTime.Now;
        return Token.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (name.Equals("time", StringComparison.OrdinalIgnoreCase))
                return clock.ToString("HH:mm", CultureInfo.CurrentCulture);
            if (name.Equals("date", StringComparison.OrdinalIgnoreCase))
                return clock.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
            var variable = document.Variables.FirstOrDefault(v =>
                v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return variable?.Value ?? "";
        });
    }

    public static string Display(SceneElement element, ExperienceDocument document, DateTime? now = null)
    {
        var clock = now ?? DateTime.Now;
        if (element.Kind == ElementKind.Clock)
        {
            var format = string.IsNullOrWhiteSpace(element.ClockFormat) ? "HH:mm" : element.ClockFormat;
            try
            {
                return clock.ToString(format, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return clock.ToString("HH:mm", CultureInfo.CurrentCulture);
            }
        }

        if (element.Kind == ElementKind.Counter)
        {
            var name = BindingName(element);
            var value = Get(document, name);
            return string.IsNullOrEmpty(value) ? "0" : value;
        }

        if (!string.IsNullOrWhiteSpace(element.VariableBinding) && string.IsNullOrEmpty(element.Text))
            return Get(document, element.VariableBinding);

        return Expand(element.Text, document, clock);
    }

    public static string BindingName(SceneElement element) =>
        string.IsNullOrWhiteSpace(element.VariableBinding) ? element.Name : element.VariableBinding;

    public static string Get(ExperienceDocument document, string name)
    {
        var variable = document.Variables.FirstOrDefault(v =>
            v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return variable?.Value ?? "";
    }

    public static void Set(ExperienceDocument document, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var variable = document.Variables.FirstOrDefault(v =>
            v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (variable == null)
        {
            document.Variables.Add(new ExperienceVariable { Name = name.Trim(), Value = value });
            return;
        }
        variable.Value = value;
    }

    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "0";
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
