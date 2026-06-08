import { ref, computed } from "vue";
import { notifyWarning } from "assets/utils";

export function useSingleFileUpload (options = {}) {
  const uploaderRef = ref(null);
  const fileData = ref(null);
  const isValidFile = ref(true);

  const {
    // allowedTypes = [],
    allowedExtensions = [],
    maxSizeInMb = 25,
    isImage = false,
    onSuccess = () => {},
    onError = () => {}
  } = options;

  // Convert MB to bytes
  const maxSizeInBytes = computed(() => {
    return maxSizeInMb * 1024 * 1024;
  });

  function reset () {
    uploaderRef.value?.reset();
    fileData.value = null;
  }

  // function validateFile (file) {
  //   if (!file) return false;

  //   const fileExtension = file.name.split(".").pop().toLowerCase();
  //   const mimeType = file.type;

  //   const isValidExtension = allowedTypes.includes(fileExtension);
  //   const isValidMime = allowedTypes.includes(mimeType);

  //   if (allowedTypes.length && !(isValidExtension || isValidMime)) {
  //     onError("Invalid file type.");
  //     reset();
  //     return false;
  //   }

  //   if (file.size > maxSizeInBytes.value) {
  //     notifyWarning({
  //       message: `File size should be less than ${maxSizeInMb} MB`
  //     });
  //     reset();
  //     return false;
  //   }

  //   return true;
  // }

  function validateFile (file) {
    if (!file) return false;
    // const mimeType = file.type ? file.type.trim() : "";
    const fileName = file.name ? file.name.toLowerCase() : "";

    // const fileTypeValid =
    //   allowedTypes.length === 0 || allowedTypes.includes(mimeType);

    const extensionValid =
      allowedExtensions.length === 0 ||
      allowedExtensions.some(ext => fileName.endsWith(ext.toLowerCase()));

    // return fileTypeValid || extensionValid;
    // return extensionValid;
    if (!extensionValid) {
      isValidFile.value = false;
      onError(`Only ${allowedExtensions.join(", ")} files are allowed`);
      reset();
      return false;
    }

    return true;
  }

  function validateSize (file) {
    if (file.size > maxSizeInBytes.value) {
      isValidFile.value = false;
      notifyWarning({
        message: `File size should be less than ${maxSizeInMb} MB`
      });
      return false;
    }
    return true;
  }

  function validateImage (file) {
    return new Promise((resolve) => {
      const img = new Image();
      const url = URL.createObjectURL(file);

      img.onload = () => {
        // if (width && height && (img.width !== width || img.height !== height)) {
        //   onError(`Image must be ${width}x${height}`);
        //   reset();
        //   resolve(false);
        // } else {
        //   resolve(true);
        // }
        resolve(true);
      };

      img.onerror = () => {
        isValidFile.value = false;
        onError("Invalid image file.");
        reset();
        resolve(false);
      };

      img.src = url;
    });
  }

  async function onFileAdded (files) {
    const file = files[0];
    if (!validateFile(file)) return;

    if (!validateSize(file)) return;

    if (isImage) {
      const validImage = await validateImage(file);
      if (!validImage) return;
    }

    fileData.value = file;
    isValidFile.value = true;
    onSuccess(file);
  }

  return {
    uploaderRef,
    fileData,
    isValidFile,
    onFileAdded,
    reset
  };
}
