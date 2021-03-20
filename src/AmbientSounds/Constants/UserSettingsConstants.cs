﻿using System.Collections.Generic;

namespace AmbientSounds.Constants
{
    /// <summary>
    /// Class of key constants
    /// for user settings.
    /// </summary>
    public class UserSettingsConstants
    {
        /// <summary>
        /// Volume settings key.
        /// </summary>
        public const string Volume = "LastUsedVolume";

        /// <summary>
        /// Telemetry enabled key.
        /// </summary>
        public const string TelemetryOn = "TelemetryOn";

        /// <summary>
        /// Application theme settings key.
        /// </summary>
        public const string Theme = "themeSetting";

        /// <summary>
        /// Settings key for notifications.
        /// </summary>
        public const string Notifications = "NotificationSetting";

        /// <summary>
        /// If true, screen saver will be triggered automatically.
        /// </summary>
        public const string EnableScreenSaver = "EnableScreenSaver";

        /// <summary>
        /// If true, the screensaver will just be a dark, blank page.
        /// </summary>
        public const string DarkScreensasver = "DarkScreensaver";

        /// <summary>
        /// The number of max active tracks.
        /// </summary>
        public const string MaxActive = "MaxActive";

        /// <summary>
        /// Key for the list of active tracks.
        /// </summary>
        public const string ActiveTracks = "ActiveTracks";

        /// <summary>
        /// Key for the active mix Id.
        /// </summary>
        public const string ActiveMixId = "ActiveMixId";

        /// <summary>
        /// Key used to fetch the stored auth provider Id. This is used for signing into the MSA account silently.
        /// </summary>
        public const string CurrentUserProviderId = "CurrentUserProviderId";

        /// <summary>
        /// Key used to fetch the stored user Id. This is used for signing into the MSA account silently.
        /// </summary>
        public const string CurrentUserId = "CurrentUserId";

        /// <summary>
        /// Key used to fetch the stored language override option.
        /// </summary>
        public const string OverrideLanguage = "OverrideLanguage";

        /// <summary>
        /// Key used to fetch the stored language that will override the default language.
        /// </summary>
        public const string PreferredLanguage = "PreferredLanguage";

        /// <summary>
        ///  Settings defaults.
        /// </summary>
        public static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>()
        {
            { Volume, 80d },
            { TelemetryOn, true },
            { Notifications, true },
            { EnableScreenSaver, false },
            { DarkScreensasver, false },
            { MaxActive, 3 },
            { ActiveTracks, new string[0] },
            { ActiveMixId, "" },
            { CurrentUserId, "" },
            { CurrentUserProviderId, "" },
            { Theme, "default" },
            { OverrideLanguage, false },
            { PreferredLanguage, "en-US"}
        };
    }
}
