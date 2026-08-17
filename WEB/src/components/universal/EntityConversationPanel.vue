<template>
  <div class="column q-gutter-sm">
    <!-- Header: quick search with loader + Add message -->
    <div class="row items-center no-wrap q-gutter-sm">
      <q-space />
      <q-input
        v-model="search"
        dense
        outlined
        clearable
        placeholder="Quick search"
        class="uf-conversation__search"
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
        label="Add message"
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
        placeholder="Write a message… type @ to mention someone"
        content-class="uf-conversation__editor"
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
          label="Post message"
          :loading="posting"
          :disable="!hasContent(draft)"
          @click="post"
        />
      </div>
    </div>

    <!-- The thread (newest first) -->
    <q-inner-loading :showing="loading" />
    <div v-if="!loading && !messages.length" class="text-grey-6 q-pa-md text-center">No messages yet.</div>
    <q-list separator>
      <q-item v-for="message in messages" :key="message.id" class="q-py-sm">
        <q-item-section avatar top>
          <q-avatar size="34px" color="primary" text-color="white">{{ initials(message.authorName) }}</q-avatar>
        </q-item-section>
        <q-item-section>
          <q-item-label>
            <span class="text-weight-medium">{{ message.authorName || "Unknown" }}</span>
            <span class="text-grey-6 q-ml-sm fs-12">{{ formatDateTime(message.createdOnUtc) }}</span>
            <q-badge v-if="message.isEdited" outline color="grey-7" class="q-ml-sm" label="edited" />
          </q-item-label>

          <div v-if="editingId === message.id" class="q-mt-xs">
            <q-editor v-model="editDraft" min-height="5rem" :toolbar="toolbar" content-class="uf-conversation__editor" />
            <div class="row q-gutter-xs q-mt-xs">
              <q-btn dense no-caps unelevated color="primary" label="Save" @click="saveEdit(message)" />
              <q-btn dense no-caps flat label="Cancel" @click="editingId = null" />
            </div>
          </div>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div v-else class="q-mt-xs message-body" v-html="renderBody(message.body)" />
        </q-item-section>

        <q-item-section side top>
          <div class="row">
            <q-btn v-if="canEdit(message)" flat round dense size="sm" icon="o_edit" @click="startEdit(message)" />
            <q-btn v-if="canDelete(message)" flat round dense size="sm" icon="o_delete" color="negative" @click="remove(message)" />
          </div>
        </q-item-section>
      </q-item>
    </q-list>
  </div>
</template>

<script setup>
import { ref, nextTick, onMounted, onBeforeUnmount } from "vue";
import { ufConversationApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useAuthStore } from "stores/auth";
import { escapeHtml, sanitizeHtml, isHtml, hasRichTextContent as hasContent } from "utils/richText";
// Token markup and id extraction are shared with AppRichTextField's CKEditor mentions — one definition,
// so a mention typed in either editor resolves in both.
import { mentionTokenHtml, extractMentionIds, fetchMentionCandidates } from "composables/useMentions";

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

const messages = ref([]);
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
    const res = await ufConversationApi.list({
      entityType: props.entityType,
      entityId: props.entityId,
      search: search.value || undefined,
      page: 1,
      limit: 100
    });
    const rows = res?.data || [];
    // Newest first, regardless of server ordering.
    messages.value = rows.sort((a, b) => new Date(b.createdOnUtc) - new Date(a.createdOnUtc));
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
    candidates.value = await fetchMentionCandidates(term);
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
  const html = `${mentionTokenHtml(c)}&nbsp;`;
  editorRef.value?.runCmd?.("insertHTML", html);
  mentionRange = null;
  mentionOpen.value = false;
};

// ---- post / edit / delete ----
const post = async () => {
  if (!hasContent(draft.value)) return;
  posting.value = true;
  try {
    await ufConversationApi.create({
      entityType: props.entityType,
      entityId: props.entityId,
      body: draft.value,
      mentionedUserIds: extractMentionIds(draft.value)
    });
    notify.success("Message posted.");
    closeComposer();
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    posting.value = false;
  }
};

const canEdit = (message) => message.authorId && message.authorId === currentUserId;
const canDelete = (message) => canEdit(message) || isAdmin;

const startEdit = (message) => {
  editingId.value = message.id;
  editDraft.value = message.body;
};

const saveEdit = async (message) => {
  try {
    await ufConversationApi.update(message.id, { body: editDraft.value, mentionedUserIds: extractMentionIds(editDraft.value) });
    editingId.value = null;
    notify.success("Message updated.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const remove = async (message) => {
  const ok = await confirm({
    title: "Delete message",
    message: "Delete this message? This cannot be undone.",
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await ufConversationApi.remove(message.id);
    notify.success("Message deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- rendering ----
// Sanitizing/escaping lives in utils/richText so the conversation and the description editors share one
// allowlist.

// New messages are q-editor HTML; ones written before the rich editor are plain text with @[Name](id) tokens.
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
.uf-conversation__search { max-width: 220px; width: 100%; }
.message-body { white-space: normal; word-break: break-word; }
.message-body :deep(.uf-mention) { color: #1976d2; font-weight: 500; background: #e3f2fd; border-radius: 4px; padding: 0 2px; }
.message-body :deep(p) { margin: 0 0 6px; }
.message-body :deep(ul), .message-body :deep(ol) { margin: 0 0 6px; padding-left: 20px; }
</style>
