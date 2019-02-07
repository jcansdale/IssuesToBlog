using System;
using System.Collections.Generic;

namespace IssuesToBlog
{
    internal class IssueModel
    {
        public static readonly string[] InterestingLabels = new[] { "post" };

        public int Number { get; set; }
        public string Title { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public IList<string> Labels { get; set; }
        public string Body { get; set; }
        public IList<CommentModel> Comments { get; set; }
    }
}