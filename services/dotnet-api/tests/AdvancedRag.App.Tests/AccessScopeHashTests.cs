using AdvancedRag.App.Auth;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class AccessScopeHashTests
{
    [Fact]
    public void AccessScopeHash_V2_IncludesRoleAdminFlagOrgUnitGroupsAndVersion()
    {
        Guid organizationalUnitId = Guid.Parse("01000000-0000-0000-0000-000000000002");
        Guid firstGroupId = Guid.Parse("03000000-0000-0000-0000-000000000002");
        Guid secondGroupId = Guid.Parse("03000000-0000-0000-0000-000000000001");

        string hash = AccessScopeHash.ComputeV2(
            "DocumentPublisher",
            isGlobalAdmin: false,
            organizationalUnitId,
            [firstGroupId, secondGroupId],
            accessScopeVersion: 42);

        hash.Should().Be("fad62356413c3965f2c50a38a6b4125d8c7990b3efd0928179c995cf7350c69e");
    }
}
