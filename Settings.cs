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

        private const string CalibrationLeftKey = "left_key";
        private static readonly double CalibrationLeftDefault = 0;

        private const string CalibreationTopKey = "top_key";
        private static readonly double CalibreationTopDefault = 0;

        private const string CalibrationWidthKey = "width_key";
        private static readonly double CalibrationWidthDefault = 320;

        private const string CalibrationHeightKey = "height_key";
        private static readonly double CalibrationHeightDefault = 180;


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

        public static double CalibrationLeft
        {
            get { return AppSettings.GetValueOrDefault(CalibrationLeftKey, CalibrationLeftDefault); }
            set { AppSettings.AddOrUpdateValue(CalibrationLeftKey, value); }
        }

        public static double CalibreationTop
        {
            get { return AppSettings.GetValueOrDefault(CalibreationTopKey, CalibreationTopDefault); }
            set { AppSettings.AddOrUpdateValue(CalibreationTopKey, value); }
        }

        public static double CalibrationWidth
        {
            get { return AppSettings.GetValueOrDefault(CalibrationWidthKey, CalibrationWidthDefault); }
            set { AppSettings.AddOrUpdateValue(CalibrationWidthKey, value); }
        }

        public static double CalibrationHeight
        {
            get { return AppSettings.GetValueOrDefault(CalibrationHeightKey, CalibrationHeightDefault); }
            set { AppSettings.AddOrUpdateValue(CalibrationHeightKey, value); }
        }

    }
}
