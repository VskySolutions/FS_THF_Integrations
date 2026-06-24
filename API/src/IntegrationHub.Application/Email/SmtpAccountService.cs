using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Email;

/// <summary>
/// Business logic for SMTP account management (SMTP Email Accounts). Encrypts passwords via
/// <see cref="ICredentialEncryptionService"/>, applies the auto-activate-first rule, performs the
/// atomic active-account swap (ADR-003), guards deletion of the active account, and runs
/// credential-decrypting test sends through <see cref="ISmtpEmailSender"/>.
/// </summary>
public sealed class SmtpAccountService : ISmtpAccountService
{
    private readonly ISmtpAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IAuditTrailService _audit;
    private readonly ISmtpEmailSender _sender;

    public SmtpAccountService(
        ISmtpAccountRepository accounts,
        IUnitOfWork unitOfWork,
        ICredentialEncryptionService encryption,
        IAuditTrailService audit,
        ISmtpEmailSender sender)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _encryption = encryption;
        _audit = audit;
        _sender = sender;
    }

    public async Task<SmtpAccount> CreateAsync(CreateSmtpAccountInput input, CancellationToken cancellationToken = default)
    {
        var name = input.AccountName.Trim();
        if (await _accounts.NameExistsAsync(input.TenantId, name, excludeId: null, cancellationToken))
        {
            throw new SmtpAccountException(SmtpAccountErrorCodes.DuplicateName, $"An account named '{name}' already exists for this tenant.");
        }

        // The first account for a tenant becomes the active one automatically (AC-SMTP-002.5).
        var isFirst = await _accounts.CountByTenantAsync(input.TenantId, cancellationToken) == 0;

        var account = new SmtpAccount
        {
            Id = Guid.NewGuid(),
            TenantId = input.TenantId,
            AccountName = name,
            Host = input.Host.Trim(),
            Port = input.Port,
            EncryptionType = input.EncryptionType,
            AuthType = input.AuthType,
            Username = string.IsNullOrWhiteSpace(input.Username) ? null : input.Username.Trim(),
            EncryptedPassword = EncryptOrNull(input.Password),
            FromName = input.FromName.Trim(),
            FromEmail = input.FromEmail.Trim(),
            IsActive = isFirst,
        };

        await _accounts.AddAsync(account, cancellationToken);
        await _audit.AddAsync(nameof(SmtpAccount), account.Id.ToString(), "SmtpAccountCreated",
            details: $"name={account.AccountName}; host={account.Host}:{account.Port}; active={account.IsActive}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return account;
    }

    public async Task<SmtpAccount?> UpdateAsync(Guid id, Guid tenantId, UpdateSmtpAccountInput input, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, tenantId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var name = input.AccountName.Trim();
        if (await _accounts.NameExistsAsync(tenantId, name, excludeId: id, cancellationToken))
        {
            throw new SmtpAccountException(SmtpAccountErrorCodes.DuplicateName, $"An account named '{name}' already exists for this tenant.");
        }

        account.AccountName = name;
        account.Host = input.Host.Trim();
        account.Port = input.Port;
        account.EncryptionType = input.EncryptionType;
        account.AuthType = input.AuthType;
        account.Username = string.IsNullOrWhiteSpace(input.Username) ? null : input.Username.Trim();
        account.FromName = input.FromName.Trim();
        account.FromEmail = input.FromEmail.Trim();
        // Preserve the existing password when none is supplied; re-encrypt when one is (AC-SMTP-003.1/003.2).
        if (!string.IsNullOrEmpty(input.Password))
        {
            account.EncryptedPassword = _encryption.Encrypt(input.Password);
        }

        _accounts.Update(account);
        await _audit.AddAsync(nameof(SmtpAccount), account.Id.ToString(), "SmtpAccountUpdated",
            details: $"name={account.AccountName}; host={account.Host}:{account.Port}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return account;
    }

    public async Task<SmtpActivationResult?> ActivateAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var target = await _accounts.GetByIdAsync(id, tenantId, cancellationToken);
        if (target is null)
        {
            return null;
        }

        // Already active → no-op (AC-SMTP-005.3); do not write an audit entry for a non-change.
        if (target.IsActive)
        {
            return new SmtpActivationResult(target.Id, null);
        }

        Guid? deactivatedId = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Deactivate the current active account FIRST and flush it, so the "single active per tenant"
            // partial unique index is never momentarily violated when the target is activated (ADR-003).
            var current = await _accounts.GetActiveAsync(tenantId, ct);
            if (current is not null && current.Id != target.Id)
            {
                current.IsActive = false;
                _accounts.Update(current);
                await _unitOfWork.SaveChangesAsync(ct);
                deactivatedId = current.Id;
            }

            target.IsActive = true;
            _accounts.Update(target);
            await _audit.AddAsync(nameof(SmtpAccount), target.Id.ToString(), "SmtpAccountActivated",
                details: deactivatedId is null ? "deactivated=none" : $"deactivated={deactivatedId}", performedBy: null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        return new SmtpActivationResult(target.Id, deactivatedId);
    }

    public async Task<bool?> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, tenantId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        // The active account cannot be deleted; it must be deactivated first (AC-SMTP-004.2).
        if (account.IsActive)
        {
            throw new SmtpAccountException(SmtpAccountErrorCodes.ActiveAccountDelete, "The active account cannot be deleted. Activate another account first.");
        }

        _accounts.Remove(account);
        await _audit.AddAsync(nameof(SmtpAccount), account.Id.ToString(), "SmtpAccountDeleted",
            details: $"name={account.AccountName}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SmtpTestResult?> TestSendAsync(Guid id, Guid tenantId, string recipientEmail, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, tenantId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var credentials = new SmtpAccountCredentials(
            account.Host,
            account.Port,
            account.EncryptionType,
            account.AuthType,
            account.Username,
            DecryptOrNull(account.EncryptedPassword),
            account.FromName,
            account.FromEmail);

        var message = new SmtpMessage(
            recipientEmail.Trim(),
            Subject: "IntegrationHub SMTP test message",
            Body: $"This is a test message sent from the IntegrationHub SMTP account '{account.AccountName}' ({account.Host}:{account.Port}). " +
                  "If you received it, the account is configured correctly.");

        var result = await _sender.SendAsync(credentials, message, cancellationToken);

        // Test sends are diagnostic: never change active status, never write an audit entry, and report
        // failures as a result rather than an exception (AC-SMTP-006.3/006.6).
        return result.Success
            ? new SmtpTestResult(true, DateTime.UtcNow, result.ServerResponse, null, null)
            : new SmtpTestResult(false, null, null, result.ErrorCategory, result.ErrorMessage);
    }

    private string? EncryptOrNull(string? plaintext)
        => string.IsNullOrEmpty(plaintext) ? null : _encryption.Encrypt(plaintext);

    private string? DecryptOrNull(string? ciphertext)
        => string.IsNullOrEmpty(ciphertext) ? null : _encryption.Decrypt(ciphertext);
}
