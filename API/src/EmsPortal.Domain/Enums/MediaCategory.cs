namespace EmsPortal.Domain.Enums;

/// <summary>
/// Functional category of a stored <see cref="Entities.Media"/> file. Persisted as its name, so
/// members may be added freely but not renamed, and a name must stay within 20 characters.
/// <para>
/// The category also picks the file's purpose folder in the upload tree
/// (<c>Application.Abstractions.Storage.StoragePaths.PurposeFor</c>) — it is what the uploader says
/// the file IS, so two screens uploading the same kind of document file it in the same place.
/// </para>
/// </summary>
public enum MediaCategory
{
    Profile,
    Attachment,
    Logo,
    Contract,
    Certificate,
    Document,
    /// <summary>A signed REMS client-acceptance form. Kept apart from <see cref="Document"/> so the
    /// one document an audit engagement cannot be approved without has its own folder.</summary>
    ClientAcceptance,
    Other
}
