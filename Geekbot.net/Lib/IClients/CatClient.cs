﻿using RestSharp;

namespace Geekbot.net.Lib.IClients
{
    public interface ICatClient
    {
        IRestClient Client { get; set; }
    }

    public class CatClient : ICatClient
    {
        public CatClient()
        {
            Client = new RestClient("http://random.cat");
        }

        public IRestClient Client { get; set; }
    }
}