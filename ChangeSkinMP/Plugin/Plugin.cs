using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ChangeSkinMP
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "05126619z.changeskin";
        public const string ModName = "ChangeSkin";
        public const string ModVersion = "3.3.0";

        internal static new ManualLogSource Logger;
        private readonly Harmony _harmony = new(ModGUID);
        public static Plugin Instance { get; private set; } = null!;
        public static GameObject SingletonObject;

        public void Awake()
        {
            Logger = base.Logger;
            Instance = this;
            try
            {
                PatchAllResilient();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            SceneManager.sceneUnloaded += new UnityAction<Scene>(SkinManager.OnSceneUnloaded);
            WorldgenPatches.OnWorldgenFinish += SkinManager.AfterConnection;
            NetPlayer.OnPlayerLeft += SkinManager.OnPlayerLeft;
            ModConfig.Load();
            SkinManager.TryLoadLastSkin();
            StartCoroutine(SkinManager.SpWatchdog());
            Logger.LogInfo($"Plugin {ModName} is loaded!");
        }

        /// <summary>
        /// Patch each [HarmonyPatch]-attributed class individually so one
        /// ambiguous/missing method doesn't abort the rest. Krokosha 4.0.1
        /// added overloads (e.g. NetBody.DestroyNPC) that make attribute-based
        /// lookup throw AmbiguousMatchException; Harmony.PatchAll() stops at
        /// the first failure, skipping every later patch class — which broke
        /// command registration and the SpriteRenderer.sprite setter prefix.
        /// </summary>
        void PatchAllResilient()
        {
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), true)) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Logger.LogError($"[ChangeSkin] Patch failed for {type.Name}: {e.Message}");
                }
            }

            // NetBody.DestroyNPC has overloads in 4.0.1 — attribute lookup is
            // ambiguous. Resolve manually (prefer the fewest-params overload)
            // and patch it outside the attribute loop.
            try
            {
                var destroy = typeof(NetBody)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "DestroyNPC")
                    .OrderBy(m => m.GetParameters().Length)
                    .FirstOrDefault();
                if (destroy != null)
                {
                    _harmony.Patch(
                        destroy,
                        prefix: new HarmonyMethod(typeof(NetBody_Patch_DestroyNPC), nameof(NetBody_Patch_DestroyNPC.Prefix))
                    );
                    Logger.LogInfo($"[ChangeSkin] Patched NetBody.DestroyNPC ({destroy.GetParameters().Length} params)");
                }
                else
                    Logger.LogWarning("[ChangeSkin] NetBody.DestroyNPC not found, skipping patch");
            }
            catch (Exception e)
            {
                Logger.LogError($"[ChangeSkin] DestroyNPC manual patch failed: {e.Message}");
            }
        }
    }
}
