namespace DontBeLate.Tests.Stages;

using DontBeLate.Stages;
using GdUnit4;
using ReusableGameSystems.Clock;
using static GdUnit4.Assertions;

[TestSuite]
public class BusTimetableTest
{
    private static BusTimetable Make() => new(
        new[] { ClockTime.Parse("8:10"), ClockTime.Parse("7:45"), ClockTime.Parse("8:00") },
        approachMinutes: 0.5,
        dwellMinutes: 1,
        departMinutes: 0.5,
        rideMinutes: 50,
        workStarts: ClockTime.Parse("9:00"));

    [TestCase]
    public void SortsArrivals()
    {
        var timetable = Make();

        AssertString(timetable.Arrivals[0].ToString()).IsEqual("07:45");
        AssertString(timetable.Arrivals[2].ToString()).IsEqual("08:10");
    }

    [TestCase]
    public void ArrivalAtWorkAndLastOnTimeBus()
    {
        var timetable = Make();

        AssertString(timetable.ArrivalAtWork(0).ToString()).IsEqual("08:36");
        AssertBool(timetable.IsOnTime(1)).IsTrue();
        AssertBool(timetable.IsOnTime(2)).IsFalse();
        AssertInt(timetable.LastOnTimeBus).IsEqual(1);
    }

    [TestCase]
    public void PhasesThroughOneVisit()
    {
        var timetable = Make();

        AssertThat(timetable.PhaseAt(ClockTime.Parse("7:40"), out int bus, out _)).IsEqual(BusPhase.Away);
        AssertInt(bus).IsEqual(-1);

        AssertThat(timetable.PhaseAt(ClockTime.FromHms(7, 44, 45), out bus, out double progress)).IsEqual(BusPhase.Approaching);
        AssertInt(bus).IsEqual(0);
        AssertFloat((float)progress).IsEqualApprox(0.5f, 0.001f);

        AssertThat(timetable.PhaseAt(ClockTime.FromHms(7, 45, 30), out bus, out _)).IsEqual(BusPhase.AtStop);
        AssertThat(timetable.PhaseAt(ClockTime.FromHms(7, 46, 10), out bus, out _)).IsEqual(BusPhase.Departing);
        AssertThat(timetable.PhaseAt(ClockTime.Parse("7:47"), out bus, out _)).IsEqual(BusPhase.Away);
    }

    [TestCase]
    public void NextBusSkipsTheOnesThatLeft()
    {
        var timetable = Make();

        AssertInt(timetable.NextBus(ClockTime.Parse("7:30"))).IsEqual(0);
        AssertInt(timetable.NextBus(ClockTime.FromHms(7, 45, 30))).IsEqual(0);
        AssertInt(timetable.NextBus(ClockTime.Parse("7:46"))).IsEqual(1);
        AssertInt(timetable.NextBus(ClockTime.Parse("8:20"))).IsEqual(-1);
    }
}
