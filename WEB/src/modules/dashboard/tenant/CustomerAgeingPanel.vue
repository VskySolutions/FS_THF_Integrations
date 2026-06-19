<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="hasBreach"
    action-label="Customers"
    :action-route="{ name: 'customers' }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="$emit('update:collapsed', $event)"
  >
    <div v-if="!ageing.length" class="column flex-center q-pa-lg text-positive">
      <q-icon name="o_verified" size="36px" class="q-mb-sm" />
      <div class="text-subtitle2">All within SLA</div>
      <div class="text-caption text-grey-7">No overdue customer requests right now.</div>
    </div>

    <template v-else>
      <q-banner v-if="oldestPending" dense rounded class="bg-orange-1 text-orange-9 q-mb-sm">
        <template #avatar><q-icon name="o_schedule" color="orange-9" /></template>
        Oldest pending approval is {{ oldestPending.daysInStatus }} days old
        ({{ oldestPending.companyName || oldestPending.customerRequestNumber }}).
      </q-banner>

      <q-list separator>
        <q-item
          v-for="row in ageing"
          :key="row.requestId"
          clickable
          @click="goToDetail(row.requestId)"
        >
          <q-item-section>
            <q-item-label class="text-weight-medium">{{ row.customerRequestNumber }}</q-item-label>
            <q-item-label caption>{{ row.companyName || "—" }}</q-item-label>
          </q-item-section>
          <q-item-section side>
            <q-badge :color="statusColor(row.status)" class="q-mb-xs">{{ statusLabel(row.status) }}</q-badge>
            <div class="row items-center no-wrap">
              <q-badge
                v-if="row.slaBreach"
                color="negative"
                text-color="white"
                class="q-mr-xs"
              >
                <q-icon name="o_warning" size="12px" class="q-mr-xs" /> SLA
              </q-badge>
              <span class="text-caption text-grey-7">{{ row.daysInStatus }}d</span>
            </div>
          </q-item-section>
        </q-item>
      </q-list>

      <div class="text-center q-mt-sm">
        <q-btn flat dense no-caps color="primary" label="View All Overdue" :to="{ name: 'customers' }" />
      </div>
    </template>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import { Permissions } from "composables/usePermissions";
import { useCustomerStatus } from "composables/useCustomerStatus";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Customer Ageing" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  ageing: { type: Array, default: () => [] }
});

defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const { customerStatusColor: statusColor, customerStatusLabel: statusLabel } = useCustomerStatus();

const hasBreach = computed(() => props.ageing.some((a) => a.slaBreach));

// Oldest pending-approval item over 3 days, surfaced as an alert banner.
const oldestPending = computed(() => {
  const pending = props.ageing
    .filter((a) => a.status === "PendingApproval" && (Number(a.daysInStatus) || 0) > 3)
    .sort((a, b) => (Number(b.daysInStatus) || 0) - (Number(a.daysInStatus) || 0));
  return pending[0] || null;
});

const goToDetail = (id) => router.push({ name: "customer_detail", params: { id } });
</script>
