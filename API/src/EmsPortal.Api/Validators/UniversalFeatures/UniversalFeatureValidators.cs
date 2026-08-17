using FluentValidation;
using EmsPortal.Api.Models.UniversalFeatures;

namespace EmsPortal.Api.Validators.UniversalFeatures;

public sealed class CreateConversationMessageRequestValidator : AbstractValidator<CreateConversationMessageRequest>
{
    public CreateConversationMessageRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
        RuleFor(x => x.EntityType).IsInEnum();
    }
}

public sealed class UpdateConversationMessageRequestValidator : AbstractValidator<UpdateConversationMessageRequest>
{
    public UpdateConversationMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Colour).MaximumLength(30);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Colour).MaximumLength(30);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}

public sealed class CreateReminderRequestValidator : AbstractValidator<CreateReminderRequest>
{
    public CreateReminderRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
        RuleFor(x => x.DueAtUtc).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class UpdateReminderRequestValidator : AbstractValidator<UpdateReminderRequest>
{
    public UpdateReminderRequestValidator()
    {
        RuleFor(x => x.DueAtUtc).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ApplyTagRequestValidator : AbstractValidator<ApplyTagRequest>
{
    public ApplyTagRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
    }
}

public sealed class CreatePinRequestValidator : AbstractValidator<CreatePinRequest>
{
    public CreatePinRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
    }
}

public sealed class UpsertColourCodeRequestValidator : AbstractValidator<UpsertColourCodeRequest>
{
    public UpsertColourCodeRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
        RuleFor(x => x.Colour).MaximumLength(30);
    }
}

public sealed class CreateChecklistRequestValidator : AbstractValidator<CreateChecklistRequest>
{
    public CreateChecklistRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class AddChecklistItemRequestValidator : AbstractValidator<AddChecklistItemRequest>
{
    public AddChecklistItemRequestValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
}

public sealed class UpdateChecklistItemRequestValidator : AbstractValidator<UpdateChecklistItemRequest>
{
    public UpdateChecklistItemRequestValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
}

public sealed class PdfExportRequestValidator : AbstractValidator<PdfExportRequest>
{
    public PdfExportRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.EntityType).IsInEnum();
    }
}

public sealed class CreateStickyNoteRequestValidator : AbstractValidator<CreateStickyNoteRequest>
{
    public CreateStickyNoteRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.Colour).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Scope).MaximumLength(300);
    }
}

public sealed class UpdateStickyNoteRequestValidator : AbstractValidator<UpdateStickyNoteRequest>
{
    public UpdateStickyNoteRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.Colour).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Scope).MaximumLength(300);
    }
}
