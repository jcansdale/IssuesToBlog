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
        public IList<string> Labels { get; internal set; }
        public string Body { get; internal set; }
    }
}