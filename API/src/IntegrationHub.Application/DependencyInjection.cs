using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Concur;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application;

/// <summary>
/// Composition-root entry point for the Application layer. Host projects
/// (Api, Workers, McpServer) call <see cref="AddApplication"/> to register
/// MediatR handlers, integration flow services, transformers, and validators.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR command/handler pipeline for the integration flows.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Concur → Maconomy import flow services.
        services.AddScoped<ExpenseImportIntegrationService>();
        services.AddScoped<VendorInvoiceImportIntegrationService>();
        services.AddScoped<VendorPaymentImportIntegrationService>();

        // Transformers, keyed by (source, destination) system pair.
        services.AddTransformer<Abstractions.Connectors.Concur.ConcurExpenseReport, Abstractions.Connectors.Maconomy.MaconomyExpenseReport, ConcurExpenseTransformer>(SystemName.Concur, SystemName.Maconomy);
        services.AddScoped<ConcurExpenseTransformer>();
        services.AddScoped<ConcurInvoiceTransformer>();
        services.AddScoped<ConcurPaymentTransformer>();

        // Domain validators.
        services.AddScoped<ExpenseValidator>();
        services.AddScoped<InvoiceValidator>();
        services.AddScoped<PaymentValidator>();

        // Customer Management workflow services.
        services.AddScoped<Abstractions.Customers.ICustomerApprovalService, Customers.CustomerApprovalService>();
        services.AddScoped<Abstractions.Customers.ICustomerDuplicateChecker, Customers.CustomerDuplicateChecker>();
        // Customer → Maconomy field mapping (applies the tenant's CustomerSync flow mapping rules).
        services.AddScoped<Customers.ICustomerMaconomyMapper, Customers.CustomerMaconomyMapper>();

        // Permission Groups: effective-permission cache computation.
        services.AddScoped<Abstractions.Security.IPermissionGroupEffectivePermissionService, Security.PermissionGroupEffectivePermissionService>();

        return services;
    }
}
