using AsmResolver.PE;
using Xunit;

namespace AsmResolver.DotNet.Tests;

public class TargetRuntimeProberTest
{
    [Fact]
    public void DetectTargetNetFramework40()
    {
        var image = PEImage.FromBytes(Properties.Resources.HelloWorld, TestReaderParameters.PEReaderParameters);
        var targetRuntime = TargetRuntimeProber.GetLikelyTargetRuntime(image);

        Assert.True(targetRuntime.IsNetFramework);
        Assert.Contains(DotNetRuntimeInfo.NetFrameworkName, targetRuntime.Name);
        Assert.Equal(4, targetRuntime.Version.Major);
        Assert.Equal(0, targetRuntime.Version.Minor);
    }

    [Fact]
    public void DetectTargetNetCore()
    {
        var image = PEImage.FromBytes(Properties.Resources.HelloWorld_NetCore, TestReaderParameters.PEReaderParameters);
        var targetRuntime = TargetRuntimeProber.GetLikelyTargetRuntime(image);

        Assert.True(targetRuntime.IsNetCoreApp);
        Assert.Contains(DotNetRuntimeInfo.NetCoreAppName, targetRuntime.Name);
        Assert.Equal(2, targetRuntime.Version.Major);
        Assert.Equal(2, targetRuntime.Version.Minor);
    }

    [Fact]
    public void DetectTargetStandard()
    {
        var image = PEImage.FromFile(typeof(TestCases.Types.Class).Assembly.Location, TestReaderParameters.PEReaderParameters);
        var targetRuntime = TargetRuntimeProber.GetLikelyTargetRuntime(image);

        Assert.True(targetRuntime.IsNetStandard);
        Assert.Contains(DotNetRuntimeInfo.NetStandardName, targetRuntime.Name);
        Assert.Equal(2, targetRuntime.Version.Major);
    }
}
