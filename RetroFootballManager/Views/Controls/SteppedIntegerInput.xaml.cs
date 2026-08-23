namespace RetroFootballManager.Views.Controls;

// Small integer +/- stepper with explicit dark-theme styling - the native MAUI Stepper
// renders with the OS default look on Windows, which is effectively invisible against this
// app's dark background outside of hover/focus. Used for small-range values (contract years,
// loan duration, wage-share %).
public partial class SteppedIntegerInput : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(int), typeof(SteppedIntegerInput), 0, BindingMode.TwoWay, propertyChanged: OnDisplayAffectingPropertyChanged);

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(int), typeof(SteppedIntegerInput), 0);

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(int), typeof(SteppedIntegerInput), 100);

    public static readonly BindableProperty IncrementProperty =
        BindableProperty.Create(nameof(Increment), typeof(int), typeof(SteppedIntegerInput), 1);

    public static readonly BindableProperty SuffixProperty = BindableProperty.Create(
        nameof(Suffix), typeof(string), typeof(SteppedIntegerInput), string.Empty, propertyChanged: OnDisplayAffectingPropertyChanged);

    private string _displayText = "0";

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Increment
    {
        get => (int)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public string DisplayText
    {
        get => _displayText;
        private set { if (_displayText != value) { _displayText = value; OnPropertyChanged(); } }
    }

    public SteppedIntegerInput()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnDisplayAffectingPropertyChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((SteppedIntegerInput)bindable).UpdateDisplay();

    private void UpdateDisplay() => DisplayText = string.IsNullOrEmpty(Suffix) ? $"{Value}" : $"{Value}{Suffix}";

    private void OnDownClicked(object? sender, EventArgs e) => Value = Math.Max(Minimum, Value - Increment);

    private void OnUpClicked(object? sender, EventArgs e) => Value = Math.Min(Maximum, Value + Increment);
}
