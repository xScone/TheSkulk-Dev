using System.Reflection;
using UnityEngine;
using BepInEx;
using LethalLib.Modules;
using BepInEx.Logging;
using System.IO;
using MistEyes.Configuration;
using GameNetcodeStuff;
using MistEyes.src.Behaviours;

namespace MistEyes
{
    [BepInPlugin(ModGUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)] 
    public class Plugin : BaseUnityPlugin {
        // It is a good idea for our GUID to be more unique than only the plugin name. Notice that it is used in the BepInPlugin attribute.
        // The GUID is also used for the config file name by default.
        public const string ModGUID = "sconeys." + PluginInfo.PLUGIN_NAME;
        internal static new ManualLogSource Logger;
        internal static PluginConfig BoundConfig { get; private set; } = null;
        public static AssetBundle ModAssets;

		private bool playerInfected;
		private PlayerControllerB randomPlayer;

		private void Start()
		{
			foreach (var player in StartOfRound.Instance.allPlayerScripts)
			{
				player.gameObject.AddComponent<Infection>();
			}


		}
		private void Update()
		{
			if (TimeOfDay.Instance.hour > 1 && playerInfected == false)
			{
				randomPlayer = StartOfRound.Instance.allPlayerScripts[UnityEngine.Random.Range(0, StartOfRound.Instance.allPlayerScripts.Length)];
				playerInfected = true;
				randomPlayer.gameObject.AddComponent<Infection>();
			}
			// TODO: Create a method that will remove infection script from players when the round ends
			foreach (var player in StartOfRound.Instance.allPlayerScripts)
			{
				if (player.GetComponent<Infection>() != null)
				{
					player.GetComponent<Infection>().enabled = false;
				}
			}
		}

		private void Awake() {
            Logger = base.Logger;
            BoundConfig = new PluginConfig(this);
            InitializeNetworkBehaviours();

            var bundleName = "misteyes-dev";
            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), bundleName));
            if (ModAssets == null) {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }

            var MistEyes = ModAssets.LoadAsset<EnemyType>("MistEyesDirector");
			var SkulkEnemy = ModAssets.LoadAsset<EnemyType>("TheSkulkEnemy");
            //var MistEyesTN = ModAssets.LoadAsset<TerminalNode>("MistEyesTN");
            //var MistEyesTK = ModAssets.LoadAsset<TerminalKeyword>("MistEyesTK");
            
            // Network Prefabs need to be registered. See https://docs-multiplayer.unity3d.com/netcode/current/basics/object-spawning/
            // LethalLib registers prefabs on GameNetworkManager.Start.
            NetworkPrefabs.RegisterNetworkPrefab(MistEyes.enemyPrefab);
			Enemies.RegisterEnemy(MistEyes, 10000, Levels.LevelTypes.All, Enemies.SpawnType.Default, null, null);
			Enemies.RegisterEnemy(SkulkEnemy, 10000, Levels.LevelTypes.All, Enemies.SpawnType.Default, null, null);

			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private static void InitializeNetworkBehaviours() {
            // See https://github.com/EvaisaDev/UnityNetcodePatcher?tab=readme-ov-file#preparing-mods-for-patching
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
    }
}