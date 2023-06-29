﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VFED;

[StaticConstructorOnStartup]
public static class Utilities
{
    private static readonly Func<Projectile, float> getArcHeightFactor =
        AccessTools.PropertyGetter(typeof(Projectile), "ArcHeightFactor").CreateDelegate<Func<Projectile, float>>();

    private static readonly Func<Projectile, float> getDistanceCoveredFraction =
        AccessTools.PropertyGetter(typeof(Projectile), "DistanceCoveredFraction").CreateDelegate<Func<Projectile, float>>();

    private static readonly HashSet<QuestScriptDef> deserterQuests = new();

    static Utilities()
    {
        foreach (var def in DefDatabase<QuestScriptDef>.AllDefs)
            if (def.HasModExtension<QuestExtension_Deserter>())
                deserterQuests.Add(def);
    }

    public static IEnumerable<QuestScriptDef> DeserterQuests => deserterQuests;

    public static bool IsDeserterQuest(this QuestScriptDef def) => deserterQuests.Contains(def);

    public static int Wrap(int value, int min, int max)
    {
        if (min == max) return min;
        while (value < min) value = max - (min - value);
        while (value > max) value = min + (value - max);
        return value;
    }

    public static void Cycle(this ref int value, int min, int max) => value = Wrap(value + 1, min, max);
    public static void Cycle(this ref int value, Array arr) => Cycle(ref value, 0, arr.Length - 1);

    public static void DoAerodroneStrike(this IntVec3 cell, Map map)
    {
        (MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, VFED_DefOf.VFED_Mote_AerodroneStrike, 1f, true) as Mote_Strike)?.LaunchStrike(
            VFED_DefOf.VFED_AerodroneStrikeIncoming, 10);
    }

    public static Vector3 ActualDrawPos(this Thing thing) =>
        thing is Projectile proj
            ? proj.DrawPos + new Vector3(0f, 0f, 1f) * (getArcHeightFactor(proj) * GenMath.InverseParabola(getDistanceCoveredFraction(proj)))
            : thing.DrawPos;

    public static bool Contains(this IntRange range, int count) => count >= range.TrueMin && count <= range.TrueMax;

    public static int TotalIntelCost(this ContrabandExtension ext) =>
        Mathf.FloorToInt(ext.intelCost * WorldComponent_Deserters.Instance.VisibilityLevel.intelCostModifier);

    public static FloatRange ReceiveTimeRange(int totalAmount) =>
        new(
            totalAmount * 0.125f * WorldComponent_Deserters.Instance.VisibilityLevel.contrabandTimeToReceiveModifier,
            totalAmount * 0.25f * WorldComponent_Deserters.Instance.VisibilityLevel.contrabandTimeToReceiveModifier);

    public static float SiteExistTime(int totalAmount) =>
        (totalAmount >= 25 ? -Mathf.Log(totalAmount - 24) + 5 : 30 - totalAmount)
      * WorldComponent_Deserters.Instance.VisibilityLevel.contrabandSiteTimeActiveModifier;

    public static int DaysToTicks(this float days) => Mathf.RoundToInt(days * GenDate.TicksPerDay);

    public static IEnumerable<T> LogInline<T>(this IEnumerable<T> source, Func<T, string> toString = null)
    {
        foreach (var item in source)
        {
            Log.Message(toString == null ? item.ToString() : toString(item));
            yield return item;
        }
    }

    public static Pawn GetLeader(this Caravan caravan) =>
        IdeoUtility.FindFirstPawnWithLeaderRole(caravan) ?? BestCaravanPawnUtility.FindBestNegotiator(caravan) ?? BestCaravanPawnUtility
           .FindBestDiplomat(caravan) ?? caravan.PawnsListForReading.Find(caravan.IsOwner);

    public static void ChangeVisibility(int by)
    {
        WorldComponent_Deserters.Instance.Visibility += by;
        WorldComponent_Deserters.Instance.Notify_VisibilityChanged();
    }

    public static LookTargets MakeLookTargets(this List<IntVec3> cells, Map map)
    {
        return new LookTargets(cells.Select(cell => new GlobalTargetInfo(cell, map)));
    }

    public static void Schedule(Action action, int tick)
    {
        WorldComponent_Deserters.Instance.EventQueue.Enqueue(action, tick);
    }

    public static string Serialize(this Action action) => $"{action.Method.Name}.{action.Method.DeclaringType.AssemblyQualifiedName}";

    public static Action DeserializeAction(this string str)
    {
        var array = str.Split('.');

        var method = array[0];
        var type = array.Skip(1).Join(delimiter: ".");
        return Type.GetType(type)?.GetMethod(method, AccessTools.all).CreateDelegate<Action>();
    }

    public static List<(T1, T2)> ToList<T1, T2>(this PriorityQueue<T1, T2> source)
    {
        var result = new List<(T1, T2)>();

        while (source.TryDequeue(out var item1, out var item2)) result.Add((item1, item2));

        foreach (var (item1, item2) in result) source.Enqueue(item1, item2);

        return result;
    }

    public static void Split<T>(this IEnumerable<T> source, out List<T> truthy, out List<T> falsy, Func<T, bool> func)
    {
        truthy = new List<T>();
        falsy = new List<T>();
        foreach (var t in source)
            if (func(t))
                truthy.Add(t);
            else falsy.Add(t);
    }

    public static void GetIntelCost(this Quest quest, out int normal, out int critical)
    {
        var choice = quest.PartsListForReading.OfType<QuestPart_Choice>().First();
        var averageValue = choice.choices.Sum(option => option.rewards.Sum(reward => reward.TotalMarketValue)) / choice.choices.Count;
        if (quest.challengeRating > 3)
        {
            normal = 0;
            critical = Mathf.CeilToInt(averageValue / 2000 * WorldComponent_Deserters.Instance.VisibilityLevel.intelCostModifier);
        }
        else
        {
            normal = Mathf.CeilToInt(averageValue / 500 * WorldComponent_Deserters.Instance.VisibilityLevel.intelCostModifier);
            critical = 0;
        }
    }

    public static void IncAlt(this ref Vector3 pos, float frac)
    {
        pos += Altitudes.AltIncVect * frac;
    }

    public static IEnumerable<Thing> AllThings(this CellRect rect, Map map) => rect.SelectMany(c => c.GetThingList(map)).Distinct();

    public static T TakeRandom<T>(this List<T> source)
    {
        var idx = Rand.Range(0, source.Count);
        var item = source[idx];
        source.RemoveAt(idx);
        return item;
    }

    public static float Radius(this CellRect rect) => Mathf.Sqrt(Mathf.Pow(rect.Width, 2) + Mathf.Pow(rect.Height, 2));
}
