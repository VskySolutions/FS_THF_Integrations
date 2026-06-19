<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="rows.length > 0"
    action-label="Tenants"
    :action-route="{ path: '/tenants' }"
    :action-permission="Permissions.TenantsWrite"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!rows.length" class="column flex-center q-pa-lg text-positive">
      <q-icon name="o_check_circle" size="32px" class="q-mb-sm" />
      <div class="text-subtitle2">All onboarded ✓</div>
    </div>
    <q-list v-else separator>
      <q-item
        v-for="t in rows"
        :key="t.tenantId"
        clickable
        @click="goToTenant(t.tenantId)"
      >
        <q-item-section>
          <q-item-label class="text-weight-medium text-primary">{{ t.tenantName }}</q-item-label>
          <q-item-label caption>
            <div class="row q-gutter-xs q-mt-xs">
              <q-chip v-if="t.missingCredentials" dense color="red-1" text-color="negative" icon="o_key">Credentials</q-chip>
              <q-chip v-if="t.missingUsers" dense color="red-1" text-color="negative" icon="o_person_off">Users</q-chip>
              <q-chip v-if="t.missingSchedules" dense color="red-1" text-color="negative" icon="o_schedule">Schedules</q-chip>
            </div>
          </q-item-label>
        </q-item-section>
        <q-item-section side><q-icon name="o_chevron_right" color="grey-6" /></q-item-section>
      </q-item>
    </q-list>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Tenant Onboarding" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  onboarding: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const rows = computed(() => props.onboarding || []);

const goToTenant = (tenantId) => {
  if (tenantId) router.push({ path: `/tenants/${tenantId}` });
};
</script>
