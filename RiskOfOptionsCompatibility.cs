using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace OopsAllGup
{
    class RiskOfOptionsCompatibility
    {
        private static bool? _enabled;

        public static bool Enabled
        {
            get
            {
                if (_enabled == null)
                {
                    _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");
                }
                return (bool)_enabled;
            }
        }

        public static void InstallRiskOfOptions()
        {
            static void AddOption(BaseOption option)
            {
                ModSettingsManager.AddOption(option, OopsAllGup.GUID, OopsAllGup.ModName);
            }

            AddOption(new CheckBoxOption(OopsAllGup.ModEnabled));
            AddOption(new CheckBoxOption(OopsAllGup.KinForcesGup));
            AddOption(new IntSliderOption(OopsAllGup.Lives, new IntSliderConfig() { min = 3, max = 50 }));
            AddOption(new IntSliderOption(OopsAllGup.SplitCount, new IntSliderConfig() { min = 1, max = 50 }));

            ModSettingsManager.SetModDescription("Control how Gups split during a run.", OopsAllGup.GUID, OopsAllGup.ModName);
        }
    }
}
