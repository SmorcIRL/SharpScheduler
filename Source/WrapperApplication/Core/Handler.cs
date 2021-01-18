using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SharpScheduler.Common;
using SmorcIRL.SharpScheduler.Handlers;
using WrapperApplication.Stuff;

namespace WrapperApplication.Core
{
    public partial class WrapperKernel
    {
        private class Handler
        {
            private const BindingFlags SuitableMethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            private readonly CancellationTokenSource _disposalTokenSource;
            private readonly Dictionary<string, MethodWrapper> _methods;

            private IHandler _handler;

            public Handler()
            {
                _disposalTokenSource = new CancellationTokenSource();
                _methods = new Dictionary<string, MethodWrapper>();
            }

            public Task Init(string handlerPath, string[] args)
            {
                var handlerType = GetHandlerType(handlerPath);

                if (handlerType == null)
                {
                    throw new HandlerLoadException("Handler type is unknown");
                }

                _handler = (IHandler) Activator.CreateInstance(handlerType);

                if (_handler == null)
                {
                    throw new HandlerLoadException($"Handler must implement \"{nameof(IHandler)}\" interface");
                }

                var infos = handlerType.GetMethods(SuitableMethodBindingFlags);

                foreach (var info in infos)
                {
                    if (!MethodWrapper.TryCreate(info, _handler, out var methodWrapper))
                    {
                        continue;
                    }

                    if (!_methods.TryAdd(methodWrapper.Command, methodWrapper))
                    {
                        Log.Warning("Duplicate of handling method, command: \"{0}\"", methodWrapper.Command);
                    }
                }

                _handler.Log += LogHandlersMessage;
                _handler.RequestDispose += HandleDisposeRequest;

                return _handler.Init(args);

                static Type GetHandlerType(string path)
                {
                    Assembly asm;

                    try
                    {
                        asm = Assembly.LoadFrom(path);
                    }
                    catch (Exception ex)
                    {
                        throw new HandlerLoadException($"Failed to load handler assembly: {ex.Message}");
                    }

                    var attribute = asm.GetCustomAttribute<HandlerDeclarationAttribute>();

                    if (attribute == null)
                    {
                        throw new HandlerLoadException($"Missing \"{nameof(HandlerDeclarationAttribute)}\" for a handler assembly");
                    }

                    return attribute.HandlerType;
                }
            }
            public async Task<(string, bool)> Handle(string command, string[] args)
            {
                if (_disposalTokenSource.IsCancellationRequested)
                {
                    throw new Exception("Handler is disposing");
                }

                if (!_methods.TryGetValue(command, out var methodWrapper))
                {
                    // Exception by default
                    return (await _handler.Handle(command, args, _disposalTokenSource.Token), true);
                }

                string result = await methodWrapper.Delegate(args, _disposalTokenSource.Token);

                return (result, methodWrapper.ReturnsValue);
            }

            public async Task HandleSchedulerTick((string, string[]) tick)
            {
                (string command, string[] args) = tick;

                try
                {
                    (string message, bool returnsValue) = await Handle(command, args);

                    if (returnsValue)
                    {
                        LogHandlersMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ImpossibleToHandleException)
                    {
                        Log.Error(ex.Message);
                    }
                    else
                    {
                        Log.Error("Exception while handling command \"{0}\" with {1} args: {2}", command, args.Length, ex);
                    }
                }
            }
            public async Task Dispose(string[] args)
            {
                try
                {
                    _disposalTokenSource.Cancel();
                    await _handler.Dispose(args);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception while handler is disposing: {0}", ex);
                }
                finally
                {
                    _handler.Log -= LogHandlersMessage;
                    _handler.RequestDispose -= HandleDisposeRequest;
                }
            }

            private void LogHandlersMessage(string message)
            {
                Log.Information("[Handler]: {0}", message);
            }
            private void HandleDisposeRequest(string message)
            {
                Log.Information("[Handler] Requested disposal. Message: {0}", message);
                _disposalTokenSource.Cancel();
            }

            public Task WaitForDisposeRequest()
            {
                return _disposalTokenSource.Token.WaitForCancelerationAsync();
            }

            private class MethodWrapper
            {
                private static readonly HashSet<Type> ValidSignatures = new()
                {
                    typeof(Func<string[], CancellationToken, Task<string>>),
                    typeof(Func<string[], CancellationToken, Task>),
                    typeof(Func<string[], string>),
                    typeof(Action<string[]>)
                };

                private MethodWrapper()
                {
                }

                public Func<string[], CancellationToken, Task<string>> Delegate { get; private set; }
                public string Command { get; private set; }
                public bool ReturnsValue { get; private set; }

                public static bool TryCreate(MethodInfo info, object @this, out MethodWrapper methodWrapper)
                {
                    methodWrapper = null;

                    var attribute = info.GetCustomAttribute<HandlesAttribute>();

                    if (attribute == null)
                    {
                        return false;
                    }

                    Delegate handler = null;

                    foreach (var _ in ValidSignatures.Where(type => (handler = CreateDelegate(type, info, @this)) != null)
                    )
                    {
                        break;
                    }

                    if (handler == null)
                    {
                        return false;
                    }

                    Func<string[], CancellationToken, Task<string>> func;
                    bool returnsValue;

                    switch (handler)
                    {
                        case Func<string[], CancellationToken, Task<string>> d1:
                        {
                            func = d1;
                            returnsValue = true;
                            break;
                        }
                        case Func<string[], CancellationToken, Task> d2:
                        {
                            func = async (args, token) =>
                            {
                                await d2(args, token);
                                return default;
                            };
                            returnsValue = false;
                            break;
                        }
                        case Func<string[], string> d3:
                        {
                            func = (args, token) => Task.Run(() => d3(args), token);
                            returnsValue = true;
                            break;
                        }
                        case Action<string[]> d4:
                        {
                            func = (args, token) => Task.Run(() =>
                            {
                                d4(args);
                                return default(string);
                            }, token);
                            returnsValue = false;
                            break;
                        }
                        default:
                        {
                            throw new Exception();
                        }
                    }

                    methodWrapper = new MethodWrapper
                    {
                        Command = attribute.Command,
                        Delegate = func,
                        ReturnsValue = returnsValue
                    };

                    return true;

                    static Delegate CreateDelegate(Type delegateType, MethodInfo methodInfo, object instance)
                    {
                        return System.Delegate.CreateDelegate(delegateType, methodInfo.IsStatic ? null : instance,
                            methodInfo, false);
                    }
                }
            }
        }
    }
}