using Jint;
using Jint.Native;

namespace Quantumwake.WebTests;

/// <summary>
/// The 3D viewer's module, <c>web/viewer3d.js</c>, loaded headless.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Page"/> runs the dashboard, and <c>ModelViewTests</c> cover what
/// the dashboard does with the viewer: the catalogue page, the hand-off from
/// the Garage and the Armoury. The module itself was untested, and it is where
/// the formats live - the packed mesh the server writes, the DDS header a
/// texture is read from, the rule that decides which sub-materials are the
/// inside of a hull. Those are arithmetic over bytes and names, and none of
/// them needs a GPU.
/// </para>
/// <para>
/// three.js is not loaded. Its vendor imports are rewritten to read from a
/// stub that holds the handful of constants the parsers compare against, so a
/// test asserts what the viewer decides and never what WebGL drew. Opening a
/// model needs a renderer and is out of reach here; the server side of that
/// path is <c>ModelReadingTests</c>.
/// </para>
/// </remarks>
public sealed class Viewer
{
    private readonly Engine _engine;

    /// <summary>Anything the module wrote to the console, for diagnosing a failure.</summary>
    public List<string> Log { get; } = [];

    public Viewer()
    {
        _engine = new Engine(options => options
            .LimitRecursion(600)
            .TimeoutInterval(TimeSpan.FromSeconds(10))
            .Strict(false));

        _engine.SetValue("host_log", new Action<string>(line => Log.Add(line)));

        Run(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dom.js")), "dom.js");
        Run(ThreeStub, "three-stub.js");

        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "viewer3d.js"));
        _engine.Modules.Add("viewer3d", WithoutVendorImports(source));

        try
        {
            _engine.Modules.Import("viewer3d");
        }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException(
                $"viewer3d.js threw at line {e.Location.Start.Line}: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }

        // The module's exports, reachable from every expression a test writes.
        _engine.Execute("const __viewer = __viewerModule;");
    }

    /// <summary>
    /// The constants the viewer compares against, in place of three.js.
    /// </summary>
    /// <remarks>
    /// The values are WebGL's own enum numbers, as three.js defines them, so a
    /// test can say "that is DXT1" by number rather than by trusting the stub.
    /// Everything else the module names at import time is a class it only
    /// constructs when a model opens, which never happens here.
    /// </remarks>
    private const string ThreeStub = """
        globalThis.__THREE = {
          RGBA_S3TC_DXT1_Format: 33777,
          RGBA_S3TC_DXT3_Format: 33778,
          RGBA_S3TC_DXT5_Format: 33779,
          RED_RGTC1_Format: 36283,
          SIGNED_RED_RGTC1_Format: 36284,
          RED_GREEN_RGTC2_Format: 36285,
          SIGNED_RED_GREEN_RGTC2_Format: 36286,
          RGBA_BPTC_Format: 36492,
          SRGBColorSpace: 'srgb',
          Color: class {
            constructor(r, g, b) { this.r = r || 0; this.g = g || 0; this.b = b || 0; }
            setRGB(r, g, b) { this.r = r; this.g = g; this.b = b; return this; }
          },
          OrbitControls: class {}, RoomEnvironment: class {}, EffectComposer: class {},
          RenderPass: class {}, GTAOPass: class {}, OutputPass: class {}, UnrealBloomPass: class {},
        };

        /* What the module announces when it has loaded: app.js waits on this
           event before it asks the viewer for anything. */
        globalThis.__events = [];
        globalThis.dispatchEvent = (e) => { globalThis.__events.push(e.type); return true; };
        """;

    /// <summary>
    /// The module with its vendor imports read from the stub instead.
    /// </summary>
    /// <remarks>
    /// Deliberately strict: an import this does not recognise fails the
    /// harness, so a new dependency is noticed rather than quietly resolved
    /// against a file the test output does not carry.
    /// </remarks>
    private static string WithoutVendorImports(string source)
    {
        var lines = source.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (!line.StartsWith("import ", StringComparison.Ordinal)) continue;

            var star = System.Text.RegularExpressions.Regex.Match(line, @"^import \* as (\w+) from '\./vendor/three/[^']+';$");
            var named = System.Text.RegularExpressions.Regex.Match(line, @"^import \{([^}]+)\} from '\./vendor/three/[^']+';$");

            lines[i] = star.Success ? $"const {star.Groups[1].Value} = globalThis.__THREE;"
                : named.Success ? $"const {{{named.Groups[1].Value}}} = globalThis.__THREE;"
                : throw new InvalidOperationException($"viewer3d.js has an import the harness does not know: {line}");
        }

        // A module's namespace is only reachable from another module, so the
        // module itself hands its exports to the page: `export` stays as it
        // is, and a line at the end collects them.
        return string.Join('\n', lines)
            + "\nglobalThis.__viewerModule = { parseMesh, parseDds, interiorSub, openModel, closeModel };\n";
    }

    private void Run(string source, string what)
    {
        try
        {
            _engine.Execute(source);
        }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException(
                $"{what} threw at line {e.Location.Start.Line}: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }
    }

    /// <summary>Evaluates an expression in the module's page and returns it as C#.</summary>
    public object? Eval(string expression)
    {
        try
        {
            return _engine.Evaluate(expression).ToObject();
        }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException($"{expression} threw: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }
    }

    public string Text(string expression) => Eval(expression)?.ToString() ?? string.Empty;

    public double Number(string expression) => Convert.ToDouble(Eval(expression));

    public bool Truth(string expression) => Convert.ToBoolean(Eval(expression));

    /// <summary>Runs statements - building a buffer, keeping a result for the next expression.</summary>
    public void Do(string statements) => Run(statements, "test statements");
}
