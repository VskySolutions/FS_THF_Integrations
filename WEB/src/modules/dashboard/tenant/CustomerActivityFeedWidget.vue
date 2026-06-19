<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Customers"
    :action-route="{ name: 'customers' }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="$emit('update:collapsed', $event)"
  >
    <div v-if="!items.length" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_history" size="36px" class="q-mb-sm" />
      <div class="text-subtitle2">No recent activity</div>
    </div>

    <template v-else>
      <q-list>
        <q-item
          v-for="item in items"
          :key="item.id"
          clickable
          @click="goToDetail(item.customerRequestId)"
        >
          <q-item-section avatar>
            <q-avatar size="32px" :color="iconFor(item.action).color" text-color="white">
              <q-icon :name="iconFor(item.action).icon" size="18px" />
            </q-avatar>
          </q-item-section>
          <q-item-section>
            <q-item-label>
              <span class="text-weight-medium">{{ item.actor || "System" }}</span>
              {{ actionLabel(item.action) }}
              <span class="text-primary">{{ item.customerRequestNumber }}</span>
            </q-item-label>
            <q-item-label v-if="item.notes" caption class="ellipsis">{{ item.notes }}</q-item-label>
            <q-item-label caption>
              {{ relativeTime(item.timestampUtc) }}
              <q-tooltip>{{ formatDateTime(item.timestampUtc) }}</q-tooltip>
            </q-item-label>
          </q-item-section>
        </q-item>
      </q-list>

      <div class="text-center q-mt-sm">
        <q-btn flat dense no-caps color="primary" label="View All Activity" :to="{ name: 'customers' }" />
      </div>
    </template>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import { Permissions } from "composables/usePermissions";
import { useDateFormat } from "composables/useDateFormat";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Activity Feed" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  activityFeed: { type: Array, default: () => [] }
});

defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const { formatDateTime } = useDateFormat();

const items = computed(() => props.activityFeed.slice(0, 15));

// Icon + colour + verb per action. Keys are matched case-insensitively against the action string so
// variants like "sync_failed" / "SyncFailed" still resolve.
const ACTION_META = {
  submitted: { icon: "o_send", color: "blue", label: "submitted" },
  enriched: { icon: "o_auto_fix_high", color: "teal", label: "enriched" },
  approved: { icon: "o_check_circle", color: "positive", label: "approved" },
  rejected: { icon: "o_cancel", color: "negative", label: "rejected" },
  returned: { icon: "o_undo", color: "warning", label: "returned" },
  synced: { icon: "o_cloud_done", color: "positive", label: "synced" },
  syncfailed: { icon: "o_sync_problem", color: "negative", label: "sync failed" },
  created: { icon: "o_add_circle", color: "grey", label: "created" },
  reopened: { icon: "o_lock_open", color: "blue", label: "reopened" }
};

const normalize = (action) => String(action || "").replace(/[\s_-]/g, "").toLowerCase();
const iconFor = (action) => ACTION_META[normalize(action)] || { icon: "o_bolt", color: "grey-7", label: action || "updated" };
const actionLabel = (action) => iconFor(action).label;

const relativeTime = (value) => {
  if (!value) return "";
  let s = String(value);
  if (!/[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)) s += "Z";
  const then = new Date(s).getTime();
  if (Number.isNaN(then)) return "";
  const diff = Math.max(0, Date.now() - then);
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  return `${months}mo ago`;
};

const goToDetail = (id) => {
  if (id) router.push({ name: "customer_detail", params: { id } });
};
</script>
