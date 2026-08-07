using System;

namespace AIHelper.Models
{
    /// <summary>
    /// Represents an AI platform configuration
    /// </summary>
    public class AiPlatform
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Url { get; set; }
        public bool IsActive { get; set; }
    }
}
