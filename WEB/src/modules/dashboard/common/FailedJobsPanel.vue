<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="jobs.length > 0"
    action-label="View Jobs"
    :action-route="{ path: '/jobs', query: { status: 'Failed' } }"
    action-permission="jobs.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!jobs.length" class="column flex-center q-pa-md text-positive">
      <q-icon name="o_check_circle" size="28px" class="q-mb-xs" />
      <div>No failures 🎉</div>
    </div>
    <q-list v-else dense separator>
      <q-item
        v-for="job in jobs"
        :key="job.jobId"
        clickable
        @click="goToJobs"
      >
        <q-item-section>
          <q-item-label class="text-weight-medium">{{ job.flowLabel || job.interfaceName || "Job" }}</q-item-label>
          <q-item-label caption lines="2" class="text-negative">{{ job.errorMessage || "Unknown error" }}</q-item-label>
          <q-item-label caption>{{ relativeTime(job.failedAtUtc) }}</q-item-label>
        </q-item-section>
        <q-item-section v-if="canRetry" side>
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            color="primary"
            icon="o_replay"
            label="Retry"
            :loading="retrying === job.jobId"
            @click.stop="onRetry(job.jobId)"
          />
        </q-item-section>
      </q-item>
    </q-list>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { jobApi, getApiErrorMessage } from "services/api";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Failed Jobs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  failedJobs: { type: Array, default: () => [] }
});
const emit = defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const { has } = usePermissions();
const { success, error: notifyError } = useNotify();

const jobs = computed(() => props.failedJobs || []);
const canRetry = computed(() => has(Permissions.JobsRetry));

const goToJobs = () => router.push({ path: "/jobs", query: { status: "Failed" } });

const retrying = ref(null);
const onRetry = async (jobId) => {
  retrying.value = jobId;
  try {
    await jobApi.retry(jobId);
    success("Job queued for retry.");
    emit("retry");
  } catch (err) {
    notifyError(getApiErrorMessage(err));
  } finally {
    retrying.value = null;
  }
};

// Compact relative-time label (e.g. "5m ago"). UTC instant assumed when no tz designator present.
const relativeTime = (value) => {
  if (!value) return "—";
  let s = String(value);
  if (!/[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)) s += "Z";
  const then = new Date(s).getTime();
  if (Number.isNaN(then)) return "—";
  const diff = Math.max(0, Date.now() - then);
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
};
</script>
