﻿using BepInEx.Configuration;
using HarmonyLib; // requires 0Harmony.dll
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using UnboundLib; // requires UnboundLib.dll
using UnboundLib.Networking;
using UnityEngine; // requires UnityEngine.dll, UnityEngine.CoreModule.dll, and UnityEngine.AssetBundleModule.dll

// requires Assembly-CSharp.dll
// requires MMHOOK-Assembly-CSharp.dll

// This code is based on the original work from https://github.com/pdcook/PickNCards
// Copyright (C) [2023] [pdcook/Pykess]
// License: GNU General Public License v3.0

// Modifications:
// - Grid card placement
// - Draw differs on the first round
// - Cards are flipped face-up on the first round
namespace DrawNCards
{
    public class DrawNCards
    {
        internal const int maxDraws = 40;

        public static ConfigEntry<int> NumDrawsConfig;
        internal static int numPerRow = 10;
        internal static int ifEnabledNumDraws = 40;
        internal static int defaultNumDraws = 5;

        private const float arc_A = 0.2102040816f;
        private const float arc_B = 1.959183674f;
        private const float arc_C = -1.959183674f;
        private const float Harc_A = 1f;
        private const float Harc_B = 2f;
        private const float Harc_C = -1.7f;
        internal static float Arc(float x, float offset = 0f)
        {
            // inputs and outputs are in SCREEN UNITS

            if (offset == 0f)
            {
                // approximate parabola of default card arc, correlation coeff = 1
                return arc_C * x * x + arc_B * x + arc_A + offset;
            }
            else if (offset < 0f)
            {
                // approximate hyperbola of default card arc
                return -UnityEngine.Mathf.Sqrt(1 + (Harc_B * Harc_B) * UnityEngine.Mathf.Pow(x - xC, 2f) / (Harc_A * Harc_A)) - Harc_C + offset;
            }
            else
            {
                // flattened hyperbola for top arc
                return -UnityEngine.Mathf.Sqrt(1 + (Harc_B * Harc_B) * UnityEngine.Mathf.Pow(x - xC, 2f) / (2f * Harc_A * Harc_A)) - Harc_C + offset;
            }
        }

        internal static List<Vector3> GetInnateSkillPositions(int N, float offset = 0f)
        {
            int numRows = ifEnabledNumDraws / numPerRow;

            float bottomMargin = 0.11f;
            float leftMargin = 0.05f;

            float rowHeight = 0.25f;

            float spacing = 0.1f;

            List<Vector3> result = new List<Vector3>();

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numPerRow; c++)
                {
                    result.Add(new Vector3(leftMargin + c * spacing, bottomMargin + r * rowHeight, -5f));
                }
            }

