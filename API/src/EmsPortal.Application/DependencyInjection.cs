using Microsoft.Extensions.DependencyInjection;

namespace EmsPortal.Application;

/// <summary>
/// Composition-root entry point for the Application layer. Host projects
/// (Api, Workers, McpServer) call <see cref="AddApplication"/> to register
/// the access, email, option-set, and universal-feature services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Permission Groups: effective-permission cache computation.
        services.AddScoped<Abstractions.Security.IPermissionGroupEffectivePermissionService, Security.PermissionGroupEffectivePermissionService>();

        // SMTP Email Accounts: account management business logic.
        services.AddScoped<Abstractions.Email.ISmtpAccountService, Email.SmtpAccountService>();

        // Email templates: management + rendering.
        services.AddScoped<Abstractions.Email.IEmailTemplateService, Email.EmailTemplateService>();

        // REMS external emails (WO-124): typed background dispatch for WO-112/113.
        services.AddScoped<Abstractions.Email.IRemsEmailNotifier, Email.RemsEmailNotifier>();

        // Swallowed delivery failures surface as a Failed event on the REMS form's email log.
        services.AddScoped<Abstractions.Email.IEmailDeliveryFailureSink, Email.RemsEmailDeliveryFailureSink>();

        // Option Sets: tenant-configurable input value lists.
        services.AddScoped<Abstractions.OptionSets.IOptionSetService, OptionSets.OptionSetService>();

        // Universal Features (Phase 14): cross-cutting activity writer + notification dispatcher.
        services.AddScoped<Abstractions.UniversalFeatures.IActivityEventWriter, UniversalFeatures.ActivityEventWriter>();
        services.AddScoped<Abstractions.UniversalFeatures.INotificationDispatcher, UniversalFeatures.NotificationDispatcher>();

        return services;
    }
}
