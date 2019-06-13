using Harmony;
using Spectrum.API.Configuration;
using Spectrum.API.GUI.Controls;
using Spectrum.API.GUI.Data;
using Spectrum.API.Interfaces.Plugins;
using Spectrum.API.Interfaces.Systems;
using Spectrum.API.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Logger = Spectrum.API.Logging.Logger;

namespace CustomPOVImage
{
    public class Entry : IPlugin
    {
        internal FileSystem FileSystem;
        internal HarmonyInstance HarmonyInstance;
        internal Logger Logger;

        internal static bool POVChangeTriggered = false;
        internal static Settings Settings;
        internal static Dictionary<string, Texture2D> POVs;

        public void Initialize(IManager manager, string ipcIdentifier)
        {
            POVs = new Dictionary<string, Texture2D>();

            FileSystem = new FileSystem();
            Settings = new Settings("povstatus");
            Logger = new Logger("pov") { WriteToConsole = true };

            var currentPov = Settings.GetOrCreate("CurrentPOV", string.Empty);

            foreach (var pngFile in FileSystem.GetFiles("."))
            {
                if (!pngFile.EndsWith(".png"))
                    continue;

                var tex = new Texture2D(256, 256);

                using (var fs = FileSystem.OpenFile(pngFile))
                {
                    byte[] b = new byte[0];

                    using (BinaryReader br = new BinaryReader(fs))
                        b = br.ReadBytes((int)fs.Length);

                    tex.LoadImage(b);
                }
                POVs.Add(Path.GetFileNameWithoutExtension(pngFile), tex);
                Logger.Info($"Loaded POV decal '{pngFile}'");
            }

            if (POVs.Count == 0)
            {
                Logger.Warning("No POVs found. Stopping.");
                return;
            }

            if (string.IsNullOrEmpty(currentPov) || !POVs.ContainsKey(currentPov))
            {
                currentPov = POVs.First(x => true).Key;

                Settings["CurrentPOV"] = currentPov;
                Settings.Save();
            }

            var menuTree = new MenuTree("it.32-b.vdd.CustomPOVImageMenu", "Customizable wheel POV")
            {
                new ListBox<Texture2D>(MenuDisplayMode.Both, "it.32-b.vdd.CustomPOVImageMenu.POVs", "Available POVs")
                    .WithEntries(POVs)
                    .WithGetter(() => POVs[Settings.GetItem<string>("CurrentPOV")])
                    .WithSetter((tex) =>
                    {
                        foreach (var kvp in POVs)
                        {
                            if (tex == kvp.Value)
                            {
                                Settings["CurrentPOV"] = kvp.Key;
                                break;
                            }
                        }

                        POVChangeTriggered = true;
                    })
                    .WithDescription("Select one of available wheel POVs.")
            };
            manager.Menus.AddMenu(MenuDisplayMode.Both, menuTree, "Change how your wheels bling.");

            HarmonyInstance = HarmonyInstance.Create("it.32-b.vdd.CustomPOVImage");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(WheelPOV), "Start")]
        private class WheelPOVLoadMyImagePatch
        {
            public static void Prefix(WheelPOV __instance)
            {
                var currentPovFileName = Settings.GetItem<string>("CurrentPOV");

                if (!POVs.ContainsKey(currentPovFileName))
                    return;

                var currentPov = POVs[currentPovFileName];

                if (currentPov != null)
                    __instance.renderer_.material.mainTexture = currentPov;
            }
        }

        [HarmonyPatch(typeof(WheelPOV), "LateUpdate")]
        private class WheelPOVChangeMyImagePatch
        {
            public static void Postfix(WheelPOV __instance)
            {
                if (!POVChangeTriggered)
                    return;

                POVChangeTriggered = false;

                var currentPovFileName = Settings.GetItem<string>("CurrentPOV");

                if (!POVs.ContainsKey(currentPovFileName))
                    return;

                var currentPov = POVs[currentPovFileName];

                if (currentPov != null)
                    __instance.renderer_.material.mainTexture = currentPov;
            }
        }
    }
}