            return result;
        }

        private const float xC = 0.5f;
        private const float yC = 0.5f;
        private const float absMaxX = 0.85f;
        private const float defMaxXWorld = 25f;
        internal const float z = -5f;
        internal static List<Vector3> GetPositions(int N, float offset = 0f)
        {
            // everything is in SCREEN UNITS

            if (InnateSkill.InnateSkill.IsInnateRoundInEffect())
            {
                return GetInnateSkillPositions(N, offset);
            }
            if (N == 0)
            {
                throw new Exception("Must have at least one card.");
            }
            else if (N == 1)
            {
                return new List<Vector3>() { new Vector3(xC, yC, z) };
            }
            else if (N == 3)
            {
                offset -= 0.025f;
            }
            else if (N > DrawNCards.maxDraws / 2)
            {
                int N1 = (int)UnityEngine.Mathf.RoundToInt(N / 2);
                int N2 = N - N1;
                int k1;
                int k2;
                if (N1 >= N2) { k1 = N1; k2 = N2; }
                else { k1 = N2; k2 = N1; }
                List<Vector3> positions1 = GetPositions(k1, offset - 0.16f);
                List<Vector3> positions2 = GetPositions(k2, offset + 0.125f);
                return positions1.Concat(positions2).ToList();
            }

            float maxX = absMaxX;

            if (N < 4) { maxX = 0.75f; }
            else
            {
                maxX = UnityEngine.Mathf.Clamp(maxX + 0.025f * (N - 5), maxX, 0.925f);
            }

            // we assume symmetry about x = 0 and fill from the center out
            List<float> xs = new List<float>() { };

            float x_init = xC;

            // if N is odd, place a card exactly at the center
            if (N % 2 != 0)
            {
                x_init = xC;
                xs.Add(x_init);
                N--;

                // now N is guarunteed to be even, so:
                int k = N / 2;

                float step = (maxX - x_init) / k;

                float x = x_init;

                for (int i = 0; i < k; i++)
                {
                    x += step;
                    xs.Add(x); // add the next point to the right and its reflection over xC
                    xs.Add(2f * xC - x);
                }
            }
            // if N is even, do it the easy way
            else
            {
                x_init = 1f - maxX;

                float step = (maxX - x_init) / (N - 1);

                float x = x_init;

                for (int i = 0; i < N; i++)
                {
                    xs.Add(x);
                    x += step;
                }
            }

            // sort by x
            xs.Sort();

            List<Vector3> positions = new List<Vector3>() { };

            foreach (float x_ in xs)
            {
                positions.Add(new Vector3(x_, Arc(x_, offset), z));
            }

            return positions;
        }

        internal static int GetNumDraws()
        {
            return InnateSkill.InnateSkill.IsInnateRoundInEffect() ? ifEnabledNumDraws : defaultNumDraws;
        }
        internal static Vector3 GetScale(int N)
        {
            // camera scale factor
            float factor = 1.04f * absMaxX.xWorldPoint() / defMaxXWorld;

            if (N == 5)
            {
                return new Vector3(1f, 1f, 1f) * factor;
            }
            else if (N < 5)
            {
                return new Vector3(1f, 1f, 1f) * factor * (1f + 1f / (2f * N));
            }
            else if (N > DrawNCards.maxDraws / 2)
            {
                return new Vector3(1f, 1f, 1f) * factor * UnityEngine.Mathf.Clamp(5f / (N / 2 + 2), 3f / 5f, 1f);
            }
            else
            {
                return new Vector3(1f, 1f, 1f) * factor * UnityEngine.Mathf.Clamp(5f / (N - 1), 3f / 5f, 1f);
            }

        }
        private const float maxPhi = 15f;
        internal static float ArcAngle(float x)
        {
            // x is in SCREEN units
            return (-maxPhi / (absMaxX - xC)) * (x - xC);
        }
        internal static List<Quaternion> GetRotations(int N)
        {
            List<Vector3> positions = GetPositions(N);

            List<Quaternion> rotations = new List<Quaternion>() { };
            foreach (Vector3 pos in positions)
            {
                rotations.Add(Quaternion.Euler(0f, 0f, 0f));
            }
            return rotations;
        }

    }


    //// patch to sort cards by name
    //[Serializable]
    //[HarmonyPatch(typeof(CardChoice), "ReplaceCards")]
    //class CardChoicePatcReplaceCards
    //{
    //    private static CardChoice _cardChoiceInstance = null;

    //    private static void Prefix(CardChoice __instance)
    //    {
    //        UnityEngine.Debug.Log($"Own Instantiated cardChoiceInstance: [{__instance}]");
    //        _cardChoiceInstance = __instance;
    //    }

    //    private static void SortCards()
    //    {
    //        // sort cards by name
    //        UnityEngine.Debug.Log($"Own about to start sorting cards...");
    //        List<GameObject> sc = (List<GameObject>)_cardChoiceInstance.GetFieldValue("spawnedCards");
    //        UnityEngine.Debug.Log($"Own about to sort cards, len: [{sc.Count}]");
    //        sc = sc.OrderBy(go => go.GetComponent<CardInfo>().cardName).ToList();
    //        UnityEngine.Debug.Log($"Own 1");
    //        if (sc.Count == 39)
    //        {
    //            UnityEngine.Debug.Log($"Own 3");
    //            _cardChoiceInstance.SetFieldValue("spawnedCards", sc);
    //        }
    //        UnityEngine.Debug.Log($"Own 2");
    //    }

    //    private static void Postfix(ref IEnumerator __result)
    //    {
    //        new Thread(() =>
    //        {
    //            Thread.Sleep(1000);
    //            SortCards();
    //        }).Start();

    //        //NetworkingManager.RPC_Others(typeof(CardChoicePatchSpawn), nameof(RPCO_AddRemotelySpawnedCard), new object[] { __result.GetComponent<PhotonView>().ViewID });
    //    }
    //}

    [Serializable]
    [HarmonyPatch(typeof(CardVisuals), "Start")]
    class CardVisualsPatchStart
    {
        private static CardVisuals _cardVisualsInstance = null;

        private static void Prefix(CardVisuals __instance)
        {
            _cardVisualsInstance = __instance;
        }

        [HarmonyPostfix]
        private static void markCardAsShowing()
        {
            if (InnateSkill.InnateSkill.IsInnateRoundInEffect())
            {
                _cardVisualsInstance.ChangeSelected(true);
            }
        }
    }

    // patch to allow up and down to jump between cards
    [Serializable]
    [HarmonyPatch(typeof(CardChoice), "DoPlayerSelect")]
    class CardChoicePatcDoPlayerSelect
    {
        private static CardChoice _cardChoiceInstance = null;
        private static int lastAction = 0; // 0: Unknown, 1: Up, 2: Down

        private static void Prefix(CardChoice __instance)
        {
            _cardChoiceInstance = __instance;
        }

        [HarmonyPostfix]
        private static void jumpAheadForUpOrDownInput()
        {
            if (lastAction != 1 && DidMoveUp())
            {
                lastAction = 1;
                AddToCardSelected(DrawNCards.numPerRow);
            }
            else if (lastAction != 2 && DidMoveDown())
            {
                lastAction = 2;
                AddToCardSelected(-DrawNCards.numPerRow);
            }
            else
            {
                lastAction = 0;
            }
        }

        private static void AddToCardSelected(int value)
        {
            int current = (int)_cardChoiceInstance.GetFieldValue("currentlySelectedCard");
            List<GameObject> sc = (List<GameObject>)_cardChoiceInstance.GetFieldValue("spawnedCards");
            int newValue = Mathf.Clamp(current + value, 0, sc.Count - 1);
            _cardChoiceInstance.SetFieldValue("currentlySelectedCard", newValue);
        }

        private static bool DidMoveUp()
        {
            return Input.GetKeyDown("w") || Input.GetAxis("Vertical") > 0.8;
        }

        private static bool DidMoveDown()
        {
            return Input.GetKeyDown("s") || Input.GetAxis("Vertical") < -0.8;
        }
    }

    // patch to change scale of cards
    [Serializable]
    [HarmonyPatch(typeof(CardChoice), "Spawn")]
    class CardChoicePatchSpawn
    {
        private static void Postfix(ref GameObject __result)
        {
            __result.transform.localScale = DrawNCards.GetScale(DrawNCards.GetNumDraws());

            NetworkingManager.RPC_Others(typeof(CardChoicePatchSpawn), nameof(RPCO_AddRemotelySpawnedCard), new object[] { __result.GetComponent<PhotonView>().ViewID });
        }
        // set scale client-side
        [UnboundRPC]
        private static void RPCO_AddRemotelySpawnedCard(int viewID)
        {
            GameObject card = PhotonView.Find(viewID).gameObject;

            // set the scale
            card.transform.localScale = DrawNCards.GetScale(DrawNCards.GetNumDraws());
        }
    }
    // reconfigure card placement before each pick in case the map size has changed
    [Serializable]
    [HarmonyPatch(typeof(CardChoice), "StartPick")]
    class CardChoicePatchStartPick
    {
        private static GameObject _cardVis = null;
        private static GameObject cardVis
        {
            get
            {
                if (_cardVis != null) { return _cardVis; }
                else
                {
                    _cardVis = ((Transform[])CardChoice.instance.GetFieldValue("children"))[0].gameObject;
                    return _cardVis;
                }
            }
            set { }
        }
        private static void Prefix(CardChoice __instance)
        {
            // remove all children except the zeroth
            foreach (Transform child in ((Transform[])CardChoice.instance.GetFieldValue("children")).Skip(1))
            {
                UnityEngine.GameObject.Destroy(child.gameObject);
            }

            List<Vector3> positions = DrawNCards.GetPositions(DrawNCards.GetNumDraws()).WorldPoint();
            List<Quaternion> rotations = DrawNCards.GetRotations(DrawNCards.GetNumDraws());
            Vector3 scale = DrawNCards.GetScale(DrawNCards.GetNumDraws());

            List<Transform> children = new List<Transform>() { cardVis.transform };

            // change properties of the first cardvis
            children[0].position = positions[0];
            children[0].rotation = rotations[0];
            children[0].localScale = scale;

            // start at 1 since the first cardVis should already be present
            for (int i = 1; i < DrawNCards.GetNumDraws(); i++)
            {
                GameObject newChild = UnityEngine.GameObject.Instantiate(cardVis, positions[i], rotations[i], __instance.transform);
                newChild.name = children.Count.ToString();
                children.Add(newChild.transform);
            }

            __instance.SetFieldValue("children", children.ToArray());
        }
    }

    // patch to fix armpos
    [Serializable]
    [HarmonyPatch(typeof(CardChoiceVisuals), "Update")]
    class CardChoiceVisualsPatchUpdate
    {
        private static bool Prefix(CardChoiceVisuals __instance)
        {
            if (!(bool)__instance.GetFieldValue("isShowinig"))
            {
                return false;
            }
            if (Time.unscaledDeltaTime > 0.1f)
            {
                return false;
            }
            if (__instance.currentCardSelected >= __instance.cardParent.transform.childCount || __instance.currentCardSelected < 0)
            {
                return false;
            }
            if (__instance.rightHandTarget.position.x == float.NaN || __instance.rightHandTarget.position.y == float.NaN || __instance.rightHandTarget.position.z == float.NaN)
            {
                __instance.rightHandTarget.position = Vector3.zero;
                __instance.SetFieldValue("rightHandVel", Vector3.zero);
            }
            if (__instance.leftHandTarget.position.x == float.NaN || __instance.leftHandTarget.position.y == float.NaN || __instance.leftHandTarget.position.z == float.NaN)
            {
                __instance.leftHandTarget.position = Vector3.zero;
                __instance.SetFieldValue("leftHandVel", Vector3.zero);
            }
            GameObject gameObject = __instance.cardParent.transform.GetChild(__instance.currentCardSelected).gameObject;
            Vector3 vector = gameObject.transform.GetChild(0).position;
            if (vector.x < 0f) // it was literally this simple Landfall...
            {
                __instance.SetFieldValue("leftHandVel", (Vector3)__instance.GetFieldValue("leftHandVel") + (vector - __instance.leftHandTarget.position) * __instance.spring * Time.unscaledDeltaTime);
                __instance.SetFieldValue("leftHandVel", (Vector3)__instance.GetFieldValue("leftHandVel") - ((Vector3)__instance.GetFieldValue("leftHandVel")) * Time.unscaledDeltaTime * __instance.drag);
                __instance.SetFieldValue("rightHandVel", (Vector3)__instance.GetFieldValue("rightHandVel") + (((Vector3)__instance.GetFieldValue("rightHandRestPos")) - __instance.rightHandTarget.position) * __instance.spring * Time.unscaledDeltaTime * 0.5f);
                __instance.SetFieldValue("rightHandVel", (Vector3)__instance.GetFieldValue("rightHandVel") - ((Vector3)__instance.GetFieldValue("rightHandVel")) * Time.unscaledDeltaTime * __instance.drag * 0.5f);
                __instance.SetFieldValue("rightHandVel", (Vector3)__instance.GetFieldValue("rightHandVel") + __instance.sway * new Vector3(-0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed, 0f), -0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed + 100f, 0f), 0f) * Time.unscaledDeltaTime);
                __instance.shieldGem.transform.position = __instance.rightHandTarget.position;
                if (__instance.framesToSnap > 0)
                {
                    __instance.leftHandTarget.position = vector;
                }
            }
            else
            {
                __instance.SetFieldValue("rightHandVel", (Vector3)__instance.GetFieldValue("rightHandVel") + (vector - __instance.rightHandTarget.position) * __instance.spring * Time.unscaledDeltaTime);
                __instance.SetFieldValue("rightHandVel", (Vector3)__instance.GetFieldValue("rightHandVel") - ((Vector3)__instance.GetFieldValue("rightHandVel")) * Time.unscaledDeltaTime * __instance.drag);
                __instance.SetFieldValue("leftHandVel", (Vector3)__instance.GetFieldValue("leftHandVel") + (((Vector3)__instance.GetFieldValue("leftHandRestPos")) - __instance.leftHandTarget.position) * __instance.spring * Time.unscaledDeltaTime * 0.5f);
                __instance.SetFieldValue("leftHandVel", (Vector3)__instance.GetFieldValue("leftHandVel") - ((Vector3)__instance.GetFieldValue("leftHandVel")) * Time.unscaledDeltaTime * __instance.drag * 0.5f);
                __instance.SetFieldValue("leftHandVel", (Vector3)__instance.GetFieldValue("leftHandVel") + __instance.sway * new Vector3(-0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed, Time.unscaledTime * __instance.swaySpeed), -0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed + 100f, Time.unscaledTime * __instance.swaySpeed + 100f), 0f) * Time.unscaledDeltaTime);
                __instance.shieldGem.transform.position = __instance.leftHandTarget.position;
                if (__instance.framesToSnap > 0)
                {
                    __instance.rightHandTarget.position = vector;
                }
            }
            __instance.framesToSnap--;
            __instance.leftHandTarget.position += (Vector3)__instance.GetFieldValue("leftHandVel") * Time.unscaledDeltaTime;
            __instance.rightHandTarget.position += (Vector3)__instance.GetFieldValue("rightHandVel") * Time.unscaledDeltaTime;

            return false; // skip original (BAD IDEA)
        }
    }
    public static class WorldToScreenExtensions
    {
        public static Vector3 ScreenPoint(this Vector3 v3)
        {
            Vector3 vec = MainCam.instance.transform.GetComponent<Camera>().WorldToScreenPoint(v3);
            vec.x /= (float)Screen.width;
            vec.y /= (float)Screen.height;
            vec.z = 0f;

            return vec;
        }

        public static Vector3 WorldPoint(this Vector3 v3)
        {
            v3.x *= (float)Screen.width;
            v3.y *= (float)Screen.height;
            Vector3 vec = MainCam.instance.transform.GetComponent<Camera>().ScreenToWorldPoint(v3);
            vec.z = DrawNCards.z;
            return vec;
        }

        public static float xScreenPoint(this float x)
        {
            return ((new Vector3(x, 0f, 0f)).ScreenPoint()).x;
        }
        public static float xWorldPoint(this float x)
        {
            return ((new Vector3(x, 0f, 0f)).WorldPoint()).x;
        }
        public static float yScreenPoint(this float y)
        {
            return ((new Vector3(0f, y, 0f)).ScreenPoint()).y;
        }
        public static float yWorldPoint(this float y)
        {
            return ((new Vector3(0f, y, 0f)).WorldPoint()).y;
        }

        public static List<Vector3> ScreenPoint(this List<Vector3> v3)
        {
            List<Vector3> v3screen = new List<Vector3>() { };
            foreach (Vector3 v in v3)
            {
                v3screen.Add(v.ScreenPoint());
            }
            return v3screen;
        }
        public static List<Vector3> WorldPoint(this List<Vector3> v3)
        {
            List<Vector3> v3screen = new List<Vector3>() { };
            foreach (Vector3 v in v3)
            {
                v3screen.Add(v.WorldPoint());
            }
            return v3screen;
        }
    }
}
