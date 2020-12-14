# SharpScheduler

**SharpScheduler** - кроссплатформенный фреймворк, позволяющий быстро настроить работу .NET-приложения в соответствии с
заданным расписанием, что может быть удобно на ранних стадиях разработки.

### Требования

- .NET 5 + CLI

### Проекты

- `Source/`
    - `CLI` - CLI для взаимодействия со службой
    - `Service` - Служба
    - `WrapperApplication` - Обертка пользовательского кода
    - `SharpScheduler.Common` - Общая сборка
    - `SharpScheduler.Handlers` - Необходима для регистрации юзер-кода в рамках фреймворка

### Сборка

```shell
dotnet publish Source/Service/Service.csproj -o Build -c Release
dotnet publish Source/WrapperApplication/WrapperApplication.csproj -o Build -c Release
dotnet publish Source/CLI/CLI.csproj -o Build -c Release
```

### Регистрация службы

Фреймворк позволяет регистрировать сервис в виде `Windows service`/`Linux daemon`.

- Windows

``` bat
> sc create SchedulerService binPath=Build\Service.exe
 ```

- Linux (systemd)

Создаем простой unit и закидываем в `/etc/systemd/system/`:

```
[Unit]
[Service]
Type=simple
ExecStart=dotnet Build/Service.dll

[Install]
WantedBy=multi-user.target
```

```shell
$ systemctl enable SchedulerService; systemctl start SchedulerService
 ```

### Интеграция в проект

Для того чтобы приложение управлялось через сервис, необходимо иметь проект библиотеки и в нем класс, реализующий
интерфейс `IHandler` из проекта `SharpScheduler.Handlers`. Добавляем зависимость:

```shell
$ dotnet add OurLib.csproj reference /Build/SharpScheduler.Handlers.dll
 ```

Теперь о реализации интерфейса. Этот класс является входной точкой в приложение и его задачей является обработка команд
вида
`(string command, string[] args)`.

Для того чтобы класс обрабатывал какую-либо команду можно:

- объявить метод специальной сигнатуры и добавить ему аттрибут `[Handles(commandName)]`
- обработать команду "вручную" в методе `Handle()` интерфейса

Приоритет будет отдаваться методу с аттрибутом, а в `Handle()` можно обрабатывать все "не основные" команды.

Сигнатура метода должна совпадать с одним из следующих делегатов:

```C#
Action<string, string[]>
Func<string, string[], CancellationToken, Task>
Func<string, string[], string>
Func<string, string[], CancellationToken, Task<string>>
```

Если метод возвращает строку, она будет записана в лог. В лог можно так же писать через `Log.Invoke()` интерфейса. В параметр `CancellationToken` передается токен, отменяемый прямо перед вызовом `Dispose()`. 

При старте приложения часто нужно выполнять некоторую работу по инициализации, а по завершению — очистке. Поскольку
класс обработчика является входной точкой в приложения и ему же сигнализируют о необходимости завершения работы — он
берет ответственность за эти задачи на себя, реализуя методы `Init()` и `Dispose()`. Еще одно событие
интерфейса - `DisposeRequest` уведомит о необходимости завершения работы.

Класс обработчика необходимо явно объявить на уровне сборки через аттрибут `HandlerDeclaration`.

Пример:

```C#
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmorcIRL.SharpScheduler.Handlers;

[assembly: HandlerDeclaration(typeof(TestHandler.TestHandler))]

namespace TestHandler
{
    public class TestHandler : IHandler
    {
        public event Action<string> RequestDispose;
        public event Action<string> Log;

        public async Task Init(string[] args)
        {
            // Doing some important stuff
        }
        public async Task Dispose(string[] args)
        {
            // Cleaning up
        }

        [Handles("mult2x2")]
        public void Multiply2X2(string[] _)
        {
            int result = 2 * 2;

            if (result == 5)
            {
                // Reason will be logged
                RequestDispose?.Invoke("Something gone wrong, it's time to stop");
                return;
            }

            Log?.Invoke($"All right: 2 * 2 = {result}");
        }

        [Handles("mult")]
        public async Task<string> MultiplyAnythingOnlineWOW(string[] args, CancellationToken token)
        {
            return await new HttpClient()
                        .GetStringAsync($"http://api.mathjs.org/v4/?expr={args[0]}*{args[1]}", token);
        }

        public async Task<string> Handle(string command, string[] args, CancellationToken token)
        {
            return command switch
            {
                "doSmth" => DoSmth(),
                "doSmthAsync" => await DoSmthAsync(token),
                _ => throw new ImpossibleToHandleException(command)
            };

            string DoSmth()
            {
                return "Done";
            }

            async Task<string> DoSmthAsync(CancellationToken token)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                return "Done";
            }
        }
    }
}
```

