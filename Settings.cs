using Plugin.Settings;
using Plugin.Settings.Abstractions;

namespace zecil.AmbiHueTv
{
    public static class Settings
    {
        private static ISettings AppSettings => CrossSettings.Current;

        #region Setting Constants

        private const string AppKey = "app_key";
        private static readonly string AppKeyDefault = string.Empty;

        private const string BridgeIpKey = "bridgeIP_key";
        private static readonly string BridgeIpDefault = string.Empty;

        private const string AnalysisAlgorithmKey = "AnalysisAlgorithm_key";
        private static readonly AnalysisAlgorithm AnalysisAlgorithmDefault = AnalysisAlgorithm.PureAverage;

        private const string BiasAlgorithmKey = "BiasAlgorithm_key";
        private static readonly BiasAlgorithm BiasAlgorithmDefault = BiasAlgorithm.RuleOfThirds;

        #endregion

        public static string DefaultBridgeIp
        {
            get { return AppSettings.GetValueOrDefault(BridgeIpKey, BridgeIpDefault); }
            set { AppSettings.AddOrUpdateValue(BridgeIpKey, value); }
        }


        public static string TheAppKey
        {
            get { return AppSettings.GetValueOrDefault(AppKey, AppKeyDefault); }
            set { AppSettings.AddOrUpdateValue(AppKey, value); }
        }

        public static AnalysisAlgorithm TheAnalysisAlgorithm
        {
            get { return AppSettings.GetValueOrDefault(AnalysisAlgorithmKey, AnalysisAlgorithmDefault); }
            set { AppSettings.AddOrUpdateValue(AnalysisAlgorithmKey, (int)value); }
        }

        public static BiasAlgorithm TheBiasAlgorithm
        {
            get { return AppSettings.GetValueOrDefault(BiasAlgorithmKey, BiasAlgorithmDefault); }
            set { AppSettings.AddOrUpdateValue(BiasAlgorithmKey, (int)value); }
        }

    }
}
