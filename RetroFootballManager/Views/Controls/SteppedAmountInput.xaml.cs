namespace RetroFootballManager.Views.Controls;

// Retro-style "odometer" number display (000.000.000, German thousands separator) with a
// single up/down pair that adjusts Value by StepSize - StepSize is meant to be bound to a
// shared PresetStepInput next to this control, so the same +/- click can mean "+1" or
// "+1.000.000" depending on what the user picked there.
public partial class SteppedAmountInput : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(SteppedAmountInput), 0.0, BindingMode.TwoWay, propertyChanged: OnValueChanged);

    public static readonly BindableProperty StepSizeProperty = BindableProperty.Create(
        nameof(StepSize), typeof(double), typeof(SteppedAmountInput), 1000.0, BindingMode.TwoWay);

    private int _millions;
    private int _thousands;
    private int _units;

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StepSize
    {
        get => (double)GetValue(StepSizeProperty);
        set => SetValue(StepSizeProperty, value);
    }

    public int Millions
    {
        get => _millions;
        private set { if (_millions != value) { _millions = value; OnPropertyChanged(); } }
    }

    public int Thousands
    {
        get => _thousands;
        private set { if (_thousands != value) { _thousands = value; OnPropertyChanged(); } }
    }

    public int Units
    {
        get => _units;
        private set { if (_units != value) { _units = value; OnPropertyChanged(); } }
    }

    public SteppedAmountInput()
    {
        InitializeComponent();
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (SteppedAmountInput)bindable;
        long total = (long)Math.Clamp(Math.Round((double)newValue), 0, 999_999_999);
        control.Millions = (int)(total / 1_000_000);
        control.Thousands = (int)(total / 1_000 % 1_000);
        control.Units = (int)(total % 1_000);
    }

    private void OnUpClicked(object? sender, EventArgs e) => Value = Math.Min(999_999_999, Value + StepSize);

    private void OnDownClicked(object? sender, EventArgs e) => Value = Math.Max(0, Value - StepSize);
}
