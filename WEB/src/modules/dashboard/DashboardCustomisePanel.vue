<template>
  <q-drawer
    :model-value="modelValue"
    side="right"
    bordered
    overlay
    :width="360"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="column full-height">
      <!-- Header -->
      <div class="row items-center q-pa-md">
        <q-icon name="o_tune" color="primary" size="22px" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-medium text-primary">Customise Dashboard</div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="$emit('update:modelValue', false)">
          <q-tooltip>Close</q-tooltip>
        </q-btn>
      </div>
      <q-separator />

      <div class="q-pa-sm text-caption text-grey-7">
        Toggle widgets on or off. Drag widgets on the dashboard to reorder them.
      </div>

      <!-- Widget catalogue -->
      <q-scroll-area class="col">
        <q-list>
          <q-item v-for="w in widgets" :key="w.key" class="q-py-sm">
            <q-item-section>
              <q-item-label class="text-weight-medium">
                {{ w.title }}
                <q-badge v-if="isNew(w.key)" color="positive" class="q-ml-xs" label="New" />
              </q-item-label>
              <q-item-label caption>{{ w.description }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-toggle
                :model-value="!layout.isHidden(w.key)"
                color="primary"
                @update:model-value="layout.toggleHidden(w.key)"
              />
            </q-item-section>
          </q-item>
        </q-list>
      </q-scroll-area>

      <q-separator />
      <div class="q-pa-md">
        <q-btn
          outline
          no-caps
          color="primary"
          icon="o_restart_alt"
          label="Reset to Default"
          class="full-width"
          @click="layout.resetToDefault()"
        />
      </div>
    </div>
  </q-drawer>
</template>

<script setup>
import { computed, watch } from "vue";
import { widgetsForRole } from "modules/dashboard/widgets/registry";
import { usePreferences } from "composables/usePreferences";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  role: { type: String, default: "common" },
  layout: { type: Object, required: true }
});
defineEmits(["update:modelValue"]);

const prefs = usePreferences("dashboard");

const widgets = computed(() => widgetsForRole(props.role));

// "New" badge: widgets whose key has not yet been seen by this user.
const seen = computed(() => prefs.get("seenWidgets", []) || []);
const isNew = (key) => !seen.value.includes(key);

// Mark all currently-available widgets as seen whenever the panel opens, so the "New" badge clears
// after the user has had a chance to view the catalogue.
const markAllSeen = () => {
  const keys = widgets.value.map((w) => w.key);
  const merged = Array.from(new Set([...(prefs.get("seenWidgets", []) || []), ...keys]));
  prefs.set("seenWidgets", merged);
};

watch(
  () => props.modelValue,
  (open) => { if (open) markAllSeen(); },
  { immediate: true }
);
</script>
