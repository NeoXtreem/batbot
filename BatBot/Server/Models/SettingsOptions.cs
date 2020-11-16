namespace BatBot.Server.Models
{
    public class SettingsOptions
    {
        public decimal MaximumGasPrice { get; set; }

        public decimal MinimumLiquidity { get; set; }

        public decimal MaximumLiquidity { get; set; }

        public decimal MinimumTargetSwapEth { get; set; }

        public decimal MaximumTargetSwapEth { get; set; }

        public double MinimumPairContractAge { get; set; }

        public decimal MinimumFrontRunEth { get; set; }

        public decimal MaximumFrontRunEth { get; set; }

        public decimal HistoricalAnalysisMinEthOut { get; set; }

        public int HistoricalAnalysisMinFetchThreshold { get; set; }

        public int HistoricalAnalysisMinRelevantSwaps { get; set; }

        public double MinimumUniqueSellRecipientsRatio { get; set; }

        public double SlippageTolerance { get; set; }

        public double TargetSwapAmountOutToleranceMin { get; set; }

        public double TargetSwapAmountOutToleranceMax { get; set; }

        public double FrontRunGasPriceFactor { get; set; }

        public double GasEscalationFactor { get; set; }

        public int GasEscalationRetries { get; set; }

        public bool SellOnFailedTargetSwap { get; set; }

        public bool ShowDiscardedSwapsOutput { get; set; }

        public bool YoloMode { get; set; }
    }
}
