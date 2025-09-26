namespace CarbonTracker.API.Models
{
    public class ResponseItem
    {
        public Guid Id { get; set; }
        public Guid ResponseId { get; set; }
        public string QuestionId { get; set; } = "";
        public string AnswerText { get; set; } = "";
        public decimal AnswerNumeric { get; set; } = 0;
        public string AnswerChoiceId { get; set; } = "";
    }
}
