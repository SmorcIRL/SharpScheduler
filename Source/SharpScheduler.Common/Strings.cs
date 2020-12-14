namespace SharpScheduler.Common
{
    public static class Names
    {
        public const string RootCommand = "scs";
        public const string LogFileName = "Log.txt";
        public const string HandlerScheduleFilename = "Schedule.json";
        public const string ServiceConfig = "Service.json";

        public const string DotnetUtilName = "dotnet";
        public const string WrapperAppName = "WrapperApplication.dll";
    }

    public static class Headers
    {
        public const string Command = nameof(Command);
        public const string CommandTarget = nameof(CommandTarget);
        public const string HandlerID = nameof(HandlerID);
    }

    public static class ServiceCommands
    {
        public const string Stop = "stop";
        public const string Info = "info";
        public const string RunHandler = "runh";
    }

    public static class HandlerCommands
    {
        public const string Stop = "stoph";
        public const string Info = "infoh";
        public const string StopTrigger = "stopt";
        public const string ExtendSchedule = "exts";
        public const string Handle = "handle";
        public const string InitInfo = "initinfo";
        public const string GetLog = "getlog";
    }

    public static class LogStrings
    {
        public const string Started = "[========================================[Started]========================================]";
        public const string Finished = "[========================================[Finished]========================================]\n\n";
    }
}