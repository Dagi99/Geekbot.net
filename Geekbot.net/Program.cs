﻿using System;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Geekbot.net.Lib;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Geekbot.net
{
    internal class Program
    {
        private DiscordSocketClient client;
        private CommandService commands;
        private IDatabase redis;
        private IServiceCollection services;
        private IServiceProvider servicesProvider;
        private RedisValue token;

        private static void Main()
        {
            Console.WriteLine(@"  ____ _____ _____ _  ______   ___ _____");
            Console.WriteLine(@" / ___| ____| ____| |/ / __ ) / _ \\_  _|");
            Console.WriteLine(@"| |  _|  _| |  _| | ' /|  _ \| | | || |");
            Console.WriteLine(@"| |_| | |___| |___| . \| |_) | |_| || |");
            Console.WriteLine(@" \____|_____|_____|_|\_\____/ \___/ |_|");
            Console.WriteLine("=========================================");
            Console.WriteLine("* Starting...");

            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            Console.WriteLine("* Initing Stuff");
            
            var ping = new Ping().Send("8.8.8.8");
            if(ping.Status != IPStatus.Success)
            {
                Console.WriteLine("It seems that you are offline");
                Console.WriteLine("Please connect to the Internet");
                Environment.Exit(101);
            }
            
            client = new DiscordSocketClient();
            commands = new CommandService();

            try
            {
                var redisMultiplexer = ConnectionMultiplexer.Connect("127.0.0.1:6379");
                redis = redisMultiplexer.GetDatabase(6);
                Console.WriteLine("-- Connected to Redis (db6)");
            }
            catch (Exception)
            {
                Console.WriteLine("Start Redis pls...");
                Environment.Exit(102);
            }

            token = redis.StringGet("discordToken");
            if (token.IsNullOrEmpty)
            {
                Console.Write("Your bot Token: ");
                var newToken = Console.ReadLine();
                redis.StringSet("discordToken", newToken);
                token = newToken;

                Console.Write("Bot Owner User ID: ");
                var ownerId = Console.ReadLine();
                redis.StringSet("botOwner", ownerId);
            }

            services = new ServiceCollection();
            var RandomClient = new Random();
            var fortunes = new FortunesProvider(RandomClient);
            var checkEmImages = new CheckEmImageProvider(RandomClient);
            var pandaImages = new PandaProvider(RandomClient);
            services.AddSingleton(redis);
            services.AddSingleton(RandomClient);
            services.AddSingleton<IFortunesProvider>(fortunes);
            services.AddSingleton<ICheckEmImageProvider>(checkEmImages);
            services.AddSingleton<IPandaProvider>(pandaImages);

            Console.WriteLine("* Connecting to Discord");

            await Login();

            await Task.Delay(-1);
        }

        public async Task Login()
        {
            try
            {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                var isConneted = await isConnected();
                if (isConneted)
                {
                    await client.SetGameAsync("Ping Pong");
                    Console.WriteLine($"* Now Connected to {client.Guilds.Count} Servers");

                    Console.WriteLine("* Registering Stuff");

                    client.MessageReceived += HandleCommand;
                    client.MessageReceived += HandleMessageReceived;
                    client.UserJoined += HandleUserJoined;
                    await commands.AddModulesAsync(Assembly.GetEntryAssembly());
                    services.AddSingleton(commands);
                    servicesProvider = services.BuildServiceProvider();

                    Console.WriteLine("* Done and ready for use\n");
                }
            }
            catch (AggregateException)
            {
                Console.WriteLine("Could not connect to discord...");
                Environment.Exit(103);
            }
        }

        public async Task<bool> isConnected()
        {
            while (!client.ConnectionState.Equals(ConnectionState.Connected))
                await Task.Delay(25);
            return true;
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            if (message.Author.IsBot) return;
            var argPos = 0;
            var lowCaseMsg = message.ToString().ToLower();
            if (lowCaseMsg.StartsWith("ping"))
            {
                await message.Channel.SendMessageAsync("pong");
                return;
            }
            if (lowCaseMsg.StartsWith("hui"))
            {
                await message.Channel.SendMessageAsync("hui!!!");
                return;
            }
            if (!(message.HasCharPrefix('!', ref argPos) ||
                  message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            var context = new CommandContext(client, message);
            var commandExec = commands.ExecuteAsync(context, argPos, servicesProvider);
        }

        public async Task HandleMessageReceived(SocketMessage messsageParam)
        {
            var message = messsageParam;
            if (message == null) return;
            
            var statsRecorder = new StatsRecorder(message, redis);
            var userRec = statsRecorder.UpdateUserRecordAsync();
            var guildRec = statsRecorder.UpdateGuildRecordAsync();
            
            if (message.Author.Id == client.CurrentUser.Id) return;
            var channel = (SocketGuildChannel) message.Channel;
            Console.WriteLine(channel.Guild.Name + " - " + message.Channel + " - " + message.Author.Username + " - " +
                              message.Content);
            await userRec;
            await guildRec;
        }

        public async Task HandleUserJoined(SocketGuildUser user)
        {
            if (!user.IsBot)
            {
                var message = redis.StringGet(user.Guild.Id + "-welcomeMsg");
                if (!message.IsNullOrEmpty)
                {
                    message = message.ToString().Replace("$user", user.Mention);
                    await user.Guild.DefaultChannel.SendMessageAsync(message);
                }
            }
        }
    }
}