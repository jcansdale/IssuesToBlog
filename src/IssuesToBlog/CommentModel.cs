using System;
using System.Collections.Generic;
using System.Text;
using Octokit.GraphQL.Core;

namespace IssuesToBlog
{
    public class CommentModel
    {
        public string BodyText { get; set; }
        public string Id { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string AuthorLogin { get; set; }
        public string AvatarUrl { get; set; }
    }
}
