namespace TodoBackend.Models
{
    public class Todo
    {
        // Changed from int to string for easy MongoDB and GraphQL integration
        public string Id { get; set; } = string.Empty;
        public required string Title { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public bool IsCompleted { get; set; }
    }
}