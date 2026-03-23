using System.Configuration;

namespace TSCloud.Desktop.Properties
{
    public sealed partial class Settings : ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default
        {
            get
            {
                return defaultInstance;
            }
        }

        [UserScopedSetting()]
        [DefaultSettingValue("Light")]
        public string Theme
        {
            get
            {
                return ((string)(this["Theme"]));
            }
            set
            {
                this["Theme"] = value;
            }
        }
    }
}