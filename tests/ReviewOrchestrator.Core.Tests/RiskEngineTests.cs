using ReviewOrchestrator.Core.Domain;
using ReviewOrchestrator.Core.Engines;
using static ReviewOrchestrator.Core.Tests.TestData;

namespace ReviewOrchestrator.Core.Tests;

public class RiskEngineTests
{
    private readonly RiskEngine _engine = new();

    [Theory]
    [InlineData("src/Auth/TokenService.cs")]
    [InlineData("src/security/KeyProvider.cs")]
    [InlineData("api/permissions/RoleCheck.cs")]
    [InlineData("auth/login.ts")]
    public void Auth_file_is_high_risk(string path)
    {
        var result = _engine.Assess(Pr(files: [File(path)]));

        Assert.Equal(ReviewRisk.High, result.Level);
        Assert.Contains(result.Reasons, r => r.Contains("security-sensitive"));
    }

    [Theory]
    [InlineData("security")]
    [InlineData("auth")]
    [InlineData("permissions")]
    [InlineData("infrastructure")]
    [InlineData("migration")]
    public void High_risk_label_is_high_risk(string label)
    {
        var result = _engine.Assess(Pr(labels: [label], files: [File("README.md")]));
        Assert.Equal(ReviewRisk.High, result.Level);
    }

    [Fact]
    public void Migration_file_is_high_risk()
    {
        var result = _engine.Assess(Pr(files: [File("src/Persistence/Migrations/20261001_AddTenant.cs", 60)]));

        Assert.Equal(ReviewRisk.High, result.Level);
        Assert.Contains("database", result.RiskAreas);
    }

    [Fact]
    public void Terraform_file_is_high_risk()
    {
        var result = _engine.Assess(Pr(files: [File("deploy/terraform/main.tf")]));
        Assert.Equal(ReviewRisk.High, result.Level);
    }

    [Fact]
    public void Security_sensitive_flag_is_high_risk()
    {
        var result = _engine.Assess(Pr(files: [File("docs/readme.md")], configure: p => p.IsSecuritySensitive = true));
        Assert.Equal(ReviewRisk.High, result.Level);
    }

    [Fact]
    public void Persistence_change_is_medium_risk()
    {
        var result = _engine.Assess(Pr(files: [File("src/Persistence/ThreadRepository.cs", 30, 10)]));

        Assert.Equal(ReviewRisk.Medium, result.Level);
        Assert.Contains(result.Reasons, r => r.Contains("persistence"));
        Assert.Contains("Data Persistence", result.AreasTouched);
    }

    [Fact]
    public void Api_contract_change_is_medium_risk()
    {
        var result = _engine.Assess(Pr(files: [File("web/src/contracts/SkillResponseDto.ts", 5)]));
        Assert.Equal(ReviewRisk.Medium, result.Level);
    }

    [Fact]
    public void Many_production_files_is_medium_risk()
    {
        var files = Enumerable.Range(1, 11).Select(i => File($"web/src/components/C{i}.tsx", 5));
        Assert.Equal(ReviewRisk.Medium, _engine.Assess(Pr(files: files)).Level);
    }

    [Fact]
    public void Tests_only_pr_is_low_risk()
    {
        var result = _engine.Assess(Pr(files:
        [
            File("tests/PendingSkillCoordinatorTests.cs", 120),
            File("tests/Fakes/FakeClock.cs", 20),
        ]));

        Assert.Equal(ReviewRisk.Low, result.Level);
        Assert.Contains("Tests / generated files only", result.Reasons);
    }

    [Fact]
    public void Tests_under_auth_folders_are_not_security_sensitive()
    {
        var result = _engine.Assess(Pr(files: [File("tests/Auth/TokenServiceTests.cs", 50)]));
        Assert.Equal(ReviewRisk.Low, result.Level);
    }

    [Fact]
    public void Docs_only_pr_is_low_risk_even_with_api_label()
    {
        var result = _engine.Assess(Pr(labels: ["docs", "api"], files: [File("docs/api/skills.md", 60)]));
        Assert.Equal(ReviewRisk.Low, result.Level);
    }

    [Fact]
    public void Telemetry_only_pr_is_low_risk()
    {
        var result = _engine.Assess(Pr(files: [File("src/Telemetry/SkillEvents.cs", 40), File("tests/Telemetry/SkillEventsTests.cs", 20)]));
        Assert.Equal(ReviewRisk.Low, result.Level);
    }

    [Fact]
    public void Reassuring_signals_are_reported_when_no_auth_changes()
    {
        var result = _engine.Assess(Pr(files: [File("src/Persistence/ThreadRepository.cs")]));

        Assert.Contains(result.Signals, s => !s.IsWarning && s.Text == "No authentication changes");
        Assert.Contains(result.Signals, s => s.IsWarning && s.Text == "Requires component-owner review");
    }
}
