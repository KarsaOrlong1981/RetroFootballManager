namespace RetroFootballManager.Behaviors;

// Entry.Keyboard="Numeric" only picks the on-screen keyboard layout - a physical keyboard
// (desktop/Windows) can still type letters and symbols into the field. This actively strips
// any non-digit character as the user types, so the field can never end up non-numeric
// regardless of input method.
public class NumericOnlyBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        bindable.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(bindable);
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue is null)
            return;

        string digitsOnly = new(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (digitsOnly != e.NewTextValue)
            entry.Text = digitsOnly;
    }
}
