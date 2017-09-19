﻿using System.Threading.Tasks;
using Discord.Commands;
using Geekbot.net.Lib;

namespace Geekbot.net.Modules
{
    public class Panda : ModuleBase
    {
        private readonly IPandaProvider pandaImages;
        
        public Panda(IPandaProvider pandaImages)
        {
            this.pandaImages = pandaImages;
        }

        [Command("panda", RunMode = RunMode.Async)]
        [Summary("Get a random panda")]
        public async Task PandaCommand()
        {
            await ReplyAsync(pandaImages.GetRandomPanda());
        }
    }
}