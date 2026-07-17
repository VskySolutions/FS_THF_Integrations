using FluentValidation;
using EmsPortal.Api.Models.SmtpAccounts;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Validators.SmtpAccounts;

public sealed class CreateSmtpAccountRequestValidator : AbstractValidator<CreateSmtpAccountRequest>
{
    public CreateSmtpAccountRequestValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.EncryptionType)
            .Must(SmtpEnum.IsValid<SmtpEncryptionType>)
            .WithMessage("Encryption type must be one of: None, StartTls, SslTls, Auto.");
        RuleFor(x => x.AuthType)
            .Must(SmtpEnum.IsValid<SmtpAuthType>)
            .WithMessage("Auth type must be one of: None, Plain, Login, CramMd5.");
        RuleFor(x => x.Username).MaximumLength(255);
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress().MaximumLength(255);
    }
}

public sealed class UpdateSmtpAccountRequestValidator : AbstractValidator<UpdateSmtpAccountRequest>
{
    public UpdateSmtpAccountRequestValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.EncryptionType)
            .Must(SmtpEnum.IsValid<SmtpEncryptionType>)
            .WithMessage("Encryption type must be one of: None, StartTls, SslTls, Auto.");
        RuleFor(x => x.AuthType)
            .Must(SmtpEnum.IsValid<SmtpAuthType>)
            .WithMessage("Auth type must be one of: None, Plain, Login, CramMd5.");
        RuleFor(x => x.Username).MaximumLength(255);
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress().MaximumLength(255);
    }
}

public sealed class TestSmtpRequestValidator : AbstractValidator<TestSmtpRequest>
{
    public TestSmtpRequestValidator()
        => RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
}

/// <summary>Helper for validating that a string is a defined enum name (case-insensitive).</summary>
internal static class SmtpEnum
{
    public static bool IsValid<TEnum>(string? value) where TEnum : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out _);

    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
        => Enum.Parse<TEnum>(value, ignoreCase: true);
}
