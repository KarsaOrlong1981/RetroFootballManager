using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RetroFootballManager.ViewModels
{
    // One skill row in the manager-creation point allocator (and later the "Punkte
    // verteilen" dialog, Phase 10e) - Increase/Decrease draw from/return to the parent's
    // shared point pool via the two callbacks instead of each slot owning its own budget.
    public partial class ManagerSkillSlot : ObservableObject
    {
        private readonly Func<int> _getRemaining;
        private readonly Action<int> _adjustRemaining;

        public ManagerSkillSlot(
            string name, int initialValue, int floor, int ceiling,
            Func<int> getRemaining, Action<int> adjustRemaining)
        {
            Name = name;
            _value = initialValue;
            Floor = floor;
            Ceiling = ceiling;
            _getRemaining = getRemaining;
            _adjustRemaining = adjustRemaining;
        }

        public string Name { get; }
        public int Floor { get; }
        public int Ceiling { get; }

        [ObservableProperty]
        private int _value;

        [RelayCommand]
        private void Increase()
        {
            if (Value < Ceiling && _getRemaining() > 0)
            {
                Value++;
                _adjustRemaining(-1);
            }
        }

        [RelayCommand]
        private void Decrease()
        {
            if (Value > Floor)
            {
                Value--;
                _adjustRemaining(1);
            }
        }
    }
}
