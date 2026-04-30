namespace TodoBackend.Models
{
    public enum TaskStatus
    {
        Todo,
        InProgress,
        Done
    }

    public class Todo
    {
        public string Id { get; set; } = string.Empty;
        public required string Title { get; set; } = string.Empty;
        public string BoardId { get; set; } = "family-home";
        public string AssigneeName { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Todo;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool Completed { get; set; }
    }
}