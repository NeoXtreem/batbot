namespace BatBot.Server.Models
{
    public class BatBotOptions
    {
        public const string Mainnet = "main";

        public string Blockchain { get; set; }

        public string Network { get; set; }

        public string BlockchainEndpointWssUrl { get; set; }

        public string BlockchainEndpointHttpUrl { get; set; }

        public string UniswapSubgraphUrl { get; set; }

        public string ContractAddress { get; set; }

        public string PrepWalletAddress { get; set; }

        public string PrepPrivateKey { get; set; }

        public string FrontRunWalletAddress { get; set; }

        public string FrontRunPrivateKey { get; set; }

        public string EtherscanBaseUrl { get; set; }

        public string CoinGeckoBaseUrl { get; set; }

        public string BlocknativeApiWssUrl { get; set; }

        public string BlocknativeApiHttpUrl { get; set; }

        public string BlocknativeApiVersion { get; set; }

        public string BlocknativeApiKey { get; set; }

        public string BlocknativeUsername { get; set; }

        public string BlocknativePassword { get; set; }
    }
}
