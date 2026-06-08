import { ref, computed, watch, onMounted } from "vue";
import { notifyWarning } from "assets/utils";

export function useMultiFileUpload (options = {}) {
  const uploaderRef = ref(null);
  const filesData = ref([]);
  const isValidFiles = ref(true);

  const {
    // allowedTypes = [],
    allowedExtensions = [],
    initialFiles = [],
    maxSizeInMb = 25,
    onSuccess = () => {},
    onError = () => {}
  } = options;

  // Convert MB to bytes
  const maxSizeInBytes = computed(() => {
    return maxSizeInMb * 1024 * 1024;
  });

  function reset () {
    uploaderRef.value?.reset();
    filesData.value = [];
  }

  function validateFile (file) {
    // const mimeType = file.type ? file.type.trim() : "";
    const fileName = file.name ? file.name.toLowerCase() : "";

    // const fileTypeValid =
    //   allowedTypes.length === 0 || allowedTypes.includes(mimeType);

    const extensionValid =
      allowedExtensions.length === 0 ||
      allowedExtensions.some(ext => fileName.endsWith(ext));

    // return fileTypeValid || extensionValid;
    return extensionValid;
  }

  function validateSize (file) {
    if (file.size > maxSizeInBytes.value) {
      notifyWarning({
        message: `File size should be less than ${maxSizeInMb} MB`
      });
      return false;
    }
    return true;
  }

  function onFileAdded (files) {
    if (!files || files.length === 0) return;

    // old files are loaded
    if ((!filesData.value || filesData.value.length === 0) && initialFiles?.length) {
      filesData.value = [...initialFiles];
    }
    // check limit (only current selection)
    if (files.length > 10) {
      notifyWarning({
        message: "You can upload maximum 10 files at a time"
      });

      // remove extra files from UI
      files.forEach((file, index) => {
        if (index >= 10) {
          uploaderRef.value?.removeFile(file);
        }
      });

      // keep only first 10
      files = files.slice(0, 10);
    }

    const validFiles = [];
    const invalidFiles = [];

    files.forEach(file => {
      if (!validateFile(file)) {
        invalidFiles.push(file);
        // hasError = true;
        return;
      }

      if (!validateSize(file)) {
        invalidFiles.push(file);
        // hasError = true;
        return;
      }

      file.flag = "new";
      validFiles.push(file);
    });

    // If any invalid → mark false
    // if (invalidFiles.length) {
    //   isValidFiles.value = false;
    // }

    // remove invalid from uploader UI
    invalidFiles.forEach(file => {
      uploaderRef.value?.removeFile(file);
      // isValidFiles.value = true;
    });

    if (invalidFiles.length) {
      const names = invalidFiles.map(f => f.name).join(", ");
      // isValidFiles.value = false;
      onError(`Invalid files: ${names}`);
    }

    // filesData.value.push(...validFiles);
    // Merge old + new files
    filesData.value = [...filesData.value, ...validFiles];
    isValidFiles.value = true;

    onSuccess(validFiles, filesData.value, isValidFiles.value);
  }

  function removeFile (file) {
    filesData.value = filesData.value.filter(f => f !== file);
    isValidFiles.value = true;
  }

  // Load initial files (EDIT MODE FIX)
  watch(
    () => initialFiles,
    (newFiles) => {
      // Only set if filesData empty (first load)
      if (newFiles) {
        filesData.value = [...newFiles];
      }
    },
    { immediate: true, deep: true }
  );

  // LOAD OLD FILES ONCE
  onMounted(() => {
    if (initialFiles?.length) {
      filesData.value = [...initialFiles];
    }
  });

  return {
    uploaderRef,
    filesData,
    isValidFiles,
    onFileAdded,
    removeFile,
    reset
  };
}
