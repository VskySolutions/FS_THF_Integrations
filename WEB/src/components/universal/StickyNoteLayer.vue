<template>
  <div class="uf-sticky-layer">
    <!-- Floating controls -->
    <div class="uf-sticky-controls">
      <q-btn round color="primary" icon="o_sticky_note_2" @click="openCreate">
        <q-tooltip>New sticky note</q-tooltip>
      </q-btn>
      <q-btn v-if="notes.length" round color="grey-7" :icon="hideAll ? 'o_visibility_off' : 'o_visibility'" @click="toggleHideAll">
        <q-tooltip>{{ hideAll ? "Show sticky notes" : "Hide sticky notes" }}</q-tooltip>
      </q-btn>
    </div>

    <!-- Desktop: floating cards -->
    <template v-if="!hideAll && !isMobile">
      <div
        v-for="n in notes"
        :key="n.id"
        class="uf-sticky-note"
        :style="cardStyle(n)"
        @mousedown="bringToFront(n)"
      >
        <div class="uf-sticky-note__bar" @mousedown.stop="startDrag(n, $event)">
          <q-icon v-if="!n.isPersonal" name="o_groups" size="16px" class="q-mr-xs" />
          <span class="uf-sticky-note__title ellipsis">{{ n.title || (n.isPersonal ? "Note" : "Team note") }}</span>
          <q-space />
          <q-badge v-if="!n.isPersonal" color="deep-orange" label="Team" class="q-mr-xs" />
          <q-btn flat dense round size="xs" :icon="n._state.isMinimised ? 'o_expand_more' : 'o_expand_less'" @mousedown.stop @click="toggleMinimise(n)" />
          <q-btn v-if="n.isPersonal && n.isOwner" flat dense round size="xs" icon="o_close" @mousedown.stop @click="remove(n)" />
          <q-btn v-else flat dense round size="xs" icon="o_close" @mousedown.stop @click="dismiss(n)">
            <q-tooltip>Dismiss</q-tooltip>
          </q-btn>
        </div>
        <div v-show="!n._state.isMinimised" class="uf-sticky-note__body">{{ n.body }}</div>
        <div
          v-show="!n._state.isMinimised"
          class="uf-sticky-note__resize"
          @mousedown.stop="startResize(n, $event)"
        />
      </div>
    </template>

    <!-- Mobile: stacked banners at the bottom -->
    <div v-if="!hideAll && isMobile" class="uf-sticky-mobile">
      <q-banner
        v-for="n in notes"
        :key="n.id"
        dense
        rounded
        class="q-mb-xs"
        :style="{ background: n.colour || '#fff9c4' }"
      >
        <div class="text-weight-medium">{{ n.title || (n.isPersonal ? "Note" : "Team note") }}</div>
        <div class="fs-13">{{ n.body }}</div>
        <template #action>
          <q-btn v-if="n.isPersonal && n.isOwner" flat dense no-caps label="Delete" @click="remove(n)" />
          <q-btn v-else flat dense no-caps label="Dismiss" @click="dismiss(n)" />
        </template>
      </q-banner>
    </div>

    <!-- Create / edit dialog -->
    <q-dialog v-model="createOpen">
      <q-card style="min-width: 340px;">
        <q-card-section class="text-h6">New sticky note</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <app-text-field v-model="form.title" label="Title" />
          <app-text-field v-model="form.body" label="Note *" type="textarea" autogrow />
          <div class="row items-center q-gutter-xs">
            <span class="text-grey-7 q-mr-sm">Colour</span>
            <div
              v-for="c in palette"
              :key="c"
              class="uf-swatch cursor-pointer"
              :style="{ backgroundColor: c, outline: c === form.colour ? '2px solid #1976d2' : 'none' }"
              @click="form.colour = c"
            />
          </div>
          <app-select v-model="form.scope" :options="scopeOptions" label="Visible on" emit-value map-options />
          <template v-if="canTenant">
            <q-toggle v-model="form.isTenant" label="Team note (visible to everyone)" />
            <app-text-field v-if="form.isTenant" v-model="form.expiresAt" type="datetime-local" label="Expires (optional)" />
          </template>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn unelevated no-caps color="primary" label="Create" :disable="!form.body.trim()" :loading="creating" @click="create" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useQuasar } from "quasar";
import { useDebounceFn } from "@vueuse/core";
import { ufStickyNoteApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePreferences } from "composables/usePreferences";
import { usePermissions, Permissions } from "composables/usePermissions";
import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";

const $q = useQuasar();
const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const prefs = usePreferences("stickyNotes");
const { has } = usePermissions();
const canTenant = has(Permissions.SettingsManage);

const isMobile = $q.platform.is.mobile;
const palette = ["#fff9c4", "#ffe0b2", "#c8e6c9", "#bbdefb", "#f8bbd0", "#d1c4e9"];

const notes = ref([]);
const hideAll = ref(prefs.get("hideAll", false));

const createOpen = ref(false);
const creating = ref(false);
const form = reactive({ title: "", body: "", colour: palette[0], scope: "global", isTenant: false, expiresAt: "" });
const scopeOptions = [
  { label: "Everywhere", value: "global" },
  { label: "This page only", value: route.path }
];

let maxZ = 10;

