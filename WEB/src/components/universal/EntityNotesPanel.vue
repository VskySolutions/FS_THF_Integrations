<template>
  <div class="column q-gutter-sm">
    <!-- Composer -->
    <div class="relative-position">
      <q-input
        ref="composerRef"
        v-model="draft"
        type="textarea"
        outlined
        dense
        autogrow
        placeholder="Write a note… type @ to mention someone"
        @update:model-value="onType"
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
      <div class="row justify-end q-mt-xs">
        <q-btn
          color="primary"
          no-caps
          unelevated
          dense
          label="Add note"
          :loading="posting"
          :disable="!draft.trim()"
          @click="post"
        />
      </div>
    </div>

    <!-- Search / filter -->
    <div class="row q-gutter-sm items-center">
      <q-input
        v-model="search"
        dense
        outlined
        clearable
        placeholder="Search notes"
        class="col"
        debounce="300"
        @update:model-value="load"
      >
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>
    </div>

    <!-- Timeline -->
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
            <q-input v-model="editDraft" type="textarea" outlined dense autogrow />
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
import { ref, onMounted } from "vue";
import { ufNotesApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useAuthStore } from "stores/auth";

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

const notes = ref([]);
const loading = ref(false);
const search = ref("");

const draft = ref("");
const posting = ref(false);
const mentionedUserIds = ref([]);

// @mention autocomplete
const composerRef = ref(null);
const mentionOpen = ref(false);
const candidates = ref([]);
let mentionTimer = null;

const editingId = ref(null);
const editDraft = ref("");

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
    notes.value = res?.data || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const onType = (value) => {
  const match = /@(\w*)$/.exec(value || "");
  if (!match) {
    mentionOpen.value = false;
    return;
  }
  const term = match[1];
  clearTimeout(mentionTimer);
  mentionTimer = setTimeout(() => fetchCandidates(term), 200);
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
  // Replace the trailing "@term" with a mention token and track the user id.
  draft.value = draft.value.replace(/@(\w*)$/, `@[${c.name}](${c.userId}) `);
  if (!mentionedUserIds.value.includes(c.userId)) {
    mentionedUserIds.value.push(c.userId);
  }
  mentionOpen.value = false;
};

const post = async () => {
  if (!draft.value.trim()) return;
  posting.value = true;
  try {
    await ufNotesApi.create({
      entityType: props.entityType,
      entityId: props.entityId,
      body: draft.value.trim(),
      mentionedUserIds: mentionedUserIds.value
    });
    draft.value = "";
    mentionedUserIds.value = [];
    notify.success("Note added.");
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
    await ufNotesApi.update(note.id, { body: editDraft.value.trim(), mentionedUserIds: [] });
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

const escapeHtml = (s) => (s || "").replace(/[&<>"']/g, (c) => (
  { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]
));

// Render @[Name](id) tokens as highlighted names; everything else escaped.
const renderBody = (body) => {
  const escaped = escapeHtml(body);
  return escaped.replace(
    /@\[([^\]]+)\]\([0-9a-fA-F-]{36}\)/g,
    (_m, name) => `<span class="uf-mention">@${escapeHtml(name)}</span>`
  );
};

const initials = (name) => (name || "?").split(" ").map((p) => p[0]).slice(0, 2).join("").toUpperCase();

onMounted(load);
defineExpose({ load });
</script>

<style scoped>
.note-body { white-space: pre-wrap; word-break: break-word; }
.note-body :deep(.uf-mention) { color: #1976d2; font-weight: 500; background: #e3f2fd; border-radius: 4px; padding: 0 2px; }
</style>
