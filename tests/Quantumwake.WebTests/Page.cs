using Jint;
using Jint.Native;
using Jint.Native.Object;

namespace Quantumwake.WebTests;

/// <summary>
/// The dashboard's script, running headless.
/// </summary>
/// <remarks>
/// <para>
/// <c>web/app.js</c> is a few thousand lines of consequential logic - what a
/// commodity is worth where, which stop comes next, what colour a price is -
/// and none of it was reachable from a test: it needs a browser, and this
/// machine has no Node. Jint plus <c>dom.js</c> gives the script enough of a
/// browser to run in-process, so the arithmetic and the rendering can be
/// asserted the same way the C# is.
/// </para>
/// <para>
/// What this does not test is layout, CSS, or anything the browser itself
/// decides. It tests what the code puts into the page.
/// </para>
/// </remarks>
public sealed class Page
{
    private readonly Engine _engine;

    public Page()
    {
        // A page that never finishes is a failing test, not a hanging build:
        // the app has poll loops that only stop because a browser's timers do,
        // and the stub's timers never fire at all.
        _engine = new Engine(options => options
            .LimitRecursion(600)
            .TimeoutInterval(TimeSpan.FromSeconds(10))
            .Strict(false));

        _engine.SetValue("host_log", new Action<string>(line => Log.Add(line)));

        Run(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dom.js")), "dom.js");
        SeedFromMarkup();
        Run(WithoutAutoStart(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "app.js"))), "app.js");
    }

    /// <summary>
    /// Starts the panels, cards and controls in the state the markup gives them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stub does not parse the document - the assertions are about what the
    /// page writes, not how the markup nests - but "is this still hidden?" and
    /// "is this box ticked?" are real questions about half the features here,
    /// and a stub where everything starts visible and unticked answers both
    /// wrongly every time. So the attributes that carry initial state are read
    /// from the markup itself, rather than each test remembering to set them.
    /// </para>
    /// <para>
    /// <c>selected</c> is seeded onto the select rather than the option, because
    /// the stub models a select's value and not its option list until the page
    /// fills one in.
    /// </para>
    /// </remarks>
    private void SeedFromMarkup()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

        string? openSelect = null;

        foreach (System.Text.RegularExpressions.Match tag in
                 System.Text.RegularExpressions.Regex.Matches(markup, "<[a-zA-Z/][^>]*>"))
        {
            // A selected option belongs to whichever select is currently open.
            if (tag.Value.StartsWith("</select", StringComparison.OrdinalIgnoreCase))
                openSelect = null;

            var id = System.Text.RegularExpressions.Regex.Match(tag.Value, @"id=""([^""]+)""");

            if (tag.Value.StartsWith("<select", StringComparison.OrdinalIgnoreCase))
                openSelect = id.Success ? id.Groups[1].Value : null;

            if (openSelect is not null
                && tag.Value.StartsWith("<option", StringComparison.OrdinalIgnoreCase)
                && System.Text.RegularExpressions.Regex.IsMatch(tag.Value, @"\sselected(\s|>|=)"))
            {
                var value = System.Text.RegularExpressions.Regex.Match(tag.Value, @"value=""([^""]*)""");
                if (value.Success)
                {
                    _engine.Execute(
                        $"__dom.node({Quote($"#{openSelect}")}).value = {Quote(value.Groups[1].Value)};");
                }
            }

            if (!id.Success)
                continue;

            if (System.Text.RegularExpressions.Regex.IsMatch(tag.Value, @"\shidden(\s|>|=)"))
                _engine.Execute($"__dom.node({Quote($"#{id.Groups[1].Value}")}).hidden = true;");

            if (System.Text.RegularExpressions.Regex.IsMatch(tag.Value, @"\schecked(\s|>|=)"))
                _engine.Execute($"__dom.node({Quote($"#{id.Groups[1].Value}")}).checked = true;");
        }
    }

    /// <summary>
    /// The page's script with its last line - <c>boot()</c> - left out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loading the script should define the page, not start it. <c>boot()</c>
    /// warms the whole dashboard up and then polls until the server answers,
    /// which in a browser is paced by timers; with the stub's timers inert it
    /// is an unbounded loop, and every test would wait for it before asserting
    /// anything.
    /// </para>
    /// <para>
    /// Deliberately strict: if the call is ever renamed the harness stops
    /// rather than quietly testing a page that never loaded.
    /// </para>
    /// </remarks>
    private static string WithoutAutoStart(string source)
    {
        const string autoStart = "\nboot();";

        var at = source.LastIndexOf(autoStart, StringComparison.Ordinal);

        if (at < 0)
            throw new InvalidOperationException(
                "web/app.js no longer ends by calling boot(); the harness needs updating.");

        return source.Remove(at, autoStart.Length);
    }

    /// <summary>Anything the page wrote to the console, for diagnosing a failure.</summary>
    public List<string> Log { get; } = [];

    private void Run(string source, string what)
    {
        try
        {
            _engine.Execute(source);
            Settle();
        }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException(
                $"{what} threw at line {e.Location.Start.Line}: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }
        catch (TimeoutException e)
        {
            throw new InvalidOperationException(
                $"{what} never finished. Page log:\n  {string.Join("\n  ", Log)}", e);
        }
    }

    /// <summary>
    /// Runs whatever promise work the last call queued.
    /// </summary>
    /// <remarks>
    /// The page is full of <c>async</c> handlers, and a test that asserted
    /// before the queue drained would be testing the moment before the answer.
    /// Timers are inert in the stub, so this settles everything that can settle.
    /// </remarks>
    private void Settle()
    {
        for (var pass = 0; pass < 8; pass++)
            _engine.Advanced.ProcessTasks();
    }

    /// <summary>Evaluates an expression in the page and returns it as C#.</summary>
    public object? Eval(string expression)
    {
        var value = _engine.Evaluate(expression);
        Settle();
        return value.ToObject();
    }

    public string Text(string expression) => Eval(expression)?.ToString() ?? string.Empty;

    public double Number(string expression) => Convert.ToDouble(Eval(expression));

    public bool Truth(string expression) => Convert.ToBoolean(Eval(expression));

    public int Count(string expression) => (int)Number(expression);

    /// <summary>
    /// Runs statements in the page - setting state, calling a render.
    /// </summary>
    /// <remarks>
    /// Anything awaiting is wrapped in an async call first, because a script has
    /// no top level to await at, and its failure is caught and re-thrown here:
    /// a rejected promise nobody looked at would otherwise pass as a green test.
    /// </remarks>
    public void Do(string statements)
    {
        var awaits = statements.Contains("await", StringComparison.Ordinal);

        var source = awaits
            ? $$"""
                __error = null;
                (async () => { {{statements}} })()
                  .catch(e => { __error = String((e && e.stack) || e); });
                """
            : statements;

        try
        {
            _engine.Execute(source);
            Settle();

            if (awaits && _engine.Evaluate("__error").ToObject() is string failure)
                throw new InvalidOperationException($"page threw while awaiting: {failure}");
        }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException(
                $"page threw: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }
    }

    /// <summary>Answers one URL the page fetches with a canned body.</summary>
    public void Serve(string url, string json) =>
        Do($"__fetch.routes[{Quote(url)}] = {json};");

    /// <summary>Every URL the page has fetched, in order.</summary>
    public IReadOnlyList<string> Fetched() =>
        ((object[])Eval("__fetch.calls.map(c => c.method + ' ' + c.url)")!)
            .Select(c => c?.ToString() ?? string.Empty)
            .ToList();

    /// <summary>
    /// What the page last sent to one URL, as the JSON it wrote.
    /// </summary>
    /// <remarks>
    /// Only calls carrying a body count: writing to the API is nearly always
    /// followed by reading the same URL back, and the read would otherwise be
    /// the "last call" and report nothing sent.
    /// </remarks>
    public string BodyOf(string url) =>
        Text($"(__fetch.calls.filter(c => c.url === {Quote(url)} && c.body).pop() || {{}}).body || ''");

    /// <summary>The text of one element the page wrote to, by selector.</summary>
    public string NodeText(string selector) => Text($"__dom.node({Quote(selector)}).textContent");

    public static string Quote(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
