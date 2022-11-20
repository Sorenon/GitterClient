using Gitter;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Spectre.Console;
using System.Diagnostics;
using System.Globalization;
using System.IO;

public static class Program
{
    public static void Main(string[] args)
    {
        var path = args[0];
        using (var repo = new Repository(path))
        {
            var upstream = Sync(repo);

            WriteLogo();
            AnsiConsole.Status().Start("Loading...", ctx =>
            {
                //TODO Fetch upstream here
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                //Thread.Sleep(2000);
            });

            Timeline(repo, path, upstream);
        }
    }

    public static Remote Sync(Repository repo)
    {
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

        return upstream;
    }

    public static void Timeline(Repository repo, string path, Remote upstream)
    {
        var rows = new List<TimelineRow> { new TimelineRow { commit = null, ty = false } };

        foreach (var c in repo.Commits.Take(50))
        {
            rows.Add(new TimelineRow { commit = c, ty = false });
        }

        rows.Add(new TimelineRow { commit = null, ty = true });

        while (true)
        {
            AnsiConsole.Clear();
            WriteLogo();

            var selected_commit = AnsiConsole.Prompt(new SelectionPrompt<TimelineRow>()
            .PageSize(20)
            .MoreChoicesText("[grey](Select messages with arrow keys)[/]")
            .AddChoices(rows)
            .UseConverter((row) =>
            {
                if (row.commit == null)
                {
                    return row.ty ? "[underline]Exit[/]" : "[underline]Make Branch Ready For PR[/]";
                }

                var c = row.commit;

                string val = string.Format(
                    "[yellow]{0}: ({1})[/] {2}",
                    c.Author.Name,
                    c.Author.When.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    c.MessageShort
                    );

                if (c.Message.Length != c.MessageShort.Length + 1)
                {
                    val += " [cyan]...[/]";
                }

                return val;
            }));

            if (selected_commit.commit == null)
            {
                if (selected_commit.ty)
                {
                    AnsiConsole.Clear();
                    return;
                }
                else
                {
                    RunCreatePost(repo, upstream, path);
                }
            }
            else
            {
                WriteCommit(selected_commit.commit, path);
            }
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
            c.Author.When.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            c.Id
        ));

        var lines = c.Message.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length > 1)
        {
            AnsiConsole.WriteLine(lines[0]);
            AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("grey")));
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine(string.Format("[underline]{0}[/]", lines[0]));
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("#"))
            {
                AnsiConsole.MarkupLine(string.Format("[aqua]{0}[/]", lines[i]));
                continue;
            }
            if (lines[i].StartsWith("!"))
            {
                var img = new CanvasImage(Path.Combine(path, "assets", lines[i][1..]).Trim());
                img.MaxWidth(48);
                AnsiConsole.Write(img);
                continue;
            }
            AnsiConsole.WriteLine(lines[i]);
        }

        while (true)
        {
            var key = Console.ReadKey();

            if (key.KeyChar != '\0' || key.Key == ConsoleKey.Backspace)
            {
                AnsiConsole.Clear();
                return;
            }
        }
    }

    public static void RunCreatePost(Repository repo, Remote upstream, string path)
    {
        AnsiConsole.Clear();

        WriteLogo();

        var client = Github.CreateClient();

        var user = Task.Run(() => { return client.User.Current(); }).Result!;

        AnsiConsole.WriteLine("Making active branch dirty...");

        //var title = Console.ReadLine();

        //AnsiConsole.WriteLine("Write your post body (create a blank line with `:q` to end) (this is also a good time to add any assets to the repo)");

        //var lines = new List<string>();

        //while (true)
        //{
        //    var line = Console.ReadLine();

        //    if (line == ":q" || line == null)
        //    {
        //        break;
        //    }
        //    else
        //    {
        //        lines.Add(line);
        //    }
        //}

        //var body = string.Join("\n", lines);

        File.WriteAllText(Path.Combine(path, user.Id.ToString()), Guid.NewGuid().ToString());

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "add .",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = path
            }
        };

        process.Start();
        process.WaitForExit();

        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "commit -m \"###\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = path
            }
        };

        process.Start();
        process.WaitForExit();

        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "push",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = path
            }
        };

        process.Start();
        process.WaitForExit();

        //var res = Task.Run(() =>
        //{
        //    return client.PullRequest.Create(
        //        "sorenon",
        //        "gitter-testing",
        //        new Octokit.NewPullRequest("This is a title", "oh-my", "main")
        //    );
        //});

        AnsiConsole.WriteLine("Branch made dirty, hop onto Github to make the pull request!");

        while (true)
        {
            var key = Console.ReadKey();

            if (key.KeyChar != '\0' || key.Key == ConsoleKey.Backspace)
            {
                AnsiConsole.Clear();
                return;
            }
        }
    }
}