namespace CarbonTracker.API.Models
{
    public class Questionnaire
    {
        public Guid Id { get; set; }
        public Guid CanonicalId { get; set; }
        public int Version { get; set; }
        public string Title { get; set; } = "";
        public string DefinitionJson { get; set; } = "";
        public string Status { get; set; } = "draft";
        public Guid? SupersedesId { get; set; }
        public Guid? ReplacedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
