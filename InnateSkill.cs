using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Networking;
using UnboundLib.Utils.UI;
using UnityEngine;


// This code is based on the original work from https://github.com/pdcook/PickNCards
// Copyright (C) [2023] [pdcook/Pykess]
// License: GNU General Public License v3.0

// Modifications:
// - Removed config to change number of picks/draws, plan to re-add them later
// - Add config to enable/disable the mod
// - Add first round check
namespace InnateSkill
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(ModId, ModName, "0.2.4")]
    [BepInProcess("Rounds.exe")]
    public class InnateSkill : BaseUnityPlugin
    {
        private const string ModId = "manyways.rounds.plugins.innateskill";
        private const string ModName = "Innate Skill";
        private const string CompatibilityModName = "InnateSkill";

        private const int picksOnFirstTurn = 1;

        internal static InnateSkill instance;

        public static ConfigEntry<bool> EnabledConfig;
        internal static bool modEnabled;
        internal static bool firstRound = true;
        internal static float delay = 0.01f;

        internal static bool lockPickQueue = false;
        internal static List<int> playerIDsToPick = new List<int>() { };
        internal static bool extraPicksInPorgress = false;

        private void Awake()
        {

            InnateSkill.instance = this;

            // bind configs with BepInEx
            EnabledConfig = Config.Bind(CompatibilityModName, "Enabled", true, "Allow an innate skill pick.");
            InnateSkill.modEnabled = InnateSkill.EnabledConfig.Value;

            // apply patches
            new Harmony(ModId).PatchAll();
        }
        private void Start()
        {
            // add credits
            Unbound.RegisterCredits("Innate Skill", new string[] { "manyways (Code)" }, new string[] { "github" }, new string[] { "https://github.com/sk-manyways" });

            // add GUI to modoptions menu
            Unbound.RegisterMenu(ModName, () => { }, this.NewGUI, null, false);

            // handshake to sync settings
            Unbound.RegisterHandshake(InnateSkill.ModId, this.OnHandShakeCompleted);

            // hooks for picking N cards
            GameModeManager.AddHook(GameModeHooks.HookPickStart, (gm) => InnateSkill.ResetPickQueue(), GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, InnateSkill.ExtraPicks, GameModeHooks.Priority.First);
        }
        private void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(typeof(InnateSkill), nameof(SyncSettings), new object[] { InnateSkill.modEnabled });
            }
        }
        [UnboundRPC]
        private static void SyncSettings(bool host_enabled)
        {
            InnateSkill.modEnabled = host_enabled;
        }
        private void NewGUI(GameObject menu)
        {

            MenuHandler.CreateText(ModName + " Options", menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
            void EnabledChanged(bool val)
            {
                InnateSkill.EnabledConfig.Value = val;
                InnateSkill.modEnabled = val;
                OnHandShakeCompleted();
            }
            MenuHandler.CreateToggle(EnabledConfig.Value, "Innate Skill Pick Enabled", menu, EnabledChanged);
        }
        [UnboundRPC]
        public static void RPC_RequestSync(int requestingPlayer)
        {
            NetworkingManager.RPC(typeof(InnateSkill), nameof(InnateSkill.RPC_SyncResponse), requestingPlayer, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        [UnboundRPC]
        public static void RPC_SyncResponse(int requestingPlayer, int readyPlayer)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == requestingPlayer)
            {
                InnateSkill.instance.RemovePendingRequest(readyPlayer, nameof(InnateSkill.RPC_RequestSync));
            }
        }

        public static bool IsInnateRoundInEffect()
        {
            return InnateSkill.modEnabled && InnateSkill.firstRound;
        }

        private IEnumerator WaitForSyncUp()
        {
            if (PhotonNetwork.OfflineMode)
            {
                yield break;
            }
            yield return this.SyncMethod(nameof(InnateSkill.RPC_RequestSync), null, PhotonNetwork.LocalPlayer.ActorNumber);
        }
        internal static IEnumerator ResetPickQueue()
        {
            if (!InnateSkill.extraPicksInPorgress)
            {
                InnateSkill.playerIDsToPick = new List<int>() { };
                InnateSkill.lockPickQueue = false;
            }
            yield break;
        }

        internal static bool IsFirstRound(IGameModeHandler gm)
        {
            InnateSkill.firstRound = gm.GetPointWinners().Length == 0;
            return InnateSkill.firstRound;
        }

        internal static IEnumerator ExtraPicks(IGameModeHandler gm)
        {
            int numPics = InnateSkill.modEnabled && IsFirstRound(gm) ? InnateSkill.picksOnFirstTurn : 1;
            //UnityEngine.Debug.Log($"First round result... [{firstRound}]");

            // doing this to ensure there is only 1 first round
            InnateSkill.firstRound = false;

            if (!InnateSkill.extraPicksInPorgress)
            {
                if (numPics <= 1 || InnateSkill.playerIDsToPick.Count() < 1)
                {
                    yield break;
                }

                InnateSkill.lockPickQueue = true;
                InnateSkill.extraPicksInPorgress = true;
                yield return InnateSkill.instance.WaitForSyncUp();

                for (int _ = 0; _ < numPics - 1; _++)
                {
                    yield return InnateSkill.instance.WaitForSyncUp();
                    //yield return GameModeManager.TriggerHook(GameModeHooks.HookPickStart);
                    for (int i = 0; i < InnateSkill.playerIDsToPick.Count(); i++)
                    {
                        yield return InnateSkill.instance.WaitForSyncUp();
                        int playerID = InnateSkill.playerIDsToPick[i];
                        yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickStart);
                        CardChoiceVisuals.instance.Show(playerID, true);
                        yield return CardChoice.instance.DoPick(1, playerID, PickerType.Player);
                        yield return new WaitForSecondsRealtime(0.1f);
                        yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickEnd);
                        yield return new WaitForSecondsRealtime(0.1f);
                    }
                    //yield return GameModeManager.TriggerHook(GameModeHooks.HookPickEnd);
                }

                CardChoiceVisuals.instance.Hide();
                InnateSkill.extraPicksInPorgress = false;
            }

            yield break;
        }
        // patch to skip pick phase if requested
        [Serializable]
        [HarmonyPatch(typeof(CardChoiceVisuals), "Show")]
        [HarmonyPriority(Priority.First)]
        class CardChoiceVisualsPatchShow
        {
            private static bool Prefix(CardChoice __instance)
            {
                if (InnateSkill.picksOnFirstTurn == 0) { return false; }
                else { return true; }
            }
        }

        // patch to determine which players have picked this phase
        [Serializable]
        [HarmonyPatch(typeof(CardChoice), "DoPick")]
        [HarmonyPriority(Priority.First)]
        class CardChoicePatchDoPick
        {
            private static bool Prefix(CardChoice __instance)
            {
                if (InnateSkill.picksOnFirstTurn == 0) { return false; }
                else { return true; }
            }
            private static void Postfix(CardChoice __instance, int picketIDToSet)
            {
                if (!InnateSkill.lockPickQueue && /*checked if player is alreadly in the queue*/!InnateSkill.playerIDsToPick.Contains(picketIDToSet)) { InnateSkill.playerIDsToPick.Add(picketIDToSet); }
            }
        }


        // patch to reset the first round indicator when the game starts
        [Serializable]
        [HarmonyPatch(typeof(GM_ArmsRace), "StartGame")]
        class GM_ArmsRacePatchStartGame
        {
            [HarmonyPostfix]
            private static void resetFirstRound()
            {
                InnateSkill.firstRound = true;
            }
        }

        // patch to reset the first round indicator when the game restarts
        [Serializable]
        [HarmonyPatch(typeof(GM_ArmsRace), "GameOverRematch")]
        class GM_ArmsRacePatchGameOverRematch
        {
            [HarmonyPostfix]
            private static void resetFirstRound()
            {
                InnateSkill.firstRound = true;
            }
        }

        // patch to change draw rate
        [HarmonyPatch]
        class CardChoicePatchReplaceCards
        {
            static Type GetNestedReplaceCardsType()
            {
                Type[] nestedTypes = typeof(CardChoice).GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic);
                Type nestedType = null;

                foreach (Type type in nestedTypes)
                {
                    if (type.Name.Contains("ReplaceCards"))
                    {
                        nestedType = type;
                        break;
                    }
                }

                return nestedType;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(GetNestedReplaceCardsType(), "MoveNext");
            }

            static float GetNewDelay()
            {
                return InnateSkill.delay;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();

                FieldInfo f_theInt = ExtensionMethods.GetFieldInfo(typeof(PublicInt), "theInt");
                MethodInfo m_GetNewDelay = ExtensionMethods.GetMethodInfo(typeof(CardChoicePatchReplaceCards), nameof(GetNewDelay));

                int index = -1;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].StoresField(f_theInt) && codes[i + 1].opcode == OpCodes.Ldarg_0 && codes[i + 2].opcode == OpCodes.Ldc_R4 && (float)(codes[i + 2].operand) == 0.1f && codes[i + 3].opcode == OpCodes.Newobj)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                {
                    throw new Exception("[REPLACECARDS PATCH] INSTRUCTION NOT FOUND");
                }
                else
                {
                    codes[index + 2] = new CodeInstruction(OpCodes.Call, m_GetNewDelay);
                }

                return codes.AsEnumerable();
            }
        }
    }
}

