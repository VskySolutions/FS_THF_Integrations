<template>
  <div class="column q-gutter-sm">
    <!-- Header: title (left) + quick search with loader + Add note (right) -->
    <div class="row items-center no-wrap q-gutter-sm">
      <div class="text-subtitle1">Notes</div>
      <q-space />
      <q-input
        v-model="search"
        dense
        outlined
        clearable
        placeholder="Quick search"
        class="uf-notes__search"
        debounce="300"
        :loading="loading"
        @update:model-value="load"
      >
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>
      <q-btn
        v-if="!composerOpen"
        color="primary"
        no-caps
        unelevated
        dense
        icon="o_add"
        label="Add note"
        @click="openComposer"
      />
    </div>

    <!-- Composer: rich text editor with @mention autocomplete -->
    <div v-if="composerOpen" class="relative-position">
      <q-editor
        ref="editorRef"
        v-model="draft"
        min-height="6rem"
        :toolbar="toolbar"
        placeholder="Write a note… type @ to mention someone"
        content-class="uf-notes__editor"
        @keydown.esc="mentionOpen = false"
      />
      <q-menu
        v-model="mentionOpen"
        no-focus
        no-parent-event
        anchor="bottom left"
        self="top left"
        :offset="[0, 4]"
      >
        <q-list style="min-width: 220px; max-height: 240px;" class="scroll">
          <q-item v-if="!candidates.length" dense>
            <q-item-section class="text-grey-6">No matches</q-item-section>
          </q-item>
          <q-item
            v-for="c in candidates"
            :key="c.userId"
            v-close-popup
            clickable
            dense
            @click="pickMention(c)"
          >
            <q-item-section avatar>
              <q-avatar size="26px" color="primary" text-color="white">{{ initials(c.name) }}</q-avatar>
            </q-item-section>
            <q-item-section>
              <q-item-label>{{ c.name }}</q-item-label>
              <q-item-label v-if="c.email" caption>{{ c.email }}</q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
      </q-menu>
      <div class="row justify-end q-gutter-sm q-mt-xs">
        <q-btn flat no-caps dense label="Cancel" @click="closeComposer" />
        <q-btn
          color="primary"
          no-caps
          unelevated
          dense
          label="Save note"
          :loading="posting"
          :disable="!hasContent(draft)"
          @click="post"
        />
      </div>
    </div>

    <!-- Timeline (newest first) -->
    <q-inner-loading :showing="loading" />
    <div v-if="!loading && !notes.length" class="text-grey-6 q-pa-md text-center">No notes yet.</div>
    <q-list separator>
      <q-item v-for="note in notes" :key="note.id" class="q-py-sm">
        <q-item-section avatar top>
          <q-avatar size="34px" color="primary" text-color="white">{{ initials(note.authorName) }}</q-avatar>
        </q-item-section>
        <q-item-section>
          <q-item-label>
            <span class="text-weight-medium">{{ note.authorName || "Unknown" }}</span>
            <span class="text-grey-6 q-ml-sm fs-12">{{ formatDateTime(note.createdOnUtc) }}</span>
            <q-badge v-if="note.isEdited" outline color="grey-7" class="q-ml-sm" label="edited" />
          </q-item-label>

          <div v-if="editingId === note.id" class="q-mt-xs">
            <q-editor v-model="editDraft" min-height="5rem" :toolbar="toolbar" content-class="uf-notes__editor" />
            <div class="row q-gutter-xs q-mt-xs">
              <q-btn dense no-caps unelevated color="primary" label="Save" @click="saveEdit(note)" />
              <q-btn dense no-caps flat label="Cancel" @click="editingId = null" />
            </div>
          </div>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div v-else class="q-mt-xs note-body" v-html="renderBody(note.body)" />
        </q-item-section>

        <q-item-section side top>
          <div class="row">
            <q-btn v-if="canEdit(note)" flat round dense size="sm" icon="o_edit" @click="startEdit(note)" />
            <q-btn v-if="canDelete(note)" flat round dense size="sm" icon="o_delete" color="negative" @click="remove(note)" />
          </div>
        </q-item-section>
      </q-item>
    </q-list>
  </div>
</template>

<script setup>
import { ref, nextTick, onMounted, onBeforeUnmount } from "vue";
import { ufNotesApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useAuthStore } from "stores/auth";
import { escapeHtml, sanitizeHtml, isHtml, hasRichTextContent as hasContent } from "utils/richText";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true }
});

const notify = useNotify();
const { confirm } = useConfirm();
const { formatDateTime } = useDateFormat();
const { has } = usePermissions();
const auth = useAuthStore();
const currentUserId = auth.user?.userId || null;
const isAdmin = has(Permissions.SettingsManage);

const toolbar = [
  ["bold", "italic", "underline", "strike"],
  ["unordered", "ordered"],
  ["link"],
  ["removeFormat"]
];

const notes = ref([]);
const loading = ref(false);
const search = ref("");

const composerOpen = ref(false);
const draft = ref("");
const posting = ref(false);

// @mention autocomplete state
const editorRef = ref(null);
const mentionOpen = ref(false);
const candidates = ref([]);
let mentionTimer = null;
let mentionContentEl = null;
let mentionRange = null; // the range covering the typed "@query"

const editingId = ref(null);
const editDraft = ref("");

