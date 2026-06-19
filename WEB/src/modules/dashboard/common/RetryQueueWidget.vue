<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="retryQueueCount > 0"
    action-label="Retry Queue"
    :action-route="{ path: '/jobs', query: { tab: 'retry' } }"
    action-permission="jobs.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div class="column flex-center q-pa-md">
      <div class="text-h3 text-weight-bold" :class="retryQueueCount > 0 ? 'text-warning' : 'text-grey-7'">
        {{ retryQueueCount }}
      </div>
      <q-badge
        v-if="retryQueueCount > 0"
        color="warning"
        text-color="white"
        class="q-mt-xs"
        label="Awaiting retry"
      />
      <div v-else class="text-grey-6 q-mt-xs">No jobs queued</div>
      <div v-if="retryQueueCount > 0 && retryQueueNextRunUtc" class="text-caption text-grey-7 q-mt-sm">
        Next retry {{ relativeTime(retryQueueNextRunUtc) }}
      </div>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Retry Queue" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  retryQueueCount: { type: Number, default: 0 },
  retryQueueNextRunUtc: { type: [String, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

// Relative-time label for the next scheduled retry (future or past). UTC assumed when no tz given.
const relativeTime = (value) => {
  if (!value) return "—";
  let s = String(value);
  if (!/[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)) s += "Z";
  const target = new Date(s).getTime();
  if (Number.isNaN(target)) return "—";
  const diff = target - Date.now();
  const future = diff >= 0;
  const mins = Math.round(Math.abs(diff) / 60000);
  const phrase = (n, unit) => (future ? `in ${n}${unit}` : `${n}${unit} ago`);
  if (mins < 1) return future ? "shortly" : "just now";
  if (mins < 60) return phrase(mins, "m");
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return phrase(hrs, "h");
  return phrase(Math.round(hrs / 24), "d");
};
</script>
