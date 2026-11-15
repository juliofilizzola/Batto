using Windows.Devices.Enumeration;
using Xunit;

namespace Batto.Tests;

public class DeviceWatcherSyntaxTest
{
    [Theory]
    [InlineData("System.ItemNameDisplay")]
    [InlineData("System.Devices.Aep.DeviceAddress")]
    [InlineData("System.Devices.Aep.IsConnected")]
    [InlineData("{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2")]
    [InlineData("{83DA6326-97A6-4088-9453-A1923F573B29} 15")]
    public void TestIndividualProperties(string prop)
    {
        string aqsFilter = "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cdb4-4960-bb44-241b0292fa17}\" OR " +
                           "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\") AND " +
                           "System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";

        var watcher = DeviceInformation.CreateWatcher(
            aqsFilter,
            new[] { prop },
            DeviceInformationKind.AssociationEndpoint);

        Assert.NotNull(watcher);
    }
}
