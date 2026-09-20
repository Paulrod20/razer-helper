using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class DgpuTextTests
{
    private static DgpuApp App(string name, long megabytes, int pid) =>
        new(pid, name, megabytes * 1024 * 1024, DgpuAppVerdict.Close);

    [Fact]
    public void Confirmation_ListsEveryAppItWillAskToClose_AndSaysTheyCanAskToSave()
    {
        var text = DgpuText.BuildConfirmation([App("blender", 800, 1), App("msedge", 150, 2)]);

        Assert.Contains("blender (800 MB)", text);
        Assert.Contains("msedge (150 MB)", text);
        Assert.Contains("save your work", text);
    }

    [Fact]
    public void Confirmation_ShowsSeveralInstancesOfOneAppOnce()
    {
        var text = DgpuText.BuildConfirmation([App("msedge", 100, 1), App("msedge", 60, 2)]);

        Assert.Contains("msedge (2 instances, 160 MB)", text);
    }

    [Theory]
    [InlineData((int)DgpuFreeUpOutcome.NoDedicatedGpu, "No dedicated GPU")]
    [InlineData((int)DgpuFreeUpOutcome.ExternalDisplay, "external display is connected")]
    [InlineData((int)DgpuFreeUpOutcome.NothingToClose, "No apps that can be closed")]
    [InlineData((int)DgpuFreeUpOutcome.ConditionsChanged, "nothing was closed")]
    [InlineData((int)DgpuFreeUpOutcome.Busy, "Already checking")]
    [InlineData((int)DgpuFreeUpOutcome.Failed, "log")]
    public void EveryOutcomeThatClosedNothing_HasAnExplanation(int outcome, string expected) =>
        Assert.Contains(expected, DgpuText.DescribeOutcome((DgpuFreeUpOutcome)outcome));

    [Theory]
    [InlineData((int)DgpuFreeUpOutcome.Closed)]
    [InlineData((int)DgpuFreeUpOutcome.Declined)]
    public void OutcomesThatSpeakForThemselves_AreNotExplained(int outcome) =>
        Assert.Null(DgpuText.DescribeOutcome((DgpuFreeUpOutcome)outcome));

    [Fact]
    public void ExternalDisplayMessage_TellsTheUserWhatToDo() =>
        Assert.Contains("Disconnect it and try again", DgpuText.DescribeOutcome(DgpuFreeUpOutcome.ExternalDisplay));
}
