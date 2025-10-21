namespace CarbonTracker.API.Models
{
    public class Response
    {
        public Guid Id { get; set; }
        public Guid QuestionnaireId { get; set; }
        public Guid CanonicalId { get; set; }
        public Guid UserId { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string DefinitionHash { get; set; } = "";
        public string AnswersJson { get; set; } = "";

    }
}
