using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// One MFD panel, running headless.
/// </summary>
/// <remarks>
/// <para>
/// The page rules in <c>mfd-core.js</c> are pure and were already testable on
/// their own. What was not was the thing between them and the pilot's thumb:
/// <c>press()</c> deciding whether a button does anything, and
/// <c>confirmSelected()</c> deciding what a second press confirms. Both had a
/// bug that no test of the rules could have caught, because both were failing
/// to ask the rules at all.
/// </para>
/// <para>
/// The same <c>dom.js</c> the dashboard's harness uses, which already carries a
/// routing table for fetch and a record of what was asked - which is exactly
/// what "did that press write to my flight plan?" needs.
/// </para>
/// </remarks>
public sealed class Panel
{
    private readonly Engine _engine;

    public Panel(string plan = "null")
    {
        _engine = new Engine(options => options
            .LimitRecursion(600)
            .TimeoutInterval(TimeSpan.FromSeconds(10))
            .Strict(false));

        _engine.SetValue("host_log", new Action<string>(_ => { }));
        Run(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dom.js")));
        Seed();

        Run($"__fetch.routes['/api/briefing'] = {plan};");

        Run(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        Run(WithoutAutoStart(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd.js"))));

        // The state a running panel would have reached, set rather than awaited.
        // Booting it here would mean every test racing four fetches and a live
        // stream to say something about one button press.
        Run($"briefing = {plan}; state = {{ connected: true, inGame: true }};");
        Run("drawLabels(); render();");
    }

    /// <summary>
    /// The panel's script with its last line - <c>bootMfd()</c> - left out.
    /// </summary>
    /// <remarks>
    /// Loading the script should define the panel, not start it. Booting opens
    /// a stream and four fetches, and none of that is what a test about a
    /// button press is asking.
    /// </remarks>
    private static string WithoutAutoStart(string script) =>
        script.Replace("\nbootMfd();", "\n/* boot omitted by the harness */");

    /// <summary>
    /// The elements mfd.js writes to, since the stub does not parse markup.
    /// </summary>
    /// <remarks>
    /// Read from <c>mfd.html</c> rather than listed here, so a element added to
    /// the panel cannot leave the harness quietly addressing nothing.
    /// </remarks>
    private void Seed()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd.html"));

        foreach (System.Text.RegularExpressions.Match tag in
                 System.Text.RegularExpressions.Regex.Matches(markup, "<[a-zA-Z][^>]*>"))
        {
            var id = System.Text.RegularExpressions.Regex.Match(tag.Value, @"id=""([^""]+)""");
            if (!id.Success) continue;

            Run($"__dom.node('#{id.Groups[1].Value}');");
            if (System.Text.RegularExpressions.Regex.IsMatch(tag.Value, @"\shidden(\s|>|=)"))
                Run($"__dom.node('#{id.Groups[1].Value}').hidden = true;");
        }
    }

    private void Run(string script)
    {
        try { _engine.Execute(script); }
        catch (Jint.Runtime.JavaScriptException e)
        {
            throw new InvalidOperationException($"panel threw: {e.Message}\n{e.JavaScriptStackTrace}", e);
        }
    }

    /// <summary>Presses a Cougar button, the way the host forwards one.</summary>
    public Panel Press(int button) { Run($"press({button});"); return this; }

    public Panel Do(string statements) { Run(statements); return this; }

    public object? Eval(string expression) => _engine.Evaluate(expression).ToObject();

    public string Text(string expression) => Eval(expression)?.ToString() ?? string.Empty;

    /// <summary>What the panel wrote to the plan, if anything.</summary>
    public IReadOnlyList<string> Writes() =>
        ((object[])Eval("__fetch.calls.filter(c => c.method !== 'GET').map(c => c.method + ' ' + c.url)")!)
            .Select(c => c?.ToString() ?? string.Empty)
            .ToList();

    /// <summary>The pinned action strip, and the footer the frame answers on.</summary>
    public string Strip => Text("__dom.node('#action-text').textContent");
    public string StripState => Text("__dom.node('#action').className");
    public string Footer => Text("__dom.node('#last-input').textContent");
    public string Title => Text("__dom.node('#title').textContent");
}
