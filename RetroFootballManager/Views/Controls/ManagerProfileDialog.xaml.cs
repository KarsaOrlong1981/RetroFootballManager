using System.Windows.Input;
using RetroFootballManager.Models;

namespace RetroFootballManager.Views.Controls
{
    // Manager/head-coach profile popup - shown read-only for AI teams (via TeamDetailDialog's
    // "Trainer" section) and, for the human's own profile, additionally offers a "Punkte
    // verteilen" allocator when UnspentSkillPoints > 0 (see StaffViewModel).
    public partial class ManagerProfileDialog : ContentView
    {
        public static readonly BindableProperty IsOpenProperty =
            BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(ManagerProfileDialog), false);

        public static readonly BindableProperty ProfileProperty =
            BindableProperty.Create(nameof(Profile), typeof(ManagerProfile), typeof(ManagerProfileDialog));

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(ManagerProfileDialog));

        public static readonly BindableProperty IsEditableProperty =
            BindableProperty.Create(nameof(IsEditable), typeof(bool), typeof(ManagerProfileDialog), false);

        public static readonly BindableProperty RemainingPointsProperty =
            BindableProperty.Create(nameof(RemainingPoints), typeof(int), typeof(ManagerProfileDialog), 0);

        public static readonly BindableProperty IncreaseSkillCommandProperty =
            BindableProperty.Create(nameof(IncreaseSkillCommand), typeof(ICommand), typeof(ManagerProfileDialog));

        public ManagerProfileDialog()
        {
            InitializeComponent();
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public ManagerProfile? Profile
        {
            get => (ManagerProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        // Only true for the viewer's own profile - AI profiles viewed via TeamDetailDialog
        // are always read-only, regardless of their own UnspentSkillPoints.
        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public int RemainingPoints
        {
            get => (int)GetValue(RemainingPointsProperty);
            set => SetValue(RemainingPointsProperty, value);
        }

        public ICommand? IncreaseSkillCommand
        {
            get => (ICommand?)GetValue(IncreaseSkillCommandProperty);
            set => SetValue(IncreaseSkillCommandProperty, value);
        }
    }
}
