namespace RetroFootballManager.Views.Controls;

// Cycles through a fixed set of round step sizes (1, 5, 10, 100, 1.000, ...) instead of a
// free-form increment - meant to be bound to SteppedAmountInput.StepSize so the user can
// pick how big each click on the amount's +/- buttons is.
public partial class PresetStepInput : ContentView
{
    private static readonly double[] Presets = [1, 5, 10, 100, 1_000, 10_000, 100_000, 1_000_000];

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(PresetStepInput), Presets[0], BindingMode.TwoWay, propertyChanged: OnValueChanged);

    private string _displayText = FormatValue(Presets[0]);

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string DisplayText
    {
        get => _displayText;
        private set { if (_displayText != value) { _displayText = value; OnPropertyChanged(); } }
    }

    public PresetStepInput()
    {
        InitializeComponent();
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((PresetStepInput)bindable).DisplayText = FormatValue((double)newValue);

    private static string FormatValue(double value) => value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

    private int CurrentIndex()
    {
        int index = Array.IndexOf(Presets, Value);
        return index >= 0 ? index : 0;
    }

    private void OnUpClicked(object? sender, EventArgs e)
    {
        int next = Math.Min(Presets.Length - 1, CurrentIndex() + 1);
        Value = Presets[next];
    }

    private void OnDownClicked(object? sender, EventArgs e)
    {
        int previous = Math.Max(0, CurrentIndex() - 1);
        Value = Presets[previous];
    }
}
