namespace DontBeLate.Tests.City;

using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using ReusableGameSystems.Citizens;
using ReusableGameSystems.CityGen;
using static GdUnit4.Assertions;

/// <summary>The town's people (content/citizens) in the game's city: everyone has somewhere to be, and the morning looks like a morning.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CitizensTest
{
    private const string StylePath = "res://content/city/city_style.tres";
    private const string SettingsPath = "res://content/citizens/citizens.tres";

    private static CitizenSim Town(ulong seed)
    {
        var layout = CityGenerator.Generate(GD.Load<CityStyle>(StylePath), seed);
        return new CitizenSim(layout, GD.Load<CitizenSettings>(SettingsPath), seed);
    }

    private static void RunTo(CitizenSim sim, double until)
    {
        for (double t = sim.Time; t < until; t += 15)
        {
            sim.Step(t, null);
        }
    }

    private static double At(int day, float hour) => (day * CitizenSim.DaySeconds) + (hour * 3600);

    [TestCase]
    public void EveryoneHasAHomeAndWorkersHaveAJob()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var sim = Town(seed);
            AssertInt(sim.Citizens.Count).IsEqual(400);
            foreach (var citizen in sim.Citizens)
            {
                AssertObject(citizen.Home).OverrideFailureMessage($"{citizen} has no home in city {seed}").IsNotNull();
                if (citizen.Archetype.DayRole.Length > 0)
                {
                    AssertObject(citizen.DayPlace).OverrideFailureMessage($"{citizen} has nowhere to go in city {seed}").IsNotNull();
                }
            }

            // Every kind of place the content knows is in the town (the beach only if there's a coast).
            foreach (var id in new[] { "home", "cafe", "pharmacy", "barbershop", "school", "hospital", "stadium", "mall", "bank", "park" })
            {
                AssertBool(sim.Places.Any(p => p.Type.Id == id)).OverrideFailureMessage($"no {id} place in city {seed}").IsTrue();
            }

            AssertBool(sim.Places.Any(p => p.Type.Id == "beach") == (sim.Layout.Surroundings?.HasCoast ?? false)).IsTrue();
        }
    }

    [TestCase]
    public void TheMorningRushHasPeopleOnTheWayToWorkAndSchool()
    {
        var sim = Town(3);
        sim.Start(At(0, 7.5f));
        int arrivedAtWork = 0;
        int mostOnTheWay = 0;
        var drove = new HashSet<Citizen>();
        sim.Arrived += (citizen, place) => arrivedAtWork += place == citizen.DayPlace ? 1 : 0;
        for (double t = sim.Time; t < At(0, 8.5f); t += 15)
        {
            sim.Step(t, null);
            mostOnTheWay = System.Math.Max(mostOnTheWay, sim.Citizens.Count(c => c.Status == CitizenStatus.Travelling));
            foreach (var citizen in sim.Citizens)
            {
                if (citizen.Trip?.Mode == TravelMode.Drive)
                {
                    drove.Add(citizen);
                }
            }
        }

        // Car owners with a longer commute drive it.
        var owners = sim.Citizens.Where(c => c.HasCar && c.DayPlace != null).Select(c => c.Home!.Position.DistanceTo(c.DayPlace!.Position)).OrderBy(d => d).ToList();
        AssertInt(drove.Count).OverrideFailureMessage($"{drove.Count} drove; {sim.Citizens.Count(c => c.HasCar)} own cars; commutes median {owners.ElementAtOrDefault(owners.Count / 2):0} max {owners.LastOrDefault():0}; town {sim.Layout.Bounds.Size}; kerbs {sim.Places.Count(p => p.Kerb.IsValid)}/{sim.Places.Count}").IsGreater(20);
        AssertBool(drove.All(c => c.HasCar)).IsTrue();

        // Trips across town take minutes, so the rush is a stream of people rather than everyone at once.
        AssertInt(arrivedAtWork).OverrideFailureMessage($"{arrivedAtWork} got to work or school by 8:30").IsGreater(150);
        AssertInt(mostOnTheWay).IsGreater(15);

        RunTo(sim, At(0, 10));
        var students = sim.Citizens.Where(c => c.Archetype.Id == "student").ToList();
        AssertInt(students.Count(c => c.Place == c.DayPlace && c.Status == CitizenStatus.AtPlace) * 100 / students.Count).IsGreaterEqual(85);
    }

    [TestCase]
    public void FansFillTheStadiumOnSaturday()
    {
        var sim = Town(3);
        sim.Start(At(5, 13));
        RunTo(sim, At(5, 15.5f));
        int atMatch = sim.Citizens.Count(c => c.Plan?.Event?.Id == "home_match" && c.Status == CitizenStatus.AtPlace);
        AssertInt(atMatch).IsGreater(50);
        AssertBool(sim.Citizens.Where(c => c.Plan?.Event != null).All(c => c.HasAnyTag(new[] { "fan:*" }))).IsTrue();
    }
}
