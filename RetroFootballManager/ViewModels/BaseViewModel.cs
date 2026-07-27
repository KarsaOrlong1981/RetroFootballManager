using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using RetroFootballManager.Logging;

namespace RetroFootballManager.ViewModels
{
    public abstract partial class BaseViewModel : ObservableRecipient
    {
        private static readonly ILog Log = LogManager.GetLogger<BaseViewModel>();

        protected IDispatcher Dispatcher { get; }

        protected BaseViewModel(IDispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;

        protected void RaisePropertyChangedOnUI([CallerMemberName] string? propertyName = null)
        {
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    OnPropertyChanged(propertyName);
                }
                catch (Exception ex)
                {
                    Log.Fatal($"Failed to raise PropertyChanged for '{propertyName}' on the UI thread.", ex);
                }
            });
        }

        protected void RaisePropertyChangedOnUI(params string[] propertyNames)
        {
            Dispatcher.Dispatch(() =>
            {
                foreach (var name in propertyNames)
                {
                    try
                    {
                        OnPropertyChanged(name);
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal($"Failed to raise PropertyChanged for '{name}' on the UI thread.", ex);
                    }
                }
            });
        }
    }
}
