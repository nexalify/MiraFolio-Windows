namespace MiraFolio.Core.Utilities;

public static class AppDataPaths
{
    public const string ProductDirectoryName = "MiraFolio";

    public static string CurrentDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ProductDirectoryName);

    public static string PrepareFile(string fileName)
    {
        Directory.CreateDirectory(CurrentDirectory);
        return Path.Combine(CurrentDirectory, fileName);
    }
}
