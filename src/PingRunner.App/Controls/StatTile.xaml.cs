using System.Windows;
using PingRunner.App.ViewModels;

namespace PingRunner.App.Controls;

/// <summary>One figure on a card: a label, the value (colored by its severity) and a hint underneath.</summary>
public partial class StatTile
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(StatTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(Severity), typeof(StatTile), new PropertyMetadata(Severity.Neutral));

    public static readonly DependencyProperty ValueSizeProperty =
        DependencyProperty.Register(nameof(ValueSize), typeof(double), typeof(StatTile), new PropertyMetadata(22d));

    public StatTile()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public Severity Severity
    {
        get => (Severity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public double ValueSize
    {
        get => (double)GetValue(ValueSizeProperty);
        set => SetValue(ValueSizeProperty, value);
    }
}
