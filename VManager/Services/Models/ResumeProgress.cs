// Services/Utils/ResumeProgress.cs

using System.Collections.Generic;

namespace VManager.Services.Models
{
    public class ResumeProgress
    {
        public List<int> CompletedChunks { get; set; } = new();
        public double TotalDurationProcessed { get; set; } = 0;
        public string Operation { get; set; } = "";
    }
}