﻿using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AlteredCarbon
{
    [HotSwappable]
    [HarmonyPatch(typeof(ITab_Pawn_Character), "PawnToShowInfoAbout", MethodType.Getter)]
    public static class ITab_Pawn_Character_PawnToShowInfoAbout_Patch
    {
        public static int lastTimeUpdated;
        public static Pawn lastPawn;
        public static bool Prefix(ref Pawn __result)
        {
            var neuralData = TryGetNeuralData();
            if (neuralData != null)
            {
                if (__result != lastPawn)
                {
                    lastPawn = __result;
                    neuralData.RefreshDummyPawn();
                }
                else if (Time.frameCount - lastTimeUpdated >= 60)
                {
                    lastTimeUpdated = Time.frameCount;
                    neuralData.RefreshDummyPawn();
                }
                __result = neuralData.DummyPawn;
                return false;
            }
            return true;
        }

        public static NeuralData TryGetNeuralData()
        {
            var selectedThing = Find.Selector.SingleSelectedThing;
            if (selectedThing is ThingWithNeuralData stack && stack.NeuralData.ContainsData)
            {
                return stack.NeuralData;
            }
            return null;
        }
    }
}
