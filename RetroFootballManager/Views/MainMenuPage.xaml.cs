using Microsoft.Maui.Layouts;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class MainMenuPage : ContentPage
    {
        private readonly MainMenuViewModel _viewModel;

        // Pixel rects on the original image (1536x1024, mainmenu_no_label.png).
        // RepositionHotspots remaps these to the actual window size using an AspectFill
        // cover formula, so the hit/tooltip area lands exactly on each object
        // (tactics board, window, trophy, ...) at any window size.
        private const double ImageNativeWidth = 1536;
        private const double ImageNativeHeight = 1024;

        private static readonly (string Name, Rect ImageRect)[] HotspotRects =
        [
            ("HsAufstellung", new Rect(1170, 270, 310, 250)),      // tactics board
            ("HsTraining", new Rect(700, 340, 110, 140)),          // green jersey
            ("HsTeamTraining", new Rect(810, 335, 110, 140)),      // red jersey #5
            ("HsJugend", new Rect(315, 645, 150, 90)),             // notepad in front of the chair
            ("HsFixtures", new Rect(985, 625, 110, 45)),           // newspaper on the coffee table
            ("HsStatistics", new Rect(500, 525, 120, 125)),        // green banker's lamp
            ("HsCupOverview", new Rect(1040, 435, 120, 140)),      // trophy by the jerseys
            ("HsTrophies", new Rect(565, 310, 145, 195)),          // big trophy left (by the photos), incl. base
            ("HsScouting", new Rect(680, 545, 210, 175)),          // laptop (screen + base)
            ("HsClub", new Rect(20, 470, 170, 160)),               // globe by the window
            ("HsStaff", new Rect(825, 215, 150, 150)),             // large photo (coaching staff)
            ("HsSponsors", new Rect(1200, 575, 290, 260)),         // armchair on the right
            ("HsFinances", new Rect(440, 720, 160, 100)),          // large paper document
            ("HsStadium", new Rect(15, 165, 425, 325)),            // entire window
            ("HsTransferMarket", new Rect(120, 760, 220, 100)),    // phone
            ("HsSaveGame", new Rect(115, 600, 130, 150)),          // golden trophy/orb on the desk (narrow, keeps the phone area free)
            ("HsBackToStart", new Rect(1470, 0, 66, 1024)),        // wall strip right of the tactics board, full height
        ];

        public MainMenuPage(MainMenuViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RepositionHotspots();
            await _viewModel.InitializeAsync();
        }

        private void HotspotLayer_SizeChanged(object? sender, EventArgs e)
        {
            RepositionHotspots();
        }

        private void RepositionHotspots()
        {
            var containerW = HotspotLayer.Width;
            var containerH = HotspotLayer.Height;
            if (containerW <= 0 || containerH <= 0)
                return;

            // Aspect="AspectFill" cover formula (like CSS background-size:cover):
            // scale uniformly until the image fully covers the container, then
            // crop the excess symmetrically.
            var scale = Math.Max(containerW / ImageNativeWidth, containerH / ImageNativeHeight);
            var renderedW = ImageNativeWidth * scale;
            var renderedH = ImageNativeHeight * scale;
            var offsetX = (renderedW - containerW) / 2.0;
            var offsetY = (renderedH - containerH) / 2.0;

            foreach (var (name, rect) in HotspotRects)
            {
                if (FindByName(name) is not View view)
                    continue;

                var bounds = new Rect(
                    rect.X * scale - offsetX,
                    rect.Y * scale - offsetY,
                    rect.Width * scale,
                    rect.Height * scale);

                AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
                AbsoluteLayout.SetLayoutBounds(view, bounds);
            }
        }
    }
}
