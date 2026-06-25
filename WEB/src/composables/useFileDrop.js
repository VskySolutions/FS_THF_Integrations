import { ref } from "vue";

// Drag-and-drop state + file validation shared by the file-upload components (AppSingleFileUpload,
// AppMultiFileUpload). Keeps the dropzone behaviour and accept/size rules identical everywhere.
export function useFileDrop () {
  const dragOver = ref(false);
  const onDragOver = () => { dragOver.value = true; };
  const onDragLeave = () => { dragOver.value = false; };
  return { dragOver, onDragOver, onDragLeave };
}

const IMAGE_EXTS = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg"];

// Lower-cased extension (with dot) of a file name, or "".
export function extOf (name) {
  const i = String(name || "").lastIndexOf(".");
  return i >= 0 ? String(name).slice(i).toLowerCase() : "";
}

// True when a File is an image (by MIME type or extension) — used to show a thumbnail preview.
export function isImageFile (file) {
  return (file?.type || "").startsWith("image/") || IMAGE_EXTS.includes(extOf(file?.name));
}

// A Material icon representing a file's type, by extension (for non-image previews).
export function iconForFile (file) {
  const e = extOf(file?.name);
  if (IMAGE_EXTS.includes(e)) return "o_image";
  if (e === ".pdf") return "o_picture_as_pdf";
  if ([".xls", ".xlsx", ".csv"].includes(e)) return "o_table_chart";
  if ([".doc", ".docx", ".txt", ".rtf", ".md"].includes(e)) return "o_description";
  if ([".zip", ".rar", ".7z"].includes(e)) return "o_folder_zip";
  return "o_insert_drive_file";
}

// Validate a picked/dropped FileList against an accept list (extensions, e.g. ".pdf,.png") and an
// optional max size (MB). Returns the accepted files plus a human-readable error for any rejection.
export function validateFiles (fileList, { accept = "", maxSizeMb = null } = {}) {
  const exts = String(accept || "")
    .split(",")
    .map((s) => s.trim().toLowerCase())
    .filter((s) => s.startsWith("."));
  const maxBytes = maxSizeMb ? maxSizeMb * 1024 * 1024 : null;

  const accepted = [];
  let error = null;
  for (const file of Array.from(fileList || [])) {
    if (exts.length && !exts.includes(extOf(file.name))) {
      error = `"${file.name}" is not an allowed file type.`;
      continue;
    }
    if (maxBytes && file.size > maxBytes) {
      error = `"${file.name}" exceeds the ${maxSizeMb} MB limit.`;
      continue;
    }
    accepted.push(file);
  }
  return { accepted, error };
}

// Human-readable byte size, shared by the upload components' selected-file rows.
export function formatFileSize (bytes) {
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(i ? 1 : 0)} ${units[i]}`;
}
