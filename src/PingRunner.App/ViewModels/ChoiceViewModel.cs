using System.Windows.Media;

namespace PingRunner.App.ViewModels;

/// <summary>One option in a settings list: its value, its label and, for accents, a swatch.</summary>
public sealed record ChoiceViewModel<T>(T Value, string Label, Brush? Swatch = null)
{
    public override string ToString() => Label;
}
