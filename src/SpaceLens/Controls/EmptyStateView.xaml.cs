using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SpaceLens.Controls;

public partial class EmptyStateView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyStateView), new PropertyMetadata("Scan to get started"));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public static readonly DependencyProperty Hint1Property =
        DependencyProperty.Register(nameof(Hint1), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public static readonly DependencyProperty Hint2Property =
        DependencyProperty.Register(nameof(Hint2), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public static readonly DependencyProperty Hint3Property =
        DependencyProperty.Register(nameof(Hint3), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(nameof(ShowActions), typeof(bool), typeof(EmptyStateView), new PropertyMetadata(true));

    public static readonly DependencyProperty EyebrowProperty =
        DependencyProperty.Register(nameof(Eyebrow), typeof(string), typeof(EmptyStateView), new PropertyMetadata("NOT YET SCANNED"));

    public EmptyStateView()
    {
        // Local converter so the control works without Window.Resources
        Resources.Add("EmptyHintVis", new EmptyHintVisibilityConverter());
        Resources.Add("BoolToVis", new BooleanToVisibilityConverter());
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Hint1
    {
        get => (string)GetValue(Hint1Property);
        set => SetValue(Hint1Property, value);
    }

    public string Hint2
    {
        get => (string)GetValue(Hint2Property);
        set => SetValue(Hint2Property, value);
    }

    public string Hint3
    {
        get => (string)GetValue(Hint3Property);
        set => SetValue(Hint3Property, value);
    }

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public string Eyebrow
    {
        get => (string)GetValue(EyebrowProperty);
        set => SetValue(EyebrowProperty, value);
    }
}

/// <summary>Shows an element only when the bound string is non-empty.</summary>
public sealed class EmptyHintVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => Binding.DoNothing;
}
