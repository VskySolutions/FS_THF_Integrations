using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application;

/// <summary>
/// Composition-root entry point for the Application layer. Host projects
/// (Api, Workers, McpServer) call <see cref="AddApplication"/> to register
/// the customer-management, access, email, option-set, and universal-feature services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Customer Management workflow services.
        services.AddScoped<Abstractions.Customers.ICustomerApprovalService, Customers.CustomerApprovalService>();
        services.AddScoped<Abstractions.Customers.ICustomerDuplicateChecker, Customers.CustomerDuplicateChecker>();

        // Permission Groups: effective-permission cache computation.
        services.AddScoped<Abstractions.Security.IPermissionGroupEffectivePermissionService, Security.PermissionGroupEffectivePermissionService>();

        // SMTP Email Accounts: account management business logic.
        services.AddScoped<Abstractions.Email.ISmtpAccountService, Email.SmtpAccountService>();

        // Email templates: management + rendering.
        services.AddScoped<Abstractions.Email.IEmailTemplateService, Email.EmailTemplateService>();

        // Option Sets: tenant-configurable input value lists.
        services.AddScoped<Abstractions.OptionSets.IOptionSetService, OptionSets.OptionSetService>();

        // Universal Features (Phase 14): cross-cutting activity writer + notification dispatcher.
        services.AddScoped<Abstractions.UniversalFeatures.IActivityEventWriter, UniversalFeatures.ActivityEventWriter>();
        services.AddScoped<Abstractions.UniversalFeatures.INotificationDispatcher, UniversalFeatures.NotificationDispatcher>();

        return services;
    }
}
