﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppFramework
{
    public class ConsoleApp
    {
        // Keep this reference as ConsoleApOptions.CommandDescriptors.
        readonly CommandDescriptorCollection commands;

        public IHost Host { get; }
        public ILogger<ConsoleApp> Logger { get; }

        internal ConsoleApp(IHost host)
        {
            this.Host = host;
            this.Logger = host.Services.GetRequiredService<ILogger<ConsoleApp>>();
            this.commands = host.Services.GetRequiredService<ConsoleAppOptions>().CommandDescriptors;
        }

        // Statics

        public static ConsoleApp Create(string[] args)
        {
            return CreateBuilder(args).Build();
        }

        public static ConsoleApp Create(string[] args, Action<ConsoleAppOptions> configureOptions)
        {
            return CreateBuilder(args, configureOptions).Build();
        }

        public static ConsoleApp Create(string[] args, Action<HostBuilderContext, ConsoleAppOptions> configureOptions)
        {
            return CreateBuilder(args, configureOptions).Build();
        }

        public static ConsoleAppBuilder CreateBuilder(string[] args)
        {
            return new ConsoleAppBuilder(args);
        }

        public static ConsoleAppBuilder CreateBuilder(string[] args, Action<ConsoleAppOptions> configureOptions)
        {
            return new ConsoleAppBuilder(args, configureOptions);
        }

        public static ConsoleAppBuilder CreateBuilder(string[] args, Action<HostBuilderContext, ConsoleAppOptions> configureOptions)
        {
            return new ConsoleAppBuilder(args, configureOptions);
        }

        public static void Run(string[] args, Delegate defaultCommand)
        {
            Create(args).Run(defaultCommand);
        }

        public static Task RunAsync(string[] args, Delegate defaultCommand)
        {
            return Create(args).AddDefaultCommand(defaultCommand).RunAsync();
        }

        public static void Run<T>(string[] args)
            where T : ConsoleAppBase
        {
            Create(args).AddCommands<T>().Run();
        }

        // TODO/ RUn routed???

        // Add Command

        public ConsoleApp AddDefaultCommand(Delegate command)
        {
            var attr = command.Method.GetCustomAttribute<CommandAttribute>();
            commands.AddDefaultCommand(new CommandDescriptor(CommandType.DefaultCommand, command.Method, command.Target, attr));
            return this;
        }

        public ConsoleApp AddCommand(string commandName, Delegate command)
        {
            var attr = new CommandAttribute(commandName);
            commands.AddCommand(new CommandDescriptor(CommandType.Command, command.Method, command.Target, attr));
            return this;
        }

        public ConsoleApp AddCommand(string commandName, string description, Delegate command)
        {
            var attr = new CommandAttribute(commandName, description);
            commands.AddCommand(new CommandDescriptor(CommandType.Command, command.Method, command.Target, attr));
            return this;
        }

        public ConsoleApp AddCommands<T>()
            where T : ConsoleAppBase
        {
            var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.Name == "Dispose" || method.Name == "DisposeAsync") continue; // ignore IDisposable

                if (method.GetCustomAttribute<DefaultCommandAttribute>() != null)
                {
                    var command = new CommandDescriptor(CommandType.DefaultCommand, method);
                    commands.AddDefaultCommand(command);
                }
                else
                {
                    var command = new CommandDescriptor(CommandType.Command, method);
                    commands.AddCommand(command);
                }
            }
            return this;
        }

        // TODO:AddSubCommand
        // TODO:AddSubCommands<T>()

        public ConsoleApp AddRoutedCommands()
        {
            return AddRoutedCommands(AppDomain.CurrentDomain.GetAssemblies());
        }

        public ConsoleApp AddRoutedCommands(params Assembly[] searchAssemblies)
        {
            foreach (var type in GetConsoleAppTypes(searchAssemblies))
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    if (method.Name == "Dispose" || method.Name == "DisposeAsync") continue; // ignore IDisposable

                    // TODO:type-name get from CommandAttribute
                    var rootName = type.Name;
                    commands.AddSubCommand(rootName, new CommandDescriptor(CommandType.SubCommand, method, rootCommand: rootName));
                }
            }
            return this;
        }

        // Run

        public void Run()
        {
            RunAsync().GetAwaiter().GetResult();
        }

        public void Run(Delegate defaultCommand)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        // Don't use return RunAsync to keep stacktrace.
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            commands.TryAddDefaultHelpMethod();
            commands.TryAddDefaultVersionMethod();

            await Host.RunAsync(cancellationToken);
        }

        static List<Type> GetConsoleAppTypes(Assembly[] searchAssemblies)
        {
            List<Type> consoleAppBaseTypes = new List<Type>();

            foreach (var asm in searchAssemblies)
            {
                if (asm.FullName!.StartsWith("System") || asm.FullName.StartsWith("Microsoft.Extensions") || asm.GetName().Name == "ConsoleAppFramework") continue;

                Type?[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types ?? Array.Empty<Type>();
                }

                foreach (var item in types.Where(x => x != null))
                {
                    if (typeof(ConsoleAppBase).IsAssignableFrom(item) && item != typeof(ConsoleAppBase))
                    {
                        consoleAppBaseTypes.Add(item!);
                    }
                }
            }

            return consoleAppBaseTypes;
        }
    }
}