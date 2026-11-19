using System.Threading.Tasks;
using ResourceAnalyzer.Helpers;
using Xunit;

namespace ResourceAnalyzer.Tests
{
    public class PackageHelperTests
    {
        [Fact]
        public void IsStorePackage_WhenRunningInStandardTestHost_ReturnsFalse()
        {
            bool isPackaged = PackageHelper.IsStorePackage();
            Assert.False(isPackaged);
        }

        [Fact]
        public void IsStartupTaskActivated_WhenUnpackaged_ReturnsFalse()
        {
            bool isActivated = PackageHelper.IsStartupTaskActivated();
            Assert.False(isActivated);
        }

        [Fact]
        public async Task GetPackagedStartupTaskStateAsync_WhenUnpackaged_ReturnsNull()
        {
            bool? state = await PackageHelper.GetPackagedStartupTaskStateAsync();
            Assert.Null(state);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SetPackagedStartupTaskStateAsync_WhenUnpackaged_ReturnsNotPackaged(bool enable)
        {
            var result = await PackageHelper.SetPackagedStartupTaskStateAsync(enable);
            Assert.Equal(PackageHelper.StartupTaskResult.NotPackaged, result);
        }
    }
}
