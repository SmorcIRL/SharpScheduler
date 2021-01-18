namespace SharpScheduler.Common
{
    public class WrapperProcessArgs
    {
        public int ServicePID { get; set; }
        public int HandlerPort { get; set; }
        public string HandlerPath { get; set; }
        public string Log { get; set; }
        public string Schedule { get; set; } = Names.HandlerScheduleFilename;
    }
}