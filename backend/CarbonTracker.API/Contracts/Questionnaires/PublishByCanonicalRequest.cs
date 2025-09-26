namespace CarbonTracker.API.Contracts.Questionnaires
{
    public sealed class PublishByCanonicalRequest
    {
        public Guid? TargetId { get; set; }      // activate this specific version id
        public int? TargetVersion { get; set; }  // or this version number within the family
        public bool Latest { get; set; } = false; // or just take the highest version
    }
}
