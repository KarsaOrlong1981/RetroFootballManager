namespace RetroFootballManager.Services
{
    // Single source of truth for the display title shown in the custom NavigationBar,
    // keyed by the route names registered in AppShell.xaml/AppShell.xaml.cs.
    public static class RouteTitles
    {
        private static readonly Dictionary<string, string> Titles = new()
        {
            ["start"] = "Start",
            ["mainmenu"] = "Hauptmenü",
            ["managercreation"] = "Trainer erstellen",
            ["teamselection"] = "Vereinswahl",
            ["fixtures"] = "Spiele & Tabellen",
            ["cupoverview"] = "Pokal-Übersicht",
            ["trophies"] = "Trophäen",
            ["scouting"] = "Scouting",
            ["matchday"] = "Spieltag",
            ["cupmatchday"] = "Pokal-Spieltag",
            ["lineup"] = "Aufstellung",
            ["training"] = "Einzeltraining",
            ["teamtraining"] = "Team-Training",
            ["youth"] = "Jugend",
            ["statistics"] = "Statistiken",
            ["finances"] = "Finanzen",
            ["club"] = "Vereine",
            ["stadium"] = "Stadion",
            ["clubloan"] = "Ausleihe",
            ["sponsors"] = "Sponsoren",
            ["staff"] = "Mitarbeiter",
            ["transfermarket"] = "Transfermarkt",
            ["inbox"] = "Postfach",
            ["friendlymatchday"] = "Freundschaftsspiel",
            ["options"] = "Optionen",
        };

        public static string Resolve(string? locationUri)
        {
            var route = locationUri?.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s));
            return route is not null && Titles.TryGetValue(route, out var title) ? title : string.Empty;
        }
    }
}
