using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    // "Create your manager" - shown once, before team selection, at the start of a new
    // career. Builds a ManagerProfile the same way ManagerProfileGenerator builds one for AI
    // teams (same license/skill budget table for the currently unlocked tier), except the
    // human distributes the points themselves instead of them being rolled.
    public partial class ManagerCreationViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ManagerCreationViewModel>();

        private readonly GameSession _session;
        private readonly INavigationService _navigation;

        public ManagerCreationViewModel(
            IDispatcher dispatcher, CareerService career, GameSession session, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _navigation = navigation;
            Title = "Trainer erstellen";

            int tier = career.HighestUnlockedTier;
            var (license, ceiling, floor) = ManagerProfileGenerator.GetBudget(tier);
            License = license;
            LicenseLabel = CoachingLicenseDisplay.Label(license);

            RemainingPoints = ManagerProfileGenerator.GetSkillPointBudget(tier) - floor * 5;

            Skills =
            [
                new ManagerSkillSlot("Trainingsgestaltung", floor, floor, ceiling, () => RemainingPoints, d => RemainingPoints += d),
                new ManagerSkillSlot("Motivationsfähigkeit", floor, floor, ceiling, () => RemainingPoints, d => RemainingPoints += d),
                new ManagerSkillSlot("Offensivkreation", floor, floor, ceiling, () => RemainingPoints, d => RemainingPoints += d),
                new ManagerSkillSlot("Defensivorganisation", floor, floor, ceiling, () => RemainingPoints, d => RemainingPoints += d),
                new ManagerSkillSlot("In-Game-Coaching", floor, floor, ceiling, () => RemainingPoints, d => RemainingPoints += d),
            ];

            BirthDate = DateTime.Today.AddYears(-45);
        }

        public CoachingLicense License { get; }

        [ObservableProperty]
        private string _licenseLabel = string.Empty;

        public ObservableCollection<ManagerSkillSlot> Skills { get; }

        [ObservableProperty]
        private int _remainingPoints;

        [ObservableProperty]
        private string _firstName = string.Empty;

        [ObservableProperty]
        private string _lastName = string.Empty;

        [ObservableProperty]
        private DateTime _birthDate;

        [ObservableProperty]
        private string? _avatarPath;

        partial void OnFirstNameChanged(string value) => ContinueCommand.NotifyCanExecuteChanged();
        partial void OnLastNameChanged(string value) => ContinueCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private async Task PickAvatar()
        {
            try
            {
#pragma warning disable CS0618 // no PickPhotosAsync overload available in this MAUI version
                var result = await MediaPicker.Default.PickPhotoAsync();
#pragma warning restore CS0618
                if (result is null)
                    return;

                string destDir = Path.Combine(FileSystem.AppDataDirectory, "avatars");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, $"{Guid.NewGuid():N}{Path.GetExtension(result.FileName)}");

                await using var sourceStream = await result.OpenReadAsync();
                await using var destStream = File.Create(destPath);
                await sourceStream.CopyToAsync(destStream);

                AvatarPath = destPath;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to pick an avatar photo.", ex);
            }
        }

        private bool CanContinue() => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);

        [RelayCommand(CanExecute = nameof(CanContinue))]
        private async Task Continue()
        {
            var profile = new ManagerProfile
            {
                IsHuman = true,
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                BirthDate = BirthDate,
                AvatarPath = AvatarPath,
                License = License,
                TrainingDesign = Skills[0].Value,
                Motivation = Skills[1].Value,
                OffensiveCreation = Skills[2].Value,
                DefensiveOrganization = Skills[3].Value,
                InGameCoaching = Skills[4].Value,
                UnspentSkillPoints = RemainingPoints,
            };

            _session.PendingManagerProfile = profile;
            await _navigation.GoToAsync("teamselection");
        }
    }
}
