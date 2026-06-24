<template>
  <div class="row items-center q-gutter-xs no-wrap">
    <q-btn-dropdown flat no-caps dense color="primary" :label="activeLabel" icon="o_view_list">
      <q-list style="min-width: 220px;">
        <q-item v-close-popup clickable :active="!activeId" @click="select(null)">
          <q-item-section>All Records</q-item-section>
        </q-item>

        <template v-if="privateViews.length">
          <q-separator />
          <q-item-label header class="q-py-xs">My views</q-item-label>
          <q-item
            v-for="v in privateViews"
            :key="v.id"
            v-close-popup
            clickable
            :active="v.id === activeId"
            @click="select(v)"
          >
            <q-item-section>{{ v.name }}</q-item-section>
            <q-item-section side>
              <div class="row items-center">
                <q-btn flat round dense size="sm" :icon="defaultId === v.id ? 'star' : 'o_star'" color="amber-8" @click.stop="setDefault(v)" />
                <q-btn flat round dense size="sm" icon="o_delete" color="negative" @click.stop="remove(v)" />
              </div>
            </q-item-section>
          </q-item>
        </template>

        <template v-if="sharedViews.length">
          <q-separator />
          <q-item-label header class="q-py-xs">Shared views</q-item-label>
          <q-item
            v-for="v in sharedViews"
            :key="v.id"
            v-close-popup
            clickable
            :active="v.id === activeId"
            @click="select(v)"
          >
            <q-item-section>
              <q-item-label>{{ v.name }}</q-item-label>
              <q-item-label caption>Shared</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-btn flat round dense size="sm" :icon="defaultId === v.id ? 'star' : 'o_star'" color="amber-8" @click.stop="setDefault(v)" />
            </q-item-section>
          </q-item>
        </template>
      </q-list>
    </q-btn-dropdown>

    <q-badge v-if="dirty" color="orange-7" label="Unsaved" />

    <template v-if="dirty">
      <q-btn v-if="canSaveActive" flat dense size="sm" no-caps color="primary" label="Save" @click="saveActive" />
      <q-btn flat dense size="sm" no-caps color="primary" label="Save As" @click="openSaveAs" />
      <q-btn flat dense size="sm" no-caps color="grey-7" label="Discard" @click="discard" />
    </template>

    <!-- Save As dialog -->
    <q-dialog v-model="saveAsOpen">
      <q-card style="min-width: 320px;">
        <q-card-section class="text-h6">Save view</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <app-text-field v-model="saveAsName" label="View name *" />
          <q-toggle v-if="canShare" v-model="saveAsShared" label="Share with tenant" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn unelevated no-caps color="primary" label="Save" :disable="!saveAsName.trim()" @click="saveAs" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { ufSavedViewApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { usePreferences } from "composables/usePreferences";
import { usePermissions, Permissions } from "composables/usePermissions";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  listPage: { type: String, required: true },
  // The page's current serializable view state: { filters, sort, columns }.
  state: { type: Object, default: () => ({}) }
});
const emit = defineEmits(["apply"]);

const notify = useNotify();
const prefs = usePreferences(`savedview:${props.listPage}`);
const { has } = usePermissions();
const canShare = has(Permissions.SettingsManage);

const views = ref([]);
const activeId = ref(null);
const defaultId = ref(prefs.get("defaultViewId", null));

const privateViews = computed(() => views.value.filter((v) => !v.isShared || v.isOwner));
const sharedViews = computed(() => views.value.filter((v) => v.isShared && !v.isOwner));
const activeView = computed(() => views.value.find((v) => v.id === activeId.value) || null);
const activeLabel = computed(() => activeView.value?.name || "All Records");
const canSaveActive = computed(() => activeView.value && (activeView.value.isOwner || (activeView.value.isShared && canShare)));

const stateJson = computed(() => JSON.stringify(props.state || {}));
const dirty = computed(() => {
  if (!activeView.value) {
    // "All Records" is dirty only when filters/sort have been applied.
    return !!(props.state && Object.keys(props.state.filters || {}).length);
  }
  return stateJson.value !== viewStateJson(activeView.value);
});

const viewStateJson = (v) => JSON.stringify({
  filters: safeParse(v.filtersJson),
  sort: safeParse(v.sortJson),
  columns: safeParse(v.columnsJson)
});

const safeParse = (json) => {
  if (!json) return null;
  try { return JSON.parse(json); } catch { return null; }
};

const load = async () => {
  try {
    views.value = (await ufSavedViewApi.list(props.listPage)) || [];
    // Auto-apply the user's default view on first load.
    if (defaultId.value && views.value.some((v) => v.id === defaultId.value)) {
      const def = views.value.find((v) => v.id === defaultId.value);
      select(def);
    }
  } catch {
    views.value = [];
  }
};

const select = (view) => {
  activeId.value = view?.id || null;
  emit("apply", view ? { filters: safeParse(view.filtersJson), sort: safeParse(view.sortJson), columns: safeParse(view.columnsJson) } : null);
};

const saveAsOpen = ref(false);
const saveAsName = ref("");
const saveAsShared = ref(false);

const openSaveAs = () => {
  saveAsName.value = "";
  saveAsShared.value = false;
  saveAsOpen.value = true;
};

const saveAs = async () => {
  try {
    const created = await ufSavedViewApi.create({
      name: saveAsName.value.trim(),
      listPage: props.listPage,
      filtersJson: JSON.stringify(props.state.filters || {}),
      sortJson: JSON.stringify(props.state.sort || {}),
      columnsJson: JSON.stringify(props.state.columns || {}),
      isShared: saveAsShared.value
    });
    saveAsOpen.value = false;
    notify.success("View saved.");
    await load();
    if (created?.id) activeId.value = created.id;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const saveActive = async () => {
  if (!activeView.value) return;
  try {
    await ufSavedViewApi.update(activeView.value.id, {
      name: activeView.value.name,
      filtersJson: JSON.stringify(props.state.filters || {}),
      sortJson: JSON.stringify(props.state.sort || {}),
      columnsJson: JSON.stringify(props.state.columns || {}),
      isShared: activeView.value.isShared
    });
    notify.success("View updated.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const discard = () => select(activeView.value);

const remove = async (view) => {
  try {
    await ufSavedViewApi.remove(view.id);
    if (activeId.value === view.id) select(null);
    if (defaultId.value === view.id) { defaultId.value = null; prefs.remove("defaultViewId"); }
    notify.success("View deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const setDefault = (view) => {
  defaultId.value = view.id;
  prefs.set("defaultViewId", view.id);
  notify.info(`"${view.name}" is now your default for this list.`);
};

onMounted(load);
</script>
