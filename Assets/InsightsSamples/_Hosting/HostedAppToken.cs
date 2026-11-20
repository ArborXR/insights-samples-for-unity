using System;
using System.Runtime.InteropServices;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace InsightsSamples.Hosting
{
    /// <summary>
    /// The App Token of a WebGL build that ships without one, such as a hosted copy. The visitor brings their own,
    /// from an earlier visit or the Connect panel, which a link (#app_token=…) fills in.
    /// Records go to the organization that owns the app, unless the visitor pairs with a passcode instead. A link
    /// that also says how to connect (&amp;connect=direct or pair) connects without asking. Not a lesson: an app ships
    /// its App Token in its build.
    /// </summary>
    public static class HostedAppToken
    {
        /// <summary>A WebGL build with no App Token of its own. Other builds run as they were built.</summary>
        public static bool Enabled { get; private set; }

        private const string PrefsKey = "insights_samples_app_token";
        private const string PairsPrefsKey = "insights_samples_pairs";
        private const string PairingOffPrefsKey = "insights_samples_pairing_off";
        private const string LinkKey = "app_token";
        private const string ConnectLinkKey = "connect";
        private const string ConnectByPairing = "pair";
        private const string ConnectDirectly = "direct";
        private const string PairingLinkKey = "pairing";
        private const string PairingIsOff = "off";

        public static string Current => Configuration.Instance.appToken;
        public static bool HasToken => !string.IsNullOrEmpty(Current);

        /// <summary>The visitor pairs with a passcode. Otherwise the App Token is the org token too.</summary>
        public static bool Pairs { get; private set; }

        /// <summary>The link brought a different connection than this browser had, so a stored pairing is the old one's.</summary>
        public static bool ChangedAtLaunch { get; private set; }

        /// <summary>The App Token a link brought for the Connect panel's field. The visitor chooses how it connects.</summary>
        public static string Offered { get; private set; }

        /// <summary>
        /// The last link said the visitor's organization can't create pairing passcodes (&amp;pairing=off). The build
        /// can't tell on its own, so without a link it assumes pairing is there.
        /// </summary>
        public static bool PairingOff { get; private set; }

        // Runs before the SDK starts (BeforeSceneLoad), so its first sign-in already has the token.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Load()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var config = Configuration.Instance;
            Enabled = string.IsNullOrEmpty(config.appToken);
            if (!Enabled) return;
            WatchLink();
            // The build is for trying Insights with your own org: the pairing prompt has no "Not now".
            config.enablePairingDismiss = false;
            var asset = LoadedAsset;
            if (asset != null) asset.enablePairingDismiss = false;

            var saved = PlayerPrefs.GetString(PrefsKey, "");
            var savedPairs = PlayerPrefs.GetInt(PairsPrefsKey, 0) == 1;
            PairingOff = PlayerPrefs.GetInt(PairingOffPrefsKey, 0) == 1;
            var linked = FromLink(LinkKey);
            var linkedConnect = FromLink(ConnectLinkKey);
            if (!string.IsNullOrEmpty(linked) && linkedConnect == null)
            {
                // A link with only the token fills in the Connect panel, and the visitor connects it.
                Offered = linked;
                PairingOff = FromLink(PairingLinkKey) == PairingIsOff;
                PlayerPrefs.SetInt(PairingOffPrefsKey, PairingOff ? 1 : 0);
                PlayerPrefs.Save();
                ClearLink();
                linked = null;
            }

            var pairs = linkedConnect == ConnectByPairing;
            if (!string.IsNullOrEmpty(linked) && TryApply(linked, pairs))
            {
                ClearLink();
                if (linked == saved && pairs == savedPairs) return;
                Save(linked, pairs);
                if (string.IsNullOrEmpty(saved)) return;
                // Started by the Connect panel once the SDK is up, after it forgets the old connection's pairing.
                ChangedAtLaunch = true;
                TurnOffAutoStart();
            }
            else if (!string.IsNullOrEmpty(saved))
            {
                TryApply(saved, savedPairs);
            }

            // The SDK keeps the identity its first sign-in finds for the whole launch, and without a token that would
            // be none. The Connect panel starts sign-in once it has one.
            if (!HasToken) TurnOffAutoStart();
        }

        /// <summary>Sets the connection if it's one the SDK accepts; otherwise leaves the current one.</summary>
        public static bool TryApply(string token, bool pairs)
        {
            var config = Configuration.Instance;
            var previous = (config.appToken, config.orgToken, config.buildType);
            // An App Token names the organization that owns its app, so Insights takes it as that organization's
            // token too. The SDK only reads a configured org token in a development build.
            SetConnection(token, pairs ? "" : token, pairs ? "production" : "development");
            // The SDK validates its settings once, when they first load, which is before this token was set. Validating
            // again also clears that start-up error.
            if (config.IsValid())
            {
                Pairs = pairs;
                return true;
            }
            SetConnection(previous.appToken, previous.orgToken, previous.buildType);
            config.IsValid();
            return false;
        }

        public static void Save(string token, bool pairs)
        {
            PlayerPrefs.SetString(PrefsKey, token);
            PlayerPrefs.SetInt(PairsPrefsKey, pairs ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Forget()
        {
            SetConnection("", "", Configuration.Instance.buildType);
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        // StartNewSession() resets the SDK's runtime configuration to the AbxrLib asset, which this build ships without a
        // token. So each setting made here goes on the loaded asset too, where the reset finds it. Only in memory: a
        // player never saves the asset.
        private static AppConfig LoadedAsset => Resources.Load<AppConfig>("AbxrLib");

        private static void SetConnection(string appToken, string orgToken, string buildType)
        {
            var config = Configuration.Instance;
            (config.appToken, config.orgToken, config.buildType) = (appToken, orgToken, buildType);
            var asset = LoadedAsset;
            if (asset != null) (asset.appToken, asset.orgToken, asset.buildType) = (appToken, orgToken, buildType);
        }

        private static void TurnOffAutoStart()
        {
            Configuration.Instance.enableAutoStartAuthentication = false;
            var asset = LoadedAsset;
            if (asset != null) asset.enableAutoStartAuthentication = false;
        }

        /// <summary>
        /// Reloads the page with this connection in its link, where the next launch picks it up. False where there's no
        /// page to reload.
        /// </summary>
        public static bool Relaunch(string token, bool pairs)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var connect = pairs ? ConnectByPairing : ConnectDirectly;
            HostingRelaunch($"{LinkKey}={Uri.EscapeDataString(token)}&{ConnectLinkKey}={connect}");
            return true;
#else
            return false;
#endif
        }

        /// <summary>The app's id from the token's payload, for showing which app is connected.</summary>
        public static string AppId()
        {
            var parts = (Current ?? "").Split('.');
            if (parts.Length != 3) return null;
            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                return JsonUtility.FromJson<Claims>(json)?.appId;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string FromLink(string key)
        {
            var url = Application.absoluteURL ?? "";
            var hash = url.IndexOf('#');
            if (hash < 0) return null;
            foreach (var pair in url.Substring(hash + 1).Split('&'))
            {
                if (pair.StartsWith(key + "=")) return Uri.UnescapeDataString(pair.Substring(key.Length + 1)).Trim();
            }
            return null;
        }

        /// <summary>Takes the token out of the address bar, so it isn't left in a link someone copies.</summary>
        private static void ClearLink()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HostingClearLink();
#endif
        }

        /// <summary>
        /// A link opened in a tab that's already running the build only changes the address. Reloads the page then, so
        /// the next launch reads it.
        /// </summary>
        private static void WatchLink()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HostingWatchLink(LinkKey);
#endif
        }

        [Serializable]
        private class Claims
        {
            public string appId;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void HostingClearLink();
        [DllImport("__Internal")] private static extern void HostingRelaunch(string fragment);
        [DllImport("__Internal")] private static extern void HostingWatchLink(string key);
#endif
    }
}
