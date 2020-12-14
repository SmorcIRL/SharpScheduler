using System;

namespace SmorcIRL.SharpScheduler.Handlers
{
    /// <summary>
    ///     Attribute needed to mark your method as handling
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HandlesAttribute : Attribute
    {
        /// <summary>
        ///     <param name="command">Command to handle</param>
        /// </summary>
        public HandlesAttribute(string command)
        {
            Command = command;
        }

        public string Command { get; }
    }

    /// <summary>
    ///     Attribute needed to declare a type, that will handle incoming commands
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class HandlerDeclarationAttribute : Attribute
    {
        /// <summary>
        ///     <param name="handlerType">Type, that implements <c>IHandler</c></param>
        /// </summary>
        public HandlerDeclarationAttribute(Type handlerType)
        {
            HandlerType = handlerType;
        }

        public Type HandlerType { get; }
    }
}