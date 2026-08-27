// Reading a file that is already STORED on the server — the counterpart to useFileDrop, which describes
// a File the browser is still holding.
//
// A stored file cannot simply be linked to. /api/media/{id}/content refuses an anonymous caller for
// anything but a profile picture, and a browser following a plain href sends no Authorization header, so
// every attachment link opened onto {"success":false,"code":"UNAUTHORIZED"} instead of the document. The
// bytes are pulled through the authenticated client here and handed to the tab as a blob URL, which is
// the one shape of link a fresh tab can follow without credentials of its own.
//
// Shapes accepted: the REMS request's `files` rows ({ id, mediaId, fileName, mimeType, fileSize, url }),
// the UF attachment rows ({ id, fileName, fileExtension, fileSize }) and a bare media response
// ({ id, originalFileName, mimeType }). Everything below reads whichever of those keys is present rather
// than asking every call site to reshape its rows first.

import { mediaApi } from "services/api";
import { extOf, formatFileSize, iconForExtension, isImageExtension } from "composables/useFileDrop";

/** The media id to fetch a stored file's bytes by. */
export const mediaIdOf = (file) => file?.mediaId || file?.id || null;

/** What to call the file on screen. */
export const nameOf = (file) => file?.fileName || file?.originalFileName || file?.name || "Attachment";

/** The file's extension, with a leading dot, from whichever of the two places it is recorded in. */
export const extOfStored = (file) => {
  const declared = file?.fileExtension;
  return declared ? `.${String(declared).replace(/^\./, "").toLowerCase()}` : extOf(nameOf(file));
};

/** The Material icon for a stored file's type. */
export const iconForStored = (file) => iconForExtension(extOfStored(file));

/** True when the stored file is a picture. */
export const isImageStored = (file) =>
  (file?.mimeType || "").startsWith("image/") || isImageExtension(extOfStored(file));

/** "PDF · 1.2 MB", or just the size where the name carries no extension. */
export const describeStored = (file) => {
  const ext = extOfStored(file).replace(".", "").toUpperCase();
  const size = file?.fileSize || file?.size;
  return [ext, size ? formatFileSize(size) : ""].filter(Boolean).join(" · ");
};

// The tab holds the blob for as long as it is rendering it; the URL only has to survive the navigation.
// Two minutes is long enough for a slow PDF to paint and short enough that reading a folder of documents
// does not pin every one of them in memory for the session.
const REVOKE_AFTER_MS = 120000;

/**
 * Fetches a stored file's bytes. Media is the default store, but not the only one — Universal Features
 * keeps its attachments behind /api/uf/attachments/{id}/download — so a caller whose file lives
 * elsewhere passes its own `fetchBlob`.
 */
const bytesOf = (file, fetchBlob) => {
  if (fetchBlob) return fetchBlob(file);
  const mediaId = mediaIdOf(file);
  if (!mediaId) return Promise.reject(new Error("This file has no stored copy to open."));
  return mediaApi.content(mediaId);
};

/**
 * Opens a stored file in a new browser tab.
 *
 * The tab is opened SYNCHRONOUSLY, before the download starts: a window.open() that runs after an await
 * is no longer attributable to the click that caused it, and pop-up blockers stop it. So a blank tab is
 * claimed in the click's own tick and pointed at the blob once it arrives. If the browser blocked even
 * that, the file is saved instead of shown — better than a click that appears to do nothing.
 *
 * Returns nothing and throws on failure, so callers can report it the way they report any other API
 * error.
 */
export async function openStoredFile (file, fetchBlob = null) {
  const tab = window.open("", "_blank");
  try {
    const blob = await bytesOf(file, fetchBlob);
    // A blob with no type opens as a download prompt rather than in the viewer, and the server's own
    // Content-Type is the better answer for a row that recorded one.
    const typed = file?.mimeType && !blob.type ? blob.slice(0, blob.size, file.mimeType) : blob;
    const url = URL.createObjectURL(typed);
    if (tab) {
      tab.location.href = url;
    } else {
      saveBlob(url, nameOf(file));
    }
    setTimeout(() => URL.revokeObjectURL(url), REVOKE_AFTER_MS);
  } catch (err) {
    // A tab left sitting on about:blank after a failed fetch is worse than no tab at all.
    tab?.close();
    throw err;
  }
}

/** Downloads a stored file under its own name rather than opening it. */
export async function downloadStoredFile (file, fetchBlob = null) {
  const blob = await bytesOf(file, fetchBlob);
  const url = URL.createObjectURL(blob);
  saveBlob(url, nameOf(file));
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function saveBlob (url, fileName) {
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
}
