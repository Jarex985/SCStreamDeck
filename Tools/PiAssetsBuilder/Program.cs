using System.Text;
using NUglify;
using NUglify.Css;
using NUglify.JavaScript;

internal static class Program
{
    private const string Lf = "\n";

    public static int Main(string[] args)
    {
        try
        {
            string piRoot = GetPiRoot(args);
            BuildAll(piRoot);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string GetPiRoot(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            throw new ArgumentException("Usage: PiAssetsBuilder <path-to-PluginCore/PropertyInspector>");
        }

        string piRoot = Path.GetFullPath(args[0]);
        if (!Directory.Exists(piRoot))
        {
            throw new DirectoryNotFoundException($"PropertyInspector directory not found: {piRoot}");
        }

        return piRoot;
    }

    private static void BuildAll(string piRoot)
    {
        string jsSrc = Path.Combine(piRoot, "js", "src");
        string jsOut = Path.Combine(piRoot, "js");
        string cssSrc = Path.Combine(piRoot, "css", "src");
        string cssOut = Path.Combine(piRoot, "css");

        EnsureDirectory(jsSrc);
        EnsureDirectory(cssSrc);

        BuildScComponents(jsSrc, jsOut);
        BuildPageScript(jsSrc, jsOut, "sc-preload.js");
        BuildPageScript(jsSrc, jsOut, "pi-control-panel.js");
        BuildPageScript(jsSrc, jsOut, "pi-function-key.js");
        BuildCss(cssSrc, cssOut);
    }

    private static void BuildScComponents(string jsSrc, string jsOut)
    {
        string[] inputs =
        [
            "sc-common.js",
            "sc-bus.js",
            "sc-dropdown.js",
            "sc-file-picker.js",
            "sc-theme.js"
        ];

        StringBuilder bundle = new();
        foreach (string file in inputs)
        {
            string path = Path.Combine(jsSrc, file);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Missing JS source: {path}");
            }

            bundle.Append(File.ReadAllText(path));
            bundle.Append(Lf);
            bundle.Append(';');
            bundle.Append(Lf);
        }

        CodeSettings settings = new() { PreserveImportantComments = false, OutputMode = OutputMode.SingleLine };

        UglifyResult result = Uglify.Js(bundle.ToString(), settings);
        ThrowIfUglifyFailed(result, "sc-components.js");

        string output = "/* generated: sc-components.js */" + Lf + result.Code + Lf;
        WriteIfChanged(Path.Combine(jsOut, "sc-components.js"), output);
    }

    private static void BuildPageScript(string jsSrc, string jsOut, string fileName)
    {
        string inputPath = Path.Combine(jsSrc, fileName);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Missing JS source: {inputPath}");
        }

        CodeSettings settings = new() { PreserveImportantComments = false, OutputMode = OutputMode.SingleLine };

        UglifyResult result = Uglify.Js(File.ReadAllText(inputPath), settings);
        ThrowIfUglifyFailed(result, fileName);

        string output = $"/* generated: {fileName} */" + Lf + result.Code + Lf;
        WriteIfChanged(Path.Combine(jsOut, fileName), output);
    }

    private static void BuildCss(string cssSrc, string cssOut)
    {
        string inputPath = Path.Combine(cssSrc, "base.css");
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Missing CSS source: {inputPath}");
        }

        CssSettings settings = new() { CommentMode = CssComment.None };

        UglifyResult result = Uglify.Css(File.ReadAllText(inputPath), settings);
        ThrowIfUglifyFailed(result, "base.css");

        string output = "/* generated: base.css */" + Lf + result.Code + Lf;
        WriteIfChanged(Path.Combine(cssOut, "base.css"), output);
    }

    private static void ThrowIfUglifyFailed(UglifyResult result, string label)
    {
        if (!result.HasErrors)
        {
            return;
        }

        StringBuilder msg = new();
        msg.AppendLine($"NUglify failed for {label}:");
        foreach (UglifyError? error in result.Errors)
        {
            msg.AppendLine($"- {error}");
        }

        throw new InvalidOperationException(msg.ToString().TrimEnd());
    }

    private static void WriteIfChanged(string outputPath, string content)
    {
        string? existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
        if (string.Equals(existing, content, StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content, new UTF8Encoding(false));
        Console.WriteLine($"Wrote {outputPath}");
    }

    private static void EnsureDirectory(string dir)
    {
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException($"Expected directory to exist: {dir}");
        }
    }
}
