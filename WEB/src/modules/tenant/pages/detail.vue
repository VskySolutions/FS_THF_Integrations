<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Home', icon: 'o_home', to: '/' },
        { label: 'Tenants', to: { name: 'tenants' } },
        { label: tenant?.name || 'Tenant' }
      ]"
      :back-to="{ name: 'tenants' }"
    />

    <div v-if="loading" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="40px" />
    </div>

    <div v-else-if="tenant">
      <!-- Basic info -->
      <q-card flat bordered class="tenant-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Basic information</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <q-input v-model="name" outlined dense stack-label label="Name" class="col-12 col-sm-6" />
          <q-input :model-value="tenant.identifier" outlined dense stack-label readonly label="Identifier" class="col-12 col-sm-6" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn unelevated no-caps color="primary" label="Save" :loading="savingName" :disable="name === tenant.name" @click="saveName" />
        </q-card-actions>
      </q-card>

      <!-- Status -->
      <q-card flat bordered class="tenant-card q-mb-md">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Status</div>
          <q-badge :color="statusColor" class="q-ml-md">{{ tenant.status }}</q-badge>
          <q-space />
          <q-btn
            v-if="tenant.status !== 'Archived'"
            outline
            no-caps
            :color="tenant.status === 'Active' ? 'negative' : 'positive'"
            :label="tenant.status === 'Active' ? 'Deactivate' : 'Activate'"
            @click="toggleStatus"
          />
        </q-card-section>
      </q-card>

      <!-- Danger zone -->
      <q-card v-if="tenant.status !== 'Archived'" flat bordered class="tenant-card danger-zone">
        <q-card-section class="text-subtitle1 text-weight-medium text-negative">Danger zone</q-card-section>
        <q-separator />
        <q-banner v-if="archiveError" dense class="bg-red-1 text-negative q-ma-md">
          <template #avatar><q-icon name="o_error" color="negative" /></template>
          {{ archiveError }}
        </q-banner>
        <q-card-actions>
          <div class="text-body2 text-grey-7 q-pa-sm">Archiving retires this tenant.</div>
          <q-space />
          <q-btn outline no-caps color="negative" icon="o_archive" label="Archive tenant" @click="archive" />
        </q-card-actions>
      </q-card>
    </div>
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { tenantApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppDetailHeader from "components/common/AppDetailHeader.vue";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();

const tenantId = route.params.id;
const tenant = ref(null);
const loading = ref(false);
const name = ref("");
const savingName = ref(false);
const archiveError = ref("");

const statusColor = computed(() =>
  ({ Active: "positive", Inactive: "grey", Archived: "blue-grey" }[tenant.value?.status] || "grey"));

const load = async () => {
  loading.value = !tenant.value;
  try {
    tenant.value = await tenantApi.get(tenantId);
    name.value = tenant.value.name;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const saveName = async () => {
  savingName.value = true;
  try {
    await tenantApi.update(tenantId, { name: name.value });
    notify.success("Tenant updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingName.value = false;
  }
};

const toggleStatus = async () => {
  const activate = tenant.value.status !== "Active";
  const ok = await confirm({
    title: activate ? "Activate tenant" : "Deactivate tenant",
    message: `${activate ? "Activate" : "Deactivate"} "${tenant.value.name}"?`,
    type: activate ? "primary" : "danger"
  });
  if (!ok) return;
  try {
    await tenantApi.setStatus(tenantId, activate);
    notify.success("Status updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const archive = async () => {
  archiveError.value = "";
  const ok = await confirm({
    title: "Archive tenant",
    message: `Archive "${tenant.value.name}"?`,
    confirmLabel: "Archive",
    type: "danger"
  });
  if (!ok) return;
  try {
    await tenantApi.archive(tenantId);
    notify.success("Tenant archived.");
    router.push({ name: "tenants" });
  } catch (err) {
    archiveError.value = getApiErrorMessage(err);
  }
};

onMounted(load);
</script>

<style scoped>
.tenant-card {
  border-radius: 12px;
}
.danger-zone {
  border-color: var(--q-negative);
}
</style>
