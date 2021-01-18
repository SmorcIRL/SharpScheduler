using System;
using System.Diagnostics.CodeAnalysis;

namespace SmorcIRL.SharpScheduler.Handlers
{
    public class ImpossibleToHandleException : Exception
    {
        public ImpossibleToHandleException([NotNull] string command) : base($"No handler for the command: \"{command}\"")
        {
        }
    }
}