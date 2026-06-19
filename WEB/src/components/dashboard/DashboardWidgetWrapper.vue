<template>
  <q-card flat bordered class="dashboard-widget" :class="{ 'dashboard-widget--fullscreen': fullscreen }">
    <!-- Header -->
    <q-card-section class="dashboard-widget__header row items-center no-wrap q-py-sm">
      <div class="text-subtitle1 text-weight-medium text-primary ellipsis">{{ title }}</div>
      <q-chip
        v-if="collapsed && alert"
        dense
        color="warning"
        text-color="white"
        icon="o_warning"
        class="q-ml-sm"
        label="Attention"
      />
      <q-space />
      <q-btn
        v-if="showAction"
        flat
        dense
        no-caps
        size="sm"
        color="primary"
        :icon="'o_open_in_new'"
        :label="actionLabel"
        :to="actionRoute"
        class="q-mr-xs"
      />
      <q-btn
        flat
        round
        dense
        size="sm"
        :icon="collapsed ? 'o_expand_more' : 'o_expand_less'"
        @click="toggleCollapsed"
      >
        <q-tooltip>{{ collapsed ? "Expand" : "Collapse" }}</q-tooltip>
      </q-btn>
      <q-btn flat round dense size="sm" :icon="fullscreen ? 'o_close_fullscreen' : 'o_open_in_full'" @click="toggleFullscreen">
        <q-tooltip>{{ fullscreen ? "Exit full screen" : "Full screen" }}</q-tooltip>
      </q-btn>
    </q-card-section>

    <q-separator v-if="!collapsed" />

    <!-- Body -->
    <q-card-section v-if="!collapsed" class="dashboard-widget__body">
      <div v-if="loading" class="column q-gutter-sm">
        <q-skeleton type="text" />
        <q-skeleton type="rect" height="80px" />
        <q-skeleton type="text" width="60%" />
      </div>
      <q-banner v-else-if="error" dense rounded class="bg-red-1 text-negative">
        <template #avatar><q-icon name="o_error" color="negative" /></template>
        {{ error }}
        <template #action>
          <q-btn flat dense no-caps color="negative" label="Retry" @click="$emit('retry')" />
        </template>
      </q-banner>
      <slot v-else />
    </q-card-section>

    <q-btn
      v-if="fullscreen"
      round
      dense
      color="primary"
      icon="o_close"
      class="dashboard-widget__close"
      @click="toggleFullscreen"
    />
  </q-card>
</template>

<script setup>
import { ref, computed, onBeforeUnmount } from "vue";
import { usePermissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  alert: { type: Boolean, default: false },
  actionLabel: { type: [String, null], default: null },
  actionRoute: { type: [Object, String, null], default: null },
  actionPermission: { type: [String, null], default: null }
});

const emit = defineEmits(["retry", "update:collapsed"]);

const { has } = usePermissions();

// The navigation action shows only when a label + route are supplied and (if gated) the user holds
// the required permission.
const showAction = computed(() =>
  !!props.actionLabel && !!props.actionRoute && (!props.actionPermission || has(props.actionPermission)));

const toggleCollapsed = () => emit("update:collapsed", !props.collapsed);

// ---- Full screen overlay ----
const fullscreen = ref(false);
const onEscape = (e) => { if (e.key === "Escape") toggleFullscreen(); };

const toggleFullscreen = () => {
  fullscreen.value = !fullscreen.value;
  if (fullscreen.value) {
    window.addEventListener("keydown", onEscape);
  } else {
    window.removeEventListener("keydown", onEscape);
  }
};

onBeforeUnmount(() => window.removeEventListener("keydown", onEscape));
</script>

<style scoped>
.dashboard-widget { border-radius: 12px; height: 100%; display: flex; flex-direction: column; }
.dashboard-widget__body { flex: 1 1 auto; }
.dashboard-widget--fullscreen {
  position: fixed;
  inset: 0;
  z-index: 6000;
  border-radius: 0;
  overflow: auto;
}
.dashboard-widget__close { position: fixed; top: 16px; right: 16px; z-index: 6001; }
</style>
