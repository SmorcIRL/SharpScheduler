using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace SmorcIRL.SharpScheduler.Handlers
{
    /// <summary>
    ///     Type must implement this interface to be able to handle commands
    /// </summary>
    public interface IHandler
    {
        /// <summary>
        ///     Invoke this event to signal that handler has done its job
        /// </summary>
        event Action<string> RequestDispose;
        /// <summary>
        ///     Invoke this event to log something
        /// </summary>
        event Action<string> Log;

        /// <summary>
        ///     Use this method to initialize your application, before starting handling
        ///     <param name="args">Init args</param>
        /// </summary>
        Task Init(string[] args);
        /// <summary>
        ///     Use this method to clean up after getting stop request
        ///     <param name="args">Dispose args</param>
        /// </summary>
        Task Dispose(string[] args);

        /// <summary>
        ///     Handle any command, that was not handled by [Handles] methods
        ///     <param name="command">Command to handle</param>
        ///     <param name="args">Command args</param>
        ///     <param name="token">Token, that will be canceled right before Dispose() call</param>
        /// </summary>
        Task<string> Handle([NotNull] string command, [NotNull] string[] args, CancellationToken token)
        {
            throw new ImpossibleToHandleException(command);
        }
    }
}