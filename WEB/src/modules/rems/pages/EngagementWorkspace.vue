<template>
  <q-page padding>
    <app-detail-header :items="breadcrumbs" back-to="/rems/client-forms">
      <template #actions>
        <q-btn
          outline no-caps color="primary" icon="o_visibility" label="View submitted form"
          :disable="!workspace" class="q-mr-sm" @click="submittedOpen = true"
        />
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="36px" /></div>

    <q-banner v-else-if="errorMsg" class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else-if="workspace">
      <!-- Request identity -->
      <div class="row items-center q-mb-md q-col-gutter-sm">
        <div class="col-auto">
          <div class="text-h5">{{ workspace.client.name }}</div>
          <div class="text-caption text-grey-6">
            {{ workspace.remsNumber }} · {{ workspace.requestStatus }}
          </div>
        </div>
      </div>

      <!-- Editable client (SEPARATE from the read-only submitted-form review). -->
      <engagement-client-card :client="workspace.client" :rems-id="remsId" @updated="onClientUpdated" />

      <!-- Entity tabs — one per entity (main + related). -->
      <q-card flat bordered class="rems-card">
        <q-tabs
          v-model="activeEntity" dense align="left" active-color="primary" indicator-color="primary"
          class="text-grey-7 rems-entity-tabs" no-caps
        >
          <q-tab v-for="e in workspace.entities" :key="e.id" :name="e.id">
            <div class="row items-center no-wrap">
              <q-icon :name="e.isMainEntity ? 'o_star' : 'o_apartment'" size="16px" class="q-mr-xs" />
              {{ e.name }}
            </div>
          </q-tab>
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="activeEntity" keep-alive animated>
          <q-tab-panel v-for="e in workspace.entities" :key="e.id" :name="e.id">
            <engagement-entity-panel
              :entity="e"
              :staff="staff"
              :dept-options="departmentOptions"
              :service-line-options="serviceLineOptions"
              :marketing-groups="marketingGroups"
              :marketing-unavailable="marketingUnavailable"
              :tax-form-options="taxFormOptions"
              :tax-form-unavailable="taxFormUnavailable"
              :other-entities="otherEntitiesFor(e)"
              :can-send-approval="canSendApproval"
              @workspace-refresh="load"
            />
          </q-tab-panel>
        </q-tab-panels>
      </q-card>
    </template>

    <!-- Read-only submitted-form review — reused, SEPARATE from this editable workspace (AC-REMS-013.3/023.7). -->
    <submitted-form-dialog v-model="submittedOpen" :rems-id="remsId" />
  </q-page>
</template>

<script setup>
// The staff REMS engagement workspace (WO-117 Part A): a submitted request's editable client + per-entity
// engagement setup, marketing, commission and send-for-approval. Distinct from the read-only submitted-form
// review (reused here only as a reference dialog). The approver decision/checklist UI is a separate surface.
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useRemsEngagementOptionSets } from "modules/rems/useRemsMeta";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import SubmittedFormDialog from "modules/rems/components/SubmittedFormDialog.vue";
import EngagementClientCard from "modules/rems/components/engagement/EngagementClientCard.vue";
import EngagementEntityPanel from "modules/rems/components/engagement/EngagementEntityPanel.vue";

const route = useRoute();
const { has } = usePermissions();

const remsId = route.params.remsId;
const canSendApproval = computed(() => has(Permissions.RemsApprovalsSend));

const {
  departmentOptions, serviceLineOptions, marketingGroups, marketingUnavailable,
  taxFormOptions, taxFormUnavailable, load: loadOptionSets
} = useRemsEngagementOptionSets();

const workspace = ref(null);
const staff = ref([]);
const loading = ref(true);
const errorMsg = ref("");
const submittedOpen = ref(false);
const activeEntity = ref(null);

const breadcrumbs = computed(() => [
  { label: "Home", icon: "o_home", to: "/" },
  { label: "Client Forms", to: "/rems/client-forms" },
  { label: workspace.value?.remsNumber || "Engagement Workspace" }
]);

// Sibling engagements that can serve as a Copy-From source: another entity of the same client that has an
// engagement (excluding self). value = the source engagement id.
const otherEntitiesFor = (entity) =>
  (workspace.value?.entities || [])
    .filter((e) => e.id !== entity.id && e.engagement)
    .map((e) => ({ label: e.name, value: e.engagement.id }));

const load = async () => {
  loading.value = true;
  errorMsg.value = "";
  try {
    const ws = await remsApi.engagement(remsId);
    workspace.value = ws;
    // Preserve the active entity across reloads; default to the main entity, else the first.
    const ids = (ws.entities || []).map((e) => e.id);
    if (!activeEntity.value || !ids.includes(activeEntity.value)) {
      activeEntity.value = (ws.entities.find((e) => e.isMainEntity) || ws.entities[0])?.id || null;
    }
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
  } finally {
    loading.value = false;
  }
};

const loadStaff = async () => {
  try {
    const admins = await remsApi.admins();
    staff.value = (admins || []).map((a) => ({ label: a.name, value: a.id }));
  } catch {
    staff.value = [];
  }
};

const onClientUpdated = (client) => {
  if (workspace.value) workspace.value.client = client;
};

onMounted(() => {
  loadOptionSets();
  loadStaff();
  load();
});
</script>

<style scoped>
.rems-card { border-radius: 12px; }
.rems-entity-tabs { border-top-left-radius: 12px; border-top-right-radius: 12px; }
</style>
