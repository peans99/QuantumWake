namespace Quantumwake.WebTests;

/// <summary>
/// Help is an About drill-down rather than one more top-bar tab. The about tab
/// stays active, while the fragment can still open Help directly.
/// </summary>
public class HelpPageTests
{
    [Fact]
    public void Help_is_its_own_view_and_keeps_About_active()
    {
        var page = new Page();
        page.Do("window.scrollTo = () => {}; showView('help');");

        Assert.True(page.Truth("__dom.node('#view-help').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#view-about').classList.contains('active')"));
        Assert.True(page.Truth("__dom.node('#test-tab-about').classList.contains('active')"));

        page.Do("showView('about');");
        Assert.True(page.Truth("__dom.node('#view-about').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#view-help').classList.contains('active')"));
    }

    [Fact]
    public void Help_markup_covers_the_current_cockpit_and_screen_features()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

        Assert.Contains("id=\"about-open-help\"", markup);
        Assert.Contains("id=\"view-help\"", markup);
        Assert.Contains("How do the Cougar MFD displays work?", markup);
        Assert.Contains("Does screenshot reading capture my screen?", markup);
        Assert.Contains("Why does cash on hand say “about”?", markup);
        Assert.Contains("Settings → Report a problem", markup);
    }

    /// <summary>
    /// The questions people actually arrive with: the wipe banner, marking a
    /// point, what a friend's file does, what a backup holds. Each answer names
    /// the page it is about, so a reader can go there rather than look for it.
    /// </summary>
    [Fact]
    public void Help_covers_points_sharing_backups_and_the_wipe_banner()
    {
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

        foreach (var question in new[]
        {
            "What is the orange banner about a patch and a wipe?",
            "How do I mark a point of interest?",
            "Why does a point say “believed to be”?",
            "How far away is a point?",
            "Which screens can it read?",
            "A reading is wrong. What do I do with it?",
            "What can I send to a friend?",
            "What happens to a file someone sends me?",
            "What is in a backup, and what is not?",
            "Can I see it on a tablet or a second screen?",
        })
        {
            Assert.Contains(question, markup);
        }

        Assert.Contains("href=\"#points\"", markup);
        Assert.Contains("href=\"#log\"", markup);
        Assert.Contains("href=\"#imports\"", markup);
        Assert.Contains("id=\"help-search\"", markup);

        // The one thing the app has never measured is not claimed.
        Assert.DoesNotContain("kiosk or mobiGlas", markup);
    }

    /// <summary>
    /// A word narrows the page to the questions that carry it - in the answer
    /// as well as the question - opens them, folds away empty sections, and
    /// says when nothing matches rather than showing a blank page.
    /// </summary>
    [Fact]
    public void The_filter_narrows_to_matching_questions_and_says_when_none_do()
    {
        var page = new Page();

        // The stub does not parse index.html, so the page is built by hand: two
        // sections, three questions, the shape the markup has.
        page.Do("""
            const root = __dom.node('#view-help');
            const item = (q, a) => {
              const d = document.createElement('details'); d.className = 'faq-item';
              const s = document.createElement('summary'); s.textContent = q;
              const p = document.createElement('p'); p.textContent = a;
              d.append(s, p); return d;
            };
            const section = (...items) => {
              const s = document.createElement('section'); s.className = 'help-section';
              s.append(...items); return s;
            };
            root.append(
              section(item('What is the orange banner?', 'It asks whether the patch wiped.'),
                      item('Why are my totals lower?', 'Sessions before the wipe are not counted.')),
              section(item('How do I mark a point of interest?', 'Type /showlocation in chat.')));
            """);

        Assert.Equal(2, Convert.ToInt32(page.Eval("filterHelp('wipe')")));
        Assert.False(page.Truth("__dom.node('#view-help').byClass('faq-item')[0].hidden"));
        Assert.True(page.Truth("__dom.node('#view-help').byClass('faq-item')[0].open"));
        Assert.False(page.Truth("__dom.node('#view-help').byClass('faq-item')[1].hidden"));
        Assert.True(page.Truth("__dom.node('#view-help').byClass('faq-item')[2].hidden"));
        Assert.True(page.Truth("__dom.node('#view-help').byClass('help-section')[1].hidden"));
        Assert.Equal("2 of 3", page.NodeText("#help-count"));
        Assert.True(page.Truth("__dom.node('#help-none').hidden"));

        Assert.Equal(0, Convert.ToInt32(page.Eval("filterHelp('quantanium')")));
        Assert.False(page.Truth("__dom.node('#help-none').hidden"));

        // Cleared, everything is back and the count says nothing.
        Assert.Equal(3, Convert.ToInt32(page.Eval("filterHelp('')")));
        Assert.False(page.Truth("__dom.node('#view-help').byClass('help-section')[1].hidden"));
        Assert.Equal("", page.NodeText("#help-count"));

        page.Do("expandHelp(true);");
        Assert.True(page.Truth("__dom.node('#view-help').byClass('faq-item')[2].open"));
        Assert.Equal("Collapse all", page.NodeText("#help-expand"));
    }
}
