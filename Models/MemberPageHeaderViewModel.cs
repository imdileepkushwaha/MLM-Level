namespace MLM_Level.Models
{
    public class MemberPageHeaderViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-grid";
        public string ThemeClass { get; set; } = "member-theme-violet";
        public string? Badge { get; set; }
        public string? ActionText { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionIcon { get; set; }
    }
}
