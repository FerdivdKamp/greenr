namespace CarbonTracker.API.Models
{
    public class QuestionnaireResponse
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid QuestionnaireId { get; set; }
        public Guid? UserId { get; set; }

        // store SurveyJS answer object as raw JSON text for now
        public string AnswersJson { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
