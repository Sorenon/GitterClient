using Octokit;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gitter
{
    internal static class Github
    {
        public static GitHubClient CreateClient() {
            var client = new GitHubClient(new ProductHeaderValue("gitter-client"));
            AuthenticateClient(client);
            return client;
        }

        public static bool GetCredential(GitHubClient client)
        {
            if (!File.Exists("credentials"))
            {
                return false;
            }

            var lines = File.ReadAllLines("credentials");

            if (lines.Length != 1) {
                return false;
            }

            client.Credentials = new Credentials(lines[0]);
            return true;
        }

        public static void AuthenticateClient(GitHubClient client)
        {
            var clientId = Environment.GetEnvironmentVariable("client-id");
            var clientSecret = Environment.GetEnvironmentVariable("client-secret");

            if (GetCredential(client)) {
                return;
            }

            AnsiConsole.MarkupLine("[red]User not yet authenticated[/]");

            var login_request = new OauthLoginRequest(clientId)
            {
                Scopes = { "user", "notifications", "write:repo_hook" },
                State = "state"
            };

            // NOTE: user must be navigated to this URL
            var oauthLoginUrl = client.Oauth.GetGitHubLoginUrl(login_request);
            Console.WriteLine(oauthLoginUrl);
            Console.WriteLine(">>>");

            //TODO PKCE!!!!!!

            var code = Console.ReadLine();

            var token_request = new OauthTokenRequest(clientId, clientSecret, code);
            var token = Task.Run(() => client.Oauth.CreateAccessToken(token_request)).Result;

            client.Credentials = new Credentials(token.AccessToken);

            File.WriteAllText("credentials", token.AccessToken);
        }
    }
}
