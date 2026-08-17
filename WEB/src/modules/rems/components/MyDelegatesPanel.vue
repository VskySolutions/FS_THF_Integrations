<template>
  <q-card flat bordered class="mdp">
    <q-card-section class="row items-center">
      <div class="col">
        <div class="text-subtitle1 text-primary">
          <q-icon name="o_switch_account" size="20px" class="q-mr-xs" />My REMS delegates
        </div>
        <div class="text-caption text-grey-7">
          People who may work your REMS requests for you. Everything they do stays attributed to both of
          you. Delegation never covers approving.
        </div>
      </div>
      <q-btn unelevated no-caps color="primary" icon="o_person_add" label="Add delegate" @click="openNew" />
    </q-card-section>
    <q-separator />

    <q-list v-if="rows.length" separator>
      <q-item v-for="d in rows" :key="d.id">
        <q-item-section>
          <q-item-label>
            {{ d.delegateName }}
            <q-badge v-if="!d.isActive" color="grey-6" class="q-ml-sm">Not active</q-badge>
          </q-item-label>
          <q-item-label caption>
            {{ d.canSend ? "Prepare and send to the client" : "Prepare only" }}
            <template v-if="d.startsOn || d.endsOn">
              · {{ d.startsOn || "any time" }} → {{ d.endsOn || "open-ended" }}
            </template>
          </q-item-label>
        </q-item-section>
        <q-item-section side>
          <div class="row q-gutter-xs">
            <q-btn flat round dense color="primary" icon="o_edit" @click="openEdit(d)">
              <q-tooltip>Edit</q-tooltip>
            </q-btn>
            <q-btn flat round dense color="negative" icon="o_delete" @click="remove(d)">
              <q-tooltip>Withdraw</q-tooltip>
            </q-btn>
          </div>
        </q-item-section>
      </q-item>
    </q-list>
    <q-card-section v-else class="text-grey-6">
      You have not named anyone. Add a delegate if you want someone able to prepare your requests.
    </q-card-section>

    <q-dialog v-model="editOpen">
      <q-card class="mdp__dialog">
        <q-card-section class="text-subtitle1 text-primary">
          {{ form.id ? "Edit delegate" : "Add delegate" }}
        </q-card-section>
        <q-separator />
        <q-card-section class="column q-gutter-md">
          <app-select
            v-model="form.delegateUserId" :options="userOptions" label="Delegate" required
            :readonly="!!form.id" :clearable="false"
          />
          <div>
            <q-toggle v-model="form.canPrepare" label="Can prepare requests as me" color="primary" />
            <div class="text-caption text-grey-7 q-ml-lg">Create and fill them in. Commits nothing.</div>
          </div>
          <div>
            <q-toggle v-model="form.canSend" label="Can send the intake form to the client" color="primary" />
            <!-- The reason the two are separate: leaving this off means nothing reaches a client until the
                 principal has looked at it. -->
            <div class="text-caption text-grey-7 q-ml-lg">
              Leave off and you see every request before the client does.
            </div>
          </div>
          <div class="row q-col-gutter-md">
            <app-date-field v-model="form.startsOn" label="From" class="col-6" />
            <app-date-field v-model="form.endsOn" label="Until" class="col-6" />
          </div>
          <div class="text-caption text-grey-7">Leave the dates empty for an open-ended delegation.</div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="editOpen = false" />
          <q-btn
            unelevated no-caps color="primary" label="Save" :loading="saving"
            :disable="!form.delegateUserId" @click="save"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-card>
</template>

<script setup>
// Self-service delegate management, for the shareholder or CSE's own profile. Concur's model: the
// principal names their own delegates, with granular rights rather than one blanket "act as me".
import { ref, reactive, onMounted } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppSelect from "components/common/AppSelect.vue";
import AppDateField from "components/common/AppDateField.vue";

const notify = useNotify();
const { confirm } = useConfirm();

const rows = ref([]);
const userOptions = ref([]);
const editOpen = ref(false);
const saving = ref(false);

const blank = () => ({
  id: null, delegateUserId: null, canPrepare: true, canSend: false, startsOn: "", endsOn: ""
});
const form = reactive(blank());

const load = async () => {
  rows.value = (await remsApi.myDelegates().catch(() => [])) || [];
};

const loadUsers = async () => {
  const admins = await remsApi.admins().catch(() => []);
  userOptions.value = (admins || []).map((a) => ({ label: a.name, value: a.id }));
};

const openNew = () => {
  Object.assign(form, blank());
  editOpen.value = true;
};

const openEdit = (d) => {
  Object.assign(form, {
    id: d.id,
    delegateUserId: d.delegateUserId,
    canPrepare: d.canPrepare,
    canSend: d.canSend,
    startsOn: d.startsOn || "",
    endsOn: d.endsOn || ""
  });
  editOpen.value = true;
};

const save = async () => {
  saving.value = true;
  try {
    await remsApi.saveDelegate({
      delegateUserId: form.delegateUserId,
      canPrepare: form.canPrepare,
      canSend: form.canSend,
      startsOn: form.startsOn || null,
      endsOn: form.endsOn || null
    });
    notify.success("Delegate saved.");
    editOpen.value = false;
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const remove = async (d) => {
  const ok = await confirm({
    title: "Withdraw delegation",
    message: `${d.delegateName} will no longer be able to work your REMS requests. Anything they already ` +
      "prepared stays as it is.",
    confirmLabel: "Withdraw",
    type: "danger"
  });
  if (!ok) return;
  try {
    await remsApi.removeDelegate(d.id);
    notify.success("Delegate withdrawn.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(() => {
  load();
  loadUsers();
});
</script>

<style scoped>
.mdp { border-radius: 12px; }
.mdp__dialog { width: 480px; max-width: 92vw; border-radius: 12px; }
</style>
