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
      <!-- Client and entity work sit side by side, split evenly; drag the divider to give either side
           more room. The chosen width is remembered per user. Each pane scrolls on its own, so a long
           entity form never pushes the client card off-screen. -->
      <q-splitter v-model="splitPct" :limits="[25, 75]" class="rems-split">
        <template #separator>
          <q-icon name="o_drag_indicator" size="18px" color="primary" />
        </template>

        <!-- Editable client (SEPARATE from the read-only submitted-form review). -->
        <template #before>
          <div class="rems-split__pane rems-split__pane--before">
            <engagement-client-card :client="workspace.client" :rems-id="remsId" @updated="onClientUpdated" />
          </div>
        </template>

        <!-- Entity tabs — one per entity (main + related). -->
        <template #after>
          <div class="rems-split__pane rems-split__pane--after">
            <q-card flat bordered class="rems-card">
              <!-- Same header treatment as the client pane so the two line up across the divider. -->
              <q-card-section class="rems-card__title row items-center q-py-sm">
                <div class="text-subtitle1 text-primary">
                  <q-icon name="o_work" size="20px" class="q-mr-xs" />{{ engagementTitle }}
                </div>
                <q-space />
                <!-- The visible tab teleports its primary save here, so the action sits beside the title
                     exactly like the client pane's rather than trailing the fields. -->
                <div id="engagement-header-actions" class="row items-center q-gutter-sm" />
              </q-card-section>
              <q-separator />

              <!-- The entity's EIN and engagement status ride on the tab strip itself: as a row of their
                   own underneath, they repeated the tab's label and spent a whole row on one badge. -->
              <div class="row items-center no-wrap">
                <!-- The main entity IS the client, so naming it here would just repeat the card title;
                     related entities are separate businesses and keep their own names. -->
                <q-tabs
                  v-model="activeEntity" dense align="left" active-color="primary" indicator-color="primary"
                  class="text-grey-7 col" no-caps
                >
                  <q-tab v-for="e in workspace.entities" :key="e.id" :name="e.id">
                    <div class="row items-center no-wrap">
                      <q-icon :name="e.isMainEntity ? 'o_star' : 'o_apartment'" size="16px" class="q-mr-xs" />
                      {{ e.isMainEntity ? "Main entity" : e.name }}
                    </div>
                  </q-tab>
                </q-tabs>
                <div class="col-auto row items-center no-wrap q-gutter-sm q-px-md">
                  <span v-if="activeEntityRow?.ein" class="text-caption text-grey-6">EIN {{ activeEntityRow.ein }}</span>
                  <q-badge v-if="activeStatus" :color="activeStatus.color">{{ activeStatus.label }}</q-badge>
                </div>
              </div>
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
                    :department-directors="workspace.departmentDirectors || []"
                    :executive-options="executiveOptions"
                    :billing-manager-options="billingManagerOptions"
                    :can-send-approval="canSendApproval"
                    @workspace-refresh="load"
                  />
                </q-tab-panel>
              </q-tab-panels>
            </q-card>
          </div>
        </template>
      </q-splitter>
    </template>

    <!-- Read-only submitted-form review — reused, SEPARATE from this editable workspace (AC-REMS-013.3/023.7). -->
    <submitted-form-dialog v-model="submittedOpen" :rems-id="remsId" />
  </q-page>
</template>

<script setup>
// The staff REMS engagement workspace (WO-117 Part A): a submitted request's editable client + per-entity
// engagement setup, marketing, commission and send-for-approval. Distinct from the read-only submitted-form
// review (reused here only as a reference dialog). The approver decision/checklist UI is a separate surface.
import { ref, computed, watch, onMounted } from "vue";
import { LocalStorage } from "quasar";
import { useRoute } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useRemsMeta, useRemsEngagementOptionSets } from "modules/rems/useRemsMeta";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import SubmittedFormDialog from "modules/rems/components/SubmittedFormDialog.vue";
import EngagementClientCard from "modules/rems/components/engagement/EngagementClientCard.vue";
import EngagementEntityPanel from "modules/rems/components/engagement/EngagementEntityPanel.vue";

const route = useRoute();
const { has } = usePermissions();
const { engagementStatusMeta } = useRemsMeta();

const remsId = route.params.remsId;
const canSendApproval = computed(() => has(Permissions.RemsApprovalsSend));

const {
  departmentOptions, serviceLineOptions, marketingGroups, marketingUnavailable,
  taxFormOptions, taxFormUnavailable, load: loadOptionSets
} = useRemsEngagementOptionSets();

const workspace = ref(null);
const staff = ref([]);
const executiveOptions = ref([]);
const billingManagerOptions = ref([]);
const loading = ref(true);
const errorMsg = ref("");
const submittedOpen = ref(false);
const activeEntity = ref(null);

// Where the client / entity divider sits, as a % of the width. Persisted across visits, the same way the
// layout remembers its drawer — resizing is only useful if the choice sticks.
const SPLIT_KEY = "remsEngagementSplit";
const splitPct = ref(LocalStorage.getItem(SPLIT_KEY) ?? 50);
watch(splitPct, (value) => LocalStorage.set(SPLIT_KEY, value));

// The entity behind the selected tab, so its EIN + engagement status can sit on the tab strip.
const activeEntityRow = computed(() =>
  (workspace.value?.entities || []).find((e) => e.id === activeEntity.value) || null);
const activeStatus = computed(() => {
  const status = activeEntityRow.value?.engagement?.status;
  return status ? engagementStatusMeta(status) : null;
});

const engagementTitle = computed(() => {
  const name = workspace.value?.client?.name;
  return name ? `Engagement Setup — ${name}` : "Engagement Setup";
});

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

// The Engagement Executive / Billing Manager pickers are scoped to a user group of the same name, kept in
// Administration → User Groups. An absent or empty group yields an empty list on purpose (the setup form
// then says which group needs members) rather than quietly offering every admin.
const EXECUTIVE_GROUP = "Engagement Executive";
const BILLING_MANAGER_GROUP = "Billing Manager";

const toOptions = (rows) => (rows || []).map((a) => ({ label: a.name, value: a.id }));

const loadStaff = async () => {
  const [admins, executives, billingManagers] = await Promise.all([
    remsApi.admins().catch(() => []),
    remsApi.admins(EXECUTIVE_GROUP).catch(() => []),
    remsApi.admins(BILLING_MANAGER_GROUP).catch(() => [])
  ]);
  staff.value = toOptions(admins);
  executiveOptions.value = toOptions(executives);
  billingManagerOptions.value = toOptions(billingManagers);
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

/* Widen the 1px divider into something that reads as a grab handle. */
.rems-split :deep(.q-splitter__separator) {
  background-color: transparent;
  border-left: 1px solid #e0e6ed;
}
.rems-split__pane--before { padding-right: 16px; }
.rems-split__pane--after { padding-left: 16px; }

/* Below the layout's own drawer breakpoint there is no room for two columns of form fields, so the
   panes stack and the divider goes away — there is nothing left to drag. The saved split percentage is
   untouched, so it comes back as soon as the window is wide enough again. */
@media (max-width: 1023px) {
  .rems-split {
    flex-direction: column;
  }
  /* QSplitter sizes the first pane with an inline width and flexes the second; neither applies once
     the panes are stacked. */
  .rems-split :deep(.q-splitter__panel) {
    width: 100% !important;
    flex: none;
  }
  .rems-split :deep(.q-splitter__separator) {
    display: none;
  }
  .rems-split__pane--before { padding-right: 0; }
  .rems-split__pane--after { padding-left: 0; padding-top: 16px; }
}
</style>
