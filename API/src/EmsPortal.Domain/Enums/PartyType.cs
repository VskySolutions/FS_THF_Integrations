namespace EmsPortal.Domain.Enums;

/// <summary>
/// What kind of party a <see cref="Entities.Person"/> row stands for — a human being, or an organisation.
///
/// <para>
/// <c>Persons</c> has been a PARTY table rather than a people table for some time: it holds the firm's
/// colleagues, the clients it onboards, the role contacts captured off a client's intake form, and the
/// other individuals on an individual's return. This says which of two shapes a row is, so that the one
/// thing that genuinely differs between them — where the NAME lives — is answered by a column rather than
/// inferred.
/// </para>
/// <para>
/// AN EXPLICIT COLUMN, not "does CorporateName happen to be filled in". Two things need this answer and
/// neither can afford to guess: the client picker, which must offer only individuals when the request is
/// for an individual, and <see cref="Entities.Person.ClientDisplayName"/>, which reads a person surname-
/// first and an organisation as its plain legal name. Deriving it from a nullable field means a row with
/// a blank name has no type at all, and a row somebody half-filled has the wrong one.
/// </para>
/// <para>
/// It is deliberately NOT an option set, unlike almost every other list in REMS. A tenant may rename a
/// status or add a department because those are the firm's own vocabulary; this is a structural fact
/// about how a row is shaped, and the application branches on it absolutely — the same reason
/// <see cref="EntityType"/> is an enum. There is no third kind of party to add.
/// </para>
/// <para>
/// Values are stable integers stored on the row. Append only.
/// </para>
/// </summary>
public enum PartyType
{
    /// <summary>
    /// A human being: the name lives in <see cref="Entities.Person.FirstName"/> /
    /// <see cref="Entities.Person.LastName"/> with an optional generational suffix. The default, and what
    /// every row written before this column existed is.
    /// </summary>
    Individual = 0,

    /// <summary>
    /// A company, trust, government body or other legal entity: the name lives in
    /// <see cref="Entities.Person.CorporateName"/> and the two name fields stay empty. Everything else on
    /// the row — the email, the phone, the address, the provenance — means exactly what it does for a
    /// person, which is why this is one table and not two.
    /// </summary>
    Organisation = 1,
}
