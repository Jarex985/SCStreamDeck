namespace Tests.Integration.Testing;

public abstract class IntegrationTestBase
{
    public static string ResolvePath(string relativePath)
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", relativePath));
    }
}