const load = async () => {
  // Always fetch so we know the note count even when hidden — the toggle button and
  // the rendered cards are gated on `hideAll` in the template, not on the fetch.
  try {
    const data = (await ufStickyNoteApi.list(route.path)) || [];
    notes.value = data.map((n, i) => ({
      ...n,
      _state: n.state
        ? { ...n.state }
        : { x: 40 + i * 28, y: 90 + i * 28, width: 240, height: 200, isMinimised: false, zIndex: ++maxZ }
    }));
    maxZ = Math.max(maxZ, ...notes.value.map((n) => n._state.zIndex || 0));
  } catch {
    notes.value = [];
  }
};

const cardStyle = (n) => ({
  left: `${n._state.x}px`,
  top: `${n._state.y}px`,
  width: `${n._state.width}px`,
  height: n._state.isMinimised ? "auto" : `${n._state.height}px`,
  background: n.colour || "#fff9c4",
  zIndex: n._state.zIndex
});

const persist = useDebounceFn((n) => {
  ufStickyNoteApi.saveState(n.id, {
    x: n._state.x,
    y: n._state.y,
    width: n._state.width,
    height: n._state.height,
    isMinimised: n._state.isMinimised,
    zIndex: n._state.zIndex
  }).catch(() => {});
}, 800);

const bringToFront = (n) => {
  n._state.zIndex = ++maxZ;
  persist(n);
};

const startDrag = (n, e) => {
  const startX = e.clientX;
  const startY = e.clientY;
  const origX = n._state.x;
  const origY = n._state.y;
  const move = (ev) => {
    n._state.x = Math.max(0, origX + (ev.clientX - startX));
    n._state.y = Math.max(56, origY + (ev.clientY - startY));
  };
  const up = () => {
    window.removeEventListener("mousemove", move);
    window.removeEventListener("mouseup", up);
    persist(n);
  };
  window.addEventListener("mousemove", move);
  window.addEventListener("mouseup", up);
};

const startResize = (n, e) => {
  const startX = e.clientX;
  const startY = e.clientY;
  const origW = n._state.width;
  const origH = n._state.height;
  const move = (ev) => {
    n._state.width = Math.max(160, origW + (ev.clientX - startX));
    n._state.height = Math.max(120, origH + (ev.clientY - startY));
  };
  const up = () => {
    window.removeEventListener("mousemove", move);
    window.removeEventListener("mouseup", up);
    persist(n);
  };
  window.addEventListener("mousemove", move);
  window.addEventListener("mouseup", up);
};

const toggleMinimise = (n) => {
  n._state.isMinimised = !n._state.isMinimised;
  persist(n);
};

const toggleHideAll = () => {
  hideAll.value = !hideAll.value;
  prefs.set("hideAll", hideAll.value);
  if (!hideAll.value) load();
};

const openCreate = () => {
  form.title = "";
  form.body = "";
  form.colour = palette[0];
  form.scope = "global";
  form.isTenant = false;
  form.expiresAt = "";
  createOpen.value = true;
};

const create = async () => {
  creating.value = true;
  try {
    await ufStickyNoteApi.create({
      title: form.title || null,
      body: form.body.trim(),
      colour: form.colour,
      scope: form.scope,
      isPersonal: !form.isTenant,
      expiresAtUtc: form.isTenant && form.expiresAt ? new Date(form.expiresAt).toISOString() : null
    });
    createOpen.value = false;
    notify.success("Sticky note created.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    creating.value = false;
  }
};

const remove = async (n) => {
  const ok = await confirm({ title: "Delete note", message: "Delete this sticky note?", confirmLabel: "Delete", type: "danger" });
  if (!ok) return;
  try {
    await ufStickyNoteApi.remove(n.id);
    notes.value = notes.value.filter((x) => x.id !== n.id);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const dismiss = async (n) => {
  try {
    await ufStickyNoteApi.dismiss(n.id);
    notes.value = notes.value.filter((x) => x.id !== n.id);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Re-fetch when the route (scope) changes.
watch(() => route.path, () => load());
onMounted(load);
</script>

<style scoped>
.uf-sticky-layer { position: fixed; inset: 0; pointer-events: none; z-index: 2000; }
.uf-sticky-controls { position: fixed; right: 18px; bottom: 68px; display: flex; flex-direction: column; gap: 8px; pointer-events: all; z-index: 2100; }
.uf-sticky-note {
  position: fixed;
  pointer-events: all;
  border-radius: 6px;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.uf-sticky-note__bar { display: flex; align-items: center; padding: 2px 4px 2px 8px; cursor: move; background: rgba(0, 0, 0, 0.06); font-size: 13px; }
.uf-sticky-note__title { font-weight: 600; max-width: 120px; }
.uf-sticky-note__body { flex: 1; padding: 8px; overflow: auto; white-space: pre-wrap; word-break: break-word; font-size: 13px; }
.uf-sticky-note__resize { position: absolute; right: 0; bottom: 0; width: 14px; height: 14px; cursor: nwse-resize; background: linear-gradient(135deg, transparent 50%, rgba(0,0,0,0.25) 50%); }
.uf-sticky-mobile { position: fixed; left: 8px; right: 8px; bottom: 8px; pointer-events: all; z-index: 2100; }
.uf-swatch { width: 26px; height: 26px; border-radius: 6px; }
</style>
