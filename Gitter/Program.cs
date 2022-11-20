using LibGit2Sharp;
using Spectre.Console;
using System.Collections;
using System.Globalization;

public static class Program
{
    public static void Main(string[] args)
    {
        var path = args[0];
        using (var repo = new Repository(path))
        {
            Sync(repo);

            WriteLogo();
            AnsiConsole.Status().Start("Loading...", ctx =>
            {
                //TODO Fetch upstream here
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                //Thread.Sleep(2000);
            });

            Timeline(repo, path);
        }
    }

    public static void Sync(Repository repo) {
        //DEAR GOD DO NOT RUN THIS ON A NORMAL GIT REPO, YOU WILL LOOSE ALL YOUR LOCAL CHANGES!!!
        var upstream = repo.Network.Remotes["origin"]!;

        if (repo.Network.Remotes["upstream"] != null)
        {
            upstream = repo.Network.Remotes["upstream"]!;
        }

        var refSpecs = upstream.FetchRefSpecs.Select(x => x.Specification);
        Commands.Fetch(repo, upstream.Name, refSpecs, null, null);
        var main = repo.Branches[upstream.Name + "/main"];
        repo.Reset(ResetMode.Hard, main.Tip);

    }

    public static void Timeline(Repository repo, string path)
    {
        while (true) {
            AnsiConsole.Clear();
            var commits = repo.Commits.Take(50).ToList();
            commits.Add(null);

            WriteLogo();

            var selected_commit = AnsiConsole.Prompt(new SelectionPrompt<Commit>()
            .PageSize(10)
            .MoreChoicesText("[grey](Select messages with arrow keys)[/]")
            .AddChoices(commits)
            .UseConverter((c) => {
                if (c == null)
                {
                    return "Exit";
                }

                string val = string.Format(
                    "[yellow]{0}: ({1})[/] {2}",
                    c.Author.Name,
                    c.Author.When.ToString("yyyy-mm-dd HH:MM", CultureInfo.InvariantCulture),
                    c.MessageShort
                    );

                if (c.Message.Length != c.MessageShort.Length + 1)
                {
                    val += " [cyan]...[/]";
                }

                return val;
            }));

            if (selected_commit == null)
            {
                AnsiConsole.Clear();
                return;
            }

            WriteCommit(selected_commit, path);
        }
    }

    public static void WriteLogo()
    {
        var s = @"[blue on black]
         _______    ________  _________  _________  ______   ______       
        /______/\  /_______/\/________/\/________/\/_____/\ /_____/\      
        \::::__\/__\__.::._\/\__.::.__\/\__.::.__\/\::::_\/_\:::_ \ \     
         \:\ /____/\  \::\ \    \::\ \     \::\ \   \:\/___/\\:(_) ) )_   
          \:\\_  _\/  _\::\ \__  \::\ \     \::\ \   \::___\/_\: __ `\ \  
           \:\_\ \ \ /__\::\__/\  \::\ \     \::\ \   \:\____/\\ \ `\ \ \ 
            \_____\/ \________\/   \__\/      \__\/    \_____\/ \_\/ \_\/ 
        [/]                                                                  
        ";
        AnsiConsole.MarkupLine(s);
    }

    public static void WriteCommit(Commit c, string path)
    {
        AnsiConsole.Clear();

        WriteLogo();

        AnsiConsole.Markup(string.Format(
            "[yellow]{0} <{1}>\n[/][blue]{2} {3}[/]\n[silver]{4}[/]\n\n",
            c.Author.Name,
            c.Author.Email,
            c.Author.When.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture),
            c.Author.When.ToString("HH:MM:ss", CultureInfo.InvariantCulture),
            c.Id
        ));

        var lines = c.Message.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length > 1)
        {
            AnsiConsole.WriteLine(lines[0]);
            AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("grey")));
        }
        else { 
            AnsiConsole.MarkupLine(string.Format("[underline]{0}[/]", lines[0]));
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("#"))
            {
                AnsiConsole.MarkupLine(string.Format("[aqua]{0}[/]", lines[i]));
                continue;
            }
            if (lines[i].StartsWith("!")) {
                var img = new CanvasImage(Path.Combine(path, "assets", lines[i][1..]).Trim());
                img.MaxWidth(32);
                AnsiConsole.Write(img);
                continue;
            }
            AnsiConsole.WriteLine(lines[i]);
        }

        while (true) {
            var key = Console.ReadKey();

            if (key.KeyChar != '\0' || key.Key == ConsoleKey.Backspace) {
                AnsiConsole.Clear();
                return;
            }
        }
    }
}