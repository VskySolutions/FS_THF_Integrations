<template>
  <q-item ref="rowRef" clickable class="app-file-item" :disable="opening" @click="open">
    <q-item-section avatar>
      <q-spinner v-if="opening" color="primary" size="28px" />
      <q-avatar v-else-if="thumbUrl" rounded size="40px">
        <img :src="thumbUrl" alt="">
      </q-avatar>
      <q-icon v-else :name="icon" size="34px" color="primary" />
    </q-item-section>
    <q-item-section>
      <q-item-label class="ellipsis">{{ name }}</q-item-label>
      <q-item-label caption>{{ description }}</q-item-label>
    </q-item-section>
    <q-item-section side>
      <div class="row items-center no-wrap">
        <q-icon name="o_open_in_new" size="18px" color="grey-6" class="q-mr-xs">
          <q-tooltip>Open in a new tab</q-tooltip>
        </q-icon>
        <q-btn
          v-if="removable" flat round dense size="sm" icon="o_close" color="negative"
          :disable="disable" @click.stop="$emit('remove')"
        >
          <q-tooltip>Remove</q-tooltip>
        </q-btn>
      </div>
    </q-item-section>
  </q-item>
</template>

<script setup>
// Preview row for a file that is already SAVED against a record: the icon for its type, its name, its
// extension and size, and — where the form it sits on is editable — an ✕ that takes it off the record.
// Clicking the row opens the file in a new tab (see openStoredFile: the bytes come through the
// authenticated client, because a bare link to /api/media/… is refused).
//
// The staged counterpart is AppFilePreviewItem, which previews a File the browser is still holding. The
// two are deliberately the same row, so a document looks the same before and after it is saved — which
// includes the thumbnail below: a picture used to preview as a picture while it sat in the picker and as
// a grey glyph the moment it was saved, so the save appeared to lose the one thing worth looking at.
import { ref, computed, watch, onMounted, onBeforeUnmount } from "vue";
import { getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import {
  describeStored, fetchStoredBytes, iconForStored, isImageStored, mediaIdOf, nameOf, openStoredFile
} from "composables/useFilePreview";

const props = defineProps({
  // Any stored-file shape: a REMS request file, a UF attachment, or a media response. See useFilePreview.
  file: { type: Object, required: true },
  // Whether this form may take the file off the record. Off by default: most places that list files
  // are reading them.
  removable: { type: Boolean, default: false },
  // Removal is in flight / the form is locked — the row still opens, it just cannot be removed.
  disable: { type: Boolean, default: false },
  // Where the bytes come from, for a file that is NOT in the media store — Universal Features keeps its
  // attachments behind an endpoint of its own. Default: /api/media/{mediaId}/content.
  fetchBlob: { type: Function, default: null }
});
defineEmits(["remove"]);

const notify = useNotify();
const opening = ref(false);

const icon = computed(() => iconForStored(props.file));
const name = computed(() => nameOf(props.file));
const description = computed(() => describeStored(props.file));

// ---- The thumbnail, for a stored file that is a picture ----
// Fetched only when the row is actually on screen, and only for images. A stored file's bytes come down
// through the authenticated client one request at a time, so a folder of a dozen attachments would
// otherwise pay for twelve downloads to draw a list most of which is below the fold.
const rowRef = ref(null);
const thumbUrl = ref(null);
// The bytes behind the thumbnail, kept so that opening a picture that has already been previewed does not
// fetch it a second time.
let thumbBlob = null;
let observer = null;

const revokeThumb = () => {
  if (thumbUrl.value) URL.revokeObjectURL(thumbUrl.value);
  thumbUrl.value = null;
  thumbBlob = null;
};

const loadThumb = async () => {
  if (thumbUrl.value || !isImageStored(props.file)) return;
  try {
    const blob = await fetchStoredBytes(props.file, props.fetchBlob);
    thumbBlob = blob;
    thumbUrl.value = URL.createObjectURL(blob);
  } catch {
    // Silent on purpose. The row still names the file, still says what type and size it is, and still
    // opens it — and a click that fails reports itself, which is where the reader actually asked for the
    // bytes. A toast about a decoration nobody requested is noise.
  }
};

const stopObserving = () => { observer?.disconnect(); observer = null; };

onMounted(() => {
  if (!isImageStored(props.file)) return;
  const el = rowRef.value?.$el || rowRef.value;
  // No observer, or no element to watch: fetch now rather than never.
  if (!el || typeof IntersectionObserver !== "function") { void loadThumb(); return; }
  observer = new IntersectionObserver((entries) => {
    if (!entries.some((e) => e.isIntersecting)) return;
    stopObserving();
    void loadThumb();
  });
  observer.observe(el);
});

// A single-file row is REPLACED rather than re-keyed — the purchase order and the signed CAF are each one
// row whose file changes under it — so the old picture has to go when it does. On screen already by then,
// which is why this does not wait for the observer.
watch(() => mediaIdOf(props.file), () => {
  stopObserving();
  revokeThumb();
  void loadThumb();
});

onBeforeUnmount(() => {
  stopObserving();
  revokeThumb();
});

const open = async () => {
  if (opening.value) return;
  opening.value = true;
  try {
    // The previewed bytes where there are some: the tab is opened synchronously by openStoredFile and
    // then pointed at the blob, so handing it one it already holds is the difference between a picture
    // appearing at once and a second trip to the server for the same file.
    const source = thumbBlob ? () => Promise.resolve(thumbBlob) : props.fetchBlob;
    await openStoredFile(props.file, source);
  } catch (err) {
    notify.error(getApiErrorMessage(err, "That file could not be opened."));
  } finally {
    opening.value = false;
  }
};
</script>

<style scoped>
.app-file-item { border: 1px solid #e2e7ee; border-radius: 8px; }
/* Matches the staged row's thumbnail, so a picture is framed the same before and after it is saved. */
.app-file-item :deep(img) { object-fit: cover; }
</style>
