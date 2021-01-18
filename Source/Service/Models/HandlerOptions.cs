using SharpScheduler.Common;

namespace Service.Models
{
    public class HandlerOptions
    {
        public string Path { get; set; }
        public string Log { get; set; }
        public string Schedule { get; set; }

        public override string ToString()
        {
            string str = $"{nameof(WrapperProcessArgs.HandlerPath)}=\"{Path}\"";

            if (!string.IsNullOrEmpty(Log))
            {
                str += $" {nameof(WrapperProcessArgs.Log)}=\"{Log}\"";
            }

            if (!string.IsNullOrEmpty(Schedule))
            {
                str += $" {nameof(WrapperProcessArgs.Schedule)}=\"{Schedule}\"";
            }

            return str;
        }
    }
}