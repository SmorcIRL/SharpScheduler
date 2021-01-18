using System;

namespace WrapperApplication.Stuff
{
    public class HandlerLoadException : Exception
    {
        public HandlerLoadException(string message) : base(message)
        {
        }
    }
}