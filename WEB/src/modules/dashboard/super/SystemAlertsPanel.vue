<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="rows.length > 0"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!rows.length" class="column flex-center q-pa-lg text-positive">
      <q-icon name="o_check_circle" size="32px" class="q-mb-sm" />
      <div class="text-subtitle2">No Active Alerts</div>
    </div>
    <q-list v-else separator>
      <q-item
        v-for="a in rows"
        :key="a.id"
        :clickable="!!a.tenantId"
        @click="a.tenantId && goToTenant(a.tenantId)"
      >
        <q-item-section avatar>
          <q-icon :name="iconFor(a.severity)" :color="colorFor(a.severity)" />
        </q-item-section>
        <q-item-section>
          <q-item-label>{{ a.message }}</q-item-label>
          <q-item-label caption>
            <q-badge :color="colorFor(a.severity)" class="q-mr-xs">{{ a.severity }}</q-badge>
            <span v-if="a.type">{{ a.type }}</span>
            <span v-if="a.tenantName"> · {{ a.tenantName }}</span>
          </q-item-label>
        </q-item-section>
      </q-item>
    </q-list>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "System Alerts" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  systemAlerts: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const rows = computed(() => props.systemAlerts || []);

const colorFor = (severity) => {
  switch (String(severity || "").toLowerCase()) {
    case "critical":
    case "error":
    case "high": return "negative";
    case "warning":
    case "medium": return "warning";
    default: return "info";
  }
};

const iconFor = (severity) => {
  switch (String(severity || "").toLowerCase()) {
    case "critical":
    case "error":
    case "high": return "o_error";
    case "warning":
    case "medium": return "o_warning";
    default: return "o_info";
  }
};

const goToTenant = (tenantId) => router.push({ path: `/tenants/${tenantId}` });
</script>
