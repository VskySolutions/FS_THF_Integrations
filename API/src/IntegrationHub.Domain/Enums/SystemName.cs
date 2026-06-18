namespace IntegrationHub.Domain.Enums;

/// <summary>
/// External systems that the IntegrationHub platform integrates with.
/// </summary>
public enum SystemName
{
    /// <summary>Paycor HR / payroll system.</summary>
    Paycor = 0,

    /// <summary>SAP Concur expense and invoice system.</summary>
    Concur = 1,

    /// <summary>Deltek Maconomy ERP system.</summary>
    Maconomy = 2,

    /// <summary>The IntegrationHub platform itself — the source for internally-originated flows (e.g. Customer onboarding).</summary>
    Platform = 3
}