// ---- data ----
const load = async () => {
  loading.value = true;
  try {
    const res = await ufNotesApi.list({
      entityType: props.entityType,
      entityId: props.entityId,
      search: search.value || undefined,
      page: 1,
      limit: 100
    });
    const rows = res?.data || [];
    // Newest first, regardless of server ordering.
    notes.value = rows.sort((a, b) => new Date(b.createdOnUtc) - new Date(a.createdOnUtc));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

// ---- composer open/close ----
const openComposer = async () => {
  composerOpen.value = true;
  await nextTick();
  wireMentionListener();
  editorRef.value?.focus?.();
};

const closeComposer = () => {
  composerOpen.value = false;
  mentionOpen.value = false;
  draft.value = "";
  unwireMentionListener();
};

// ---- @mention autocomplete inside the contenteditable editor ----
const editorContentEl = () =>
  editorRef.value?.getContentEl?.() || editorRef.value?.$el?.querySelector(".q-editor__content") || null;

const wireMentionListener = () => {
  const el = editorContentEl();
  if (el && el !== mentionContentEl) {
    unwireMentionListener();
    mentionContentEl = el;
    el.addEventListener("keyup", onEditorKeyup);
  }
};

const unwireMentionListener = () => {
  mentionContentEl?.removeEventListener("keyup", onEditorKeyup);
  mentionContentEl = null;
};

const onEditorKeyup = () => {
  const sel = window.getSelection();
  if (!sel || !sel.rangeCount) { mentionOpen.value = false; return; }
  const range = sel.getRangeAt(0);
  const node = range.startContainer;
  if (node.nodeType !== Node.TEXT_NODE) { mentionOpen.value = false; return; }

  const textBefore = node.textContent.slice(0, range.startOffset);
  const match = /@(\w*)$/.exec(textBefore);
  if (!match) { mentionOpen.value = false; return; }

  // Remember the exact span of the "@query" so we can replace it on selection.
  mentionRange = document.createRange();
  mentionRange.setStart(node, range.startOffset - match[0].length);
  mentionRange.setEnd(node, range.startOffset);

  clearTimeout(mentionTimer);
  mentionTimer = setTimeout(() => fetchCandidates(match[1]), 200);
};

const fetchCandidates = async (term) => {
  try {
    candidates.value = (await ufNotesApi.mentionCandidates(term)) || [];
    mentionOpen.value = true;
  } catch {
    candidates.value = [];
  }
};

const pickMention = (c) => {
  editorRef.value?.focus?.();
  // Reselect the "@query" range and replace it with a non-editable mention token.
  if (mentionRange) {
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(mentionRange);
  }
  const html = `<span class="uf-mention" data-user-id="${c.userId}" contenteditable="false">@${escapeHtml(c.name)}</span>&nbsp;`;
  editorRef.value?.runCmd?.("insertHTML", html);
  mentionRange = null;
  mentionOpen.value = false;
};

// User ids are derived from the mention tokens present in the saved HTML — robust to manual edits.
const extractMentionIds = (html) => {
  const doc = new DOMParser().parseFromString(html || "", "text/html");
  const ids = Array.from(doc.querySelectorAll("[data-user-id]"))
    .map((el) => el.getAttribute("data-user-id"))
    .filter(Boolean);
  return [...new Set(ids)];
};

// ---- post / edit / delete ----
const post = async () => {
  if (!hasContent(draft.value)) return;
  posting.value = true;
  try {
    await ufNotesApi.create({
      entityType: props.entityType,
      entityId: props.entityId,
      body: draft.value,
      mentionedUserIds: extractMentionIds(draft.value)
    });
    notify.success("Note added.");
    closeComposer();
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    posting.value = false;
  }
};

const canEdit = (note) => note.authorId && note.authorId === currentUserId;
const canDelete = (note) => canEdit(note) || isAdmin;

const startEdit = (note) => {
  editingId.value = note.id;
  editDraft.value = note.body;
};

const saveEdit = async (note) => {
  try {
    await ufNotesApi.update(note.id, { body: editDraft.value, mentionedUserIds: extractMentionIds(editDraft.value) });
    editingId.value = null;
    notify.success("Note updated.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const remove = async (note) => {
  const ok = await confirm({
    title: "Delete note",
    message: "Delete this note? This cannot be undone.",
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await ufNotesApi.remove(note.id);
    notify.success("Note deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- rendering ----
// Sanitizing/escaping lives in utils/richText so notes and the description editors share one allowlist.

// New notes are q-editor HTML; legacy notes are plain text with @[Name](id) tokens.
const renderBody = (body) => {
  if (isHtml(body)) {
    return sanitizeHtml(body);
  }
  return escapeHtml(body).replace(
    /@\[([^\]]+)\]\([0-9a-fA-F-]{36}\)/g,
    (_m, name) => `<span class="uf-mention">@${escapeHtml(name)}</span>`
  );
};

const initials = (name) => (name || "?").split(" ").map((p) => p[0]).slice(0, 2).join("").toUpperCase();

onMounted(load);
onBeforeUnmount(unwireMentionListener);
defineExpose({ load });
</script>

<style scoped>
.uf-notes__search { max-width: 220px; width: 100%; }
.note-body { white-space: normal; word-break: break-word; }
.note-body :deep(.uf-mention) { color: #1976d2; font-weight: 500; background: #e3f2fd; border-radius: 4px; padding: 0 2px; }
.note-body :deep(p) { margin: 0 0 6px; }
.note-body :deep(ul), .note-body :deep(ol) { margin: 0 0 6px; padding-left: 20px; }
</style>
