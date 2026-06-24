using FluentAssertions;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Email;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Moq;

namespace IntegrationHub.UnitTests;

public class EmailTemplateServiceTests
{
    private readonly Mock<IEmailTemplateRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EmailTemplateService Create() => new(_repo.Object, _unitOfWork.Object);

    private static IReadOnlyDictionary<string, string?> Model(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in pairs)
        {
            dict[k] = v;
        }
        return dict;
    }

    private static EmailTemplate Row(Guid? tenantId, EmailTemplateKey key, string subject, string body) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TemplateKey = key,
        Subject = subject,
        Body = body,
    };

    [Fact]
    public void Render_substitutes_placeholders_case_insensitively_and_leaves_unknowns()
    {
        var result = Create().Render(
            "Hi {{FullName}}",
            "{{ fullname }} <{{Email}}> — {{Unknown}}",
            Model(("FullName", "Jane Doe"), ("Email", "jane@x.com")));

        result.Subject.Should().Be("Hi Jane Doe");
        result.Body.Should().Be("Jane Doe <jane@x.com> — {{Unknown}}");
    }

    [Fact]
    public async Task ListAsync_returns_all_keys_with_effective_content_and_override_flag()
    {
        var tenant = Guid.NewGuid();
        _repo.Setup(r => r.ListForScopeAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Row(tenant, EmailTemplateKey.UserInvitation, "Tenant subject", "Tenant body"), // override
            Row(null, EmailTemplateKey.Welcome, "Global welcome", "Global body"),           // global default row
        });

        var list = await Create().ListAsync(tenant, default);

        list.Should().HaveCount(DefaultEmailTemplates.All.Count);

        var invitation = list.Single(d => d.Key == nameof(EmailTemplateKey.UserInvitation));
        invitation.IsOverridden.Should().BeTrue();
        invitation.Subject.Should().Be("Tenant subject");

        var welcome = list.Single(d => d.Key == nameof(EmailTemplateKey.Welcome));
        welcome.IsOverridden.Should().BeFalse();
        welcome.Subject.Should().Be("Global welcome");

        // No row at all → falls back to the built-in default content.
        var reset = list.Single(d => d.Key == nameof(EmailTemplateKey.PasswordReset));
        reset.IsOverridden.Should().BeFalse();
        reset.Subject.Should().Be(DefaultEmailTemplates.For(EmailTemplateKey.PasswordReset).Subject);
    }

    [Fact]
    public async Task SaveAsync_adds_a_new_row_when_none_exists()
    {
        var tenant = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(tenant, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync((EmailTemplate?)null);

        await Create().SaveAsync(tenant, EmailTemplateKey.Welcome, "New", "Body", default);

        _repo.Verify(r => r.AddAsync(It.Is<EmailTemplate>(t => t.TenantId == tenant && t.TemplateKey == EmailTemplateKey.Welcome && t.Subject == "New"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_updates_the_existing_row()
    {
        var tenant = Guid.NewGuid();
        var existing = Row(tenant, EmailTemplateKey.Welcome, "Old", "Old body");
        _repo.Setup(r => r.GetAsync(tenant, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Create().SaveAsync(tenant, EmailTemplateKey.Welcome, "Updated", "Updated body", default);

        existing.Subject.Should().Be("Updated");
        existing.Body.Should().Be("Updated body");
        _repo.Verify(r => r.Update(existing), Times.Once);
        _repo.Verify(r => r.AddAsync(It.IsAny<EmailTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetAsync_removes_a_tenant_override()
    {
        var tenant = Guid.NewGuid();
        var existing = Row(tenant, EmailTemplateKey.Welcome, "Custom", "Custom body");
        _repo.Setup(r => r.GetAsync(tenant, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await Create().ResetAsync(tenant, EmailTemplateKey.Welcome, default);

        result.Should().BeTrue();
        _repo.Verify(r => r.Remove(existing), Times.Once);
    }

    [Fact]
    public async Task ResetAsync_returns_false_when_no_tenant_override_exists()
    {
        var tenant = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(tenant, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync((EmailTemplate?)null);

        (await Create().ResetAsync(tenant, EmailTemplateKey.Welcome, default)).Should().BeFalse();
        _repo.Verify(r => r.Remove(It.IsAny<EmailTemplate>()), Times.Never);
    }

    [Fact]
    public async Task ResetAsync_restores_built_in_default_content_for_the_global_scope()
    {
        var global = Row(null, EmailTemplateKey.Welcome, "Edited global", "Edited body");
        _repo.Setup(r => r.GetAsync(null, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync(global);

        var result = await Create().ResetAsync(null, EmailTemplateKey.Welcome, default);

        result.Should().BeTrue();
        var def = DefaultEmailTemplates.For(EmailTemplateKey.Welcome);
        global.Subject.Should().Be(def.Subject);
        global.Body.Should().Be(def.Body);
        _repo.Verify(r => r.Remove(It.IsAny<EmailTemplate>()), Times.Never);
    }

    [Fact]
    public async Task RenderEffectiveAsync_uses_the_effective_template()
    {
        var tenant = Guid.NewGuid();
        _repo.Setup(r => r.GetEffectiveAsync(tenant, EmailTemplateKey.UserInvitation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(tenant, EmailTemplateKey.UserInvitation, "Hi {{FullName}}", "Body {{FullName}}"));

        var rendered = await Create().RenderEffectiveAsync(tenant, EmailTemplateKey.UserInvitation, Model(("FullName", "Sam")), default);

        rendered!.Subject.Should().Be("Hi Sam");
        rendered.Body.Should().Be("Body Sam");
    }
}
