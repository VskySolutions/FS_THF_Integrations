using System.Globalization;
using FluentValidation;

namespace EmsPortal.Api.Validators;

/// <summary>
/// What a person's name may be, in one place. The browser twin is <c>WEB/src/utils/personName.js</c>; the
/// two must agree or a form the SPA accepts comes back as a 400 the field cannot explain.
/// <para>
/// A name is letters. What this exists to keep OUT is what actually arrives in name boxes: a phone number
/// typed into the surname, an email address, "N/A", "test123", and the punctuation a keyboard slip leaves
/// behind. Every one of those becomes a Person record, and a client filed under "asdf1" is a client
/// nobody finds again.
/// </para>
/// <para>
/// Three marks are allowed INSIDE a name because real names carry them — the hyphen (Smith-Jones), the
/// apostrophe (O'Brien) and the period (St. John) — and so is the internal space, which is not a lapse in
/// strictness but the reason a name is asked for in two boxes at all: "Van Der Berg" is one surname, and
/// a rule that rejected it would send that client's record back to the guesswork the split was made to
/// end. Leading, trailing and doubled spaces are still refused; those are typing, not names.
/// </para>
/// </summary>
public static class PersonNames
{
    /// <summary>Mirrors the nvarchar(100) name columns, and the browser's NAME_MAX_LENGTH.</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// What is wrong with a name, as a sentence to show against the field — or <c>null</c> when there is
    /// nothing wrong. An EMPTY value is never this method's complaint: whether a name is REQUIRED differs
    /// field by field, so emptiness is left to whatever rule asks for it.
    /// </summary>
    public static string? Issue(string? value, string label = "This name")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!string.Equals(value, trimmed, StringComparison.Ordinal))
        {
            return $"{label} cannot start or end with a space.";
        }
        if (trimmed.Length > MaxLength)
        {
            return $"{label} is at most {MaxLength} characters.";
        }
        if (trimmed.Any(char.IsDigit))
        {
            return $"{label} cannot contain numbers.";
        }
        if (trimmed.Contains("  ", StringComparison.Ordinal))
        {
            return $"{label} cannot contain two spaces in a row.";
        }
        if (!IsNameStart(trimmed[0]))
        {
            return $"{label} must start with a letter.";
        }
        return trimmed.All(IsNameChar)
            ? null
            : $"{label} can only contain letters, spaces, hyphens, apostrophes and periods.";
    }

    /// <summary>True when a name is usable as it stands (an empty one included — see <see cref="Issue"/>).</summary>
    public static bool IsValid(string? value) => Issue(value) is null;

    // A letter from any script, plus the combining marks that go with one (an accent written as its own
    // code point). char.IsLetter covers the scripts; the mark categories cover decomposed forms, which is
    // how a name pasted out of another system often arrives.
    private static bool IsNameStart(char c) => char.IsLetter(c) || IsMark(c);

    private static bool IsNameChar(char c) =>
        IsNameStart(c) || c is ' ' or '-' or '\'' or '’' or '.';

    private static bool IsMark(char c) => CharUnicodeInfo.GetUnicodeCategory(c) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
}

/// <summary>Wires <see cref="PersonNames"/> into a FluentValidation chain.</summary>
public static class PersonNameValidatorExtensions
{
    /// <summary>
    /// The property must read as a person's name. Silent on an empty value — pair it with
    /// <c>NotEmpty()</c> where the name is required.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeAPersonName<T>(
        this IRuleBuilder<T, string?> rule, string label)
        => rule
            .Must(value => PersonNames.IsValid(value))
            // The message is re-derived from the value so the caller is told WHICH rule they broke — a
            // digit, a doubled space, a symbol — rather than being read the whole rule back at them.
            .WithMessage((_, value) => PersonNames.Issue(value, label)
                ?? $"{label} can only contain letters, spaces, hyphens, apostrophes and periods.");
}