### Конфигурация

После того как в библиотеке реализован интерфейс, нужно составить расписание и подключить библиотеку к сервису. При
старте сервис будет анализировать файл конфигурации `Service.json`.  Его формат:

```json5
{
  // Порт по которому будем подключаться из CLI.
  // В случае если порт будет занят, либо не указан - будет выбран свободный
  // Выбранный порт пишется в лог
  "PrefPort": 15000,
  // Путь к утилите dotnet. Можно настроить, если есть проблемы c путями
  "DotnetPath": "dotnet",
  // Путь к обертке. Можно опустить если проекты собраны в одну папку
  "WrapperPath": "WrapperApplication.dll",
  // Массив обработчиков, запускаемых сервисом при старте
  "Handlers": []
}
```

Обработчики представляются в формате

```json5
{
  // Путь к библиотеке обработчика. Конечная директория будет являться рабочей для приложения
  "Path": "Path/To/My/Handler.dll",
  // Относительный путь для локального лога обработчика. По умолчанию - "Log_*PID*.txt" 
  "Log": "Log.txt",
  // Относительный путь для расписания обработчика. По умолчанию - "Schedule.json" 
  "Schedule": "Schedule.json"
}
```

Интерес представляет файл расписания. На каждый обработчик — можно определить свой файл, но они могут ссылаться и на
один, если например нужно несколько экземпляров одного приложения. Его формат:

```json5
{
  // Определяет аргументы, которые передадутся в Init() при старте приложения
  "InitArgs": [],
  // Определяет аргументы, которые передадутся в Dispose() при завершении приложения по умолчанию
  "DisposeArgs": [],
  // Определяет расписание в виде триггеров
  "Triggers": {
    "Simple": [],
    "Dated": [],
    "Weekly": []
  }
}

```

Триггеры, определяющие расписание задаются следующим образом:

- `Simple` - Самые настраиваемые:

```json5
{
  // Во всех триггерах указываются "Command" и "Args"
  "Command": "dosmth",
  "Args": [
    "file.txt",
    "http://site.com"
  ],
  // Задержка после старта, по умолчанию - сразу же
  "Delay": "00:00:01",
  // Интервал тиков, обязательно, по умолчанию - минимален (0.1 с)
  // TODO: Это поведение изменится
  "Interval": "00:00:01",
  // Количество тиков, по умолчанию = 1 (-1 для бесконечных тиков)
  "Ticks": 10
}

```

- `Dated` - Единичный датированный триггер:

```json5
{
  // Во всех триггерах указываются "Command" и "Args"

  // Дата и время, обязательно
  "Date": "08/18/2022 07:22:16",
}

```

- `Weekly` - Еженедельные триггеры:

```json5
{
  // Во всех триггерах указываются "Command" и "Args"

  // День недели, по умолчанию - текущий
  "WeekDay": "Monday",
  // Время дня, по умолчанию - текущее 
  "Time": "14:20:00",
  // Количество тиков = количество недель, по умолчанию - не ограничено (-1)
  "Ticks": 10
}

```

Расписание загружается при старте обработчика, но может быть дополнено/сокращено при помощи CLI.

### CLI

Работа с сервисом идет через командный интерфейс. Для удобства можно задать alias:

- Windows (PowerShell)

``` bat
> Set-Alias -Name scs -Value Build/CLI/scs.exe
```

- Linux

``` shell
$ alias scs="dotnet Build/CLI/scs.dll"
```

Выполнив команду без параметров, можно увидеть справку по командам:

``` shell
$ scs

===========================================================
    CLI for https://github.com/SmorcIRL/SharpScheduler
===========================================================

Usage: scs [command] [options]

Options:
  -?|-h|--help  Show help information.

Commands:
  exts          Extend handler's list of triggers using "schedule"-like file
  getlog        Get copy of the local handler's log
  handle        Handle command and return result without creating a trigger
  info          Get info about active handlers and their triggers
  infoh         Get list of handlers's active triggers
  runh          Start new handler
  stop          Stop service
  stoph         Stop selected handler
  stopt         Stop selected trigger

Run 'scs [command] -?|-h|--help' for more information about a command.

```

### Используемые пакеты

- [Serilog](https://github.com/serilog/serilog)
- [CommandLineUtils](https://github.com/natemcmaster/CommandLineUtils)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [ConsoleTables](https://github.com/khalidabuhakmeh/ConsoleTables)