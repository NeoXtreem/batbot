namespace BatBot.Server.Models
{
    public class SettingsOptions
    {
        public double MaximumGasPrice { get; set; }

        public decimal MinimumMarketCap { get; set; }

        public double MinimumTargetSwapEth { get; set; }

        public double MaximumTargetSwapEth { get; set; }

        public double MinimumPairContractAge { get; set; }

        public double MinimumFrontRunEth { get; set; }

        public double MaximumFrontRunEth { get; set; }

        public double SlippageTolerance { get; set; }

        public double TargetSwapAmountOutToleranceMin { get; set; }

        public double TargetSwapAmountOutToleranceMax { get; set; }

        public double FrontRunGasPriceFactor { get; set; }

        public double GasEscalationFactor { get; set; }

        public int GasEscalationRetries { get; set; }

        public bool SellOnFailedTargetSwap { get; set; }

        public bool ShowDiscardedSwapsOutput { get; set; }
    }
}
