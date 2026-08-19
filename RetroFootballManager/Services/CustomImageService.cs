namespace RetroFootballManager.Services
{
    public class CustomImageService
    {
        private static readonly string RootDirectory = Path.Combine(FileSystem.AppDataDirectory, "Images");

        public async Task<string> SaveImageAsync(string sourceFilePath, string category, int entityId)
        {
            var directory = Path.Combine(RootDirectory, category);
            Directory.CreateDirectory(directory);

            var extension = Path.GetExtension(sourceFilePath);
            var destination = Path.Combine(directory, $"{entityId}_{Guid.NewGuid():N}{extension}");

            using var source = File.OpenRead(sourceFilePath);
            using var target = File.Create(destination);
            await source.CopyToAsync(target);

            return destination;
        }
        public void DeleteImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
                return;

            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
