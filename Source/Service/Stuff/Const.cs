using System;

namespace Service.Stuff
{
    public static class Const
    {
        public static readonly TimeSpan WrapperInitTimeout = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan WrappersAutoRemoveTimeout = TimeSpan.FromSeconds(5);
    }
}