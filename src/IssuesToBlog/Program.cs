using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace IssuesToBlog
{
    class Program
    {
        static readonly string[] Labels = IssueModel.InterestingLabels;

        [STAThread]
        public static async Task Main(string[] args)
        {
            var token = Environment.GetEnvironmentVariable("PERSONAL_ACCESS_TOKEN");
            token = token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (token is null)
            {
                Console.WriteLine("ERROR: Environment variable PERSONAL_ACCESS_TOKEN or GITHUB_TOKEN must contain a token");
                return;
            }

            var productHeader = new ProductHeaderValue("IssuesToBlog", "0.1");
            var connection = new Connection(productHeader, token);
            var (owner, repositoryName, workingDirectory) = GetLocalRepository();
            var issues = (await GetIssues(connection, owner, repositoryName))
                .OrderBy(x => x.Number)
                .ToList();

            foreach (var issue in issues)
            {
                var date = issue.CreatedAt.Value;
                var year = date.Year.ToString();
                var fileName = $"{date.Year}-{date.Month}-{date.Day}-issue-{issue.Number}.markdown";
                var dir = Path.Combine(workingDirectory, "_posts", year);
                var path = Path.Combine(dir, fileName);
                Directory.CreateDirectory(dir);

                var (header, body) = SplitHeaderAndBody(issue.Body);
                var contents =
$@"---
title: ""{issue.Title}""
date: {issue.CreatedAt.Value.ToString("u")}
tags: [{string.Join(',', issue.Labels)}]
new_comment_url: {issue.Url}#new_comment_field
{header}
---

{body}";
                File.WriteAllText(path, contents);

                foreach(var comment in issue.Comments)
                {
                    var commentDir = Path.Combine(workingDirectory, "_data", "comments", $"issue-{issue.Number}");
                    var commentPath = Path.Combine(commentDir, $"{comment.Id}.yml");
                    Directory.CreateDirectory(commentDir);
                    var commentContents =
$@"id: {comment.Id}
date: {comment.PublishedAt?.ToString("u")}
name: {comment.AuthorLogin}
avatar: {comment.AvatarUrl}
message: |";
                    foreach(var line in comment.BodyHTML.Split('\n'))
                    {
                        commentContents += $"\n    {line}";
                    }
                    File.WriteAllText(commentPath, commentContents);
                }
            }

            if (args.Length > 0 && args[0] == "push")
            {
                var viewer = await GetViewerInfo(connection);
                var name = Environment.GetEnvironmentVariable("VIEWER_NAME") ?? viewer.name;
                var email = Environment.GetEnvironmentVariable("VIEWER_EMAIL") ?? viewer.email;
                PushChangedFiles(new[] { "_posts", "_data" }, "update", token, name, email);
            }
        }

        static (string header, string body) SplitHeaderAndBody(string text)
        {
            var separator = "```";
            var startIndex = text.IndexOf(separator);
            if (startIndex == -1)
            {
                return ("", text);
            }

            var endIndex = text.IndexOf(separator, startIndex + separator.Length);
            if (endIndex == -1)
            {
                return ("", text);
            }

            return
            (
                text.Substring(startIndex + separator.Length, endIndex - startIndex - separator.Length).Trim(),
                text.Substring(endIndex + separator.Length).Trim()
            );
        }

        static void RunProcess(string command, string args, string workingDirectory)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                Arguments = args,
                FileName = command,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });
            Console.WriteLine(process.StandardError.ReadToEnd());
            Console.WriteLine(process.StandardOutput.ReadToEnd());
            process.WaitForExit();
        }

        static async Task<IList<IssueModel>> GetIssues(Connection connection, string owner, string name)
        {
            var order = new IssueOrder
            {
                Field = IssueOrderField.CreatedAt,
                Direction = OrderDirection.Asc,
            };

            var query = new Query()
                .Repository(owner: owner, name: name)
                .Issues(first: 30, labels: Labels, states: new[] { IssueState.Open })
                .Nodes
                .Select(y => new IssueModel
                {
                    Url = y.Url,
                    Number = y.Number,
                    Title = y.Title,
                    Body = y.Body,
                    CreatedAt = y.CreatedAt,
                    Labels = y.Labels(30, null, null, null).Nodes.Select(z => z.Name).ToList(),
                    Comments = y.Comments(100, null, null, null).Nodes.Select(c => new CommentModel
                    {
                        BodyHTML = c.BodyHTML,
                        Id = c.Id.Value,
                        PublishedAt = c.PublishedAt,
                        AuthorLogin = c.Author.Login,
                        AvatarUrl = c.Author.AvatarUrl(160)
                    }).ToList()
                });

            return (await connection.Run(query))
                .ToList();
        }

        static async Task<(string name, string email)> GetViewerInfo(Connection connection)
        {
            var query = new Query()
                .Viewer
                .Select(v => new { v.Name, v.Email });

            var info = await connection.Run(query);
            return (info.Name, info.Email);
        }

        static (string owner, string repositoryName, string workingDirectory) GetLocalRepository()
        {
            var dir = LibGit2Sharp.Repository.Discover(".");
            using (var repo = new LibGit2Sharp.Repository(dir))
            {
                var uri = new Uri(repo.Network.Remotes["origin"].Url);
                var path = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
                var postfix = ".git";
                path = TrimPostfix(path, postfix);
                var split = path.Split('/');
                return (split[0], split[1], repo.Info.WorkingDirectory);
            }
        }

        static void PushChangedFiles(IEnumerable<string> paths, string message, string token, string name, string email)
        {
            var dir = LibGit2Sharp.Repository.Discover(".");
            using (var repo = new LibGit2Sharp.Repository(dir))
            {
                LibGit2Sharp.Commands.Stage(repo, paths);
                var author = new LibGit2Sharp.Signature(name, email, DateTimeOffset.Now);
                repo.Commit(message, author, author, new LibGit2Sharp.CommitOptions { });
                var remote = repo.Network.Remotes["origin"];
                var options = new LibGit2Sharp.PushOptions
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                        new LibGit2Sharp.UsernamePasswordCredentials { Username = "token", Password = token }
                };

                repo.Network.Push(remote, @"refs/heads/master", options);
            }
        }

        static string TrimPostfix(string text, string postfix)
        {
            return text.EndsWith(postfix, StringComparison.OrdinalIgnoreCase) ?
                text.Substring(0, text.Length - postfix.Length) : text;
        }
    }
}
