using System;

namespace SmorcIRL.SharpScheduler.Handlers
{
    public class ImpossibleToHandleException : Exception
    {
        public ImpossibleToHandleException(string command) : base($"No handler for the command: \"{command}\"")
        {
        }
    }
}