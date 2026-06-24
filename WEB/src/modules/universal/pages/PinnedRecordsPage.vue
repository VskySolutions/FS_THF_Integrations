<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'My Pinned' }]"
      show-back
      @back="$router.back()"
    />

    <q-card flat bordered>
      <q-inner-loading :showing="loading && !pins.length" />
      <div v-if="!loading && !pins.length" class="text-grey-6 q-pa-lg text-center">
        <q-icon name="o_push_pin" size="32px" class="q-mb-sm" />
        <div>You haven't pinned any records yet.</div>
      </div>

      <q-list separator>
        <q-item v-for="p in pins" :key="p.id" clickable @click="openRecord(p)">
          <q-item-section avatar>
            <q-avatar color="blue-1" text-color="primary"><q-icon :name="iconFor(p.entityType)" /></q-avatar>
          </q-item-section>
          <q-item-section>
            <q-item-label>{{ labelFor(p.entityType) }}</q-item-label>
            <q-item-label caption>Pinned {{ formatDateTime(p.pinnedOnUtc) }}</q-item-label>
          </q-item-section>
          <q-item-section side>
            <q-btn flat round dense icon="o_push_pin" color="primary" @click.stop="unpin(p)">
              <q-tooltip>Unpin</q-tooltip>
            </q-btn>
          </q-item-section>
        </q-item>
      </q-list>

      <div v-if="totalRecords > pins.length" class="row justify-center q-pa-md">
        <q-btn flat no-caps color="primary" label="Load more" :loading="loading" @click="loadMore" />
      </div>
    </q-card>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { ufPinApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useEntityMeta } from "composables/uf/useEntityMeta";
import AppListHeader from "components/common/AppListHeader.vue";

const router = useRouter();
const notify = useNotify();
const { formatDateTime } = useDateFormat();
const { iconFor, labelFor, routeFor } = useEntityMeta();

const pins = ref([]);
const loading = ref(false);
const page = ref(1);
const totalRecords = ref(0);
const limit = 20;

const load = async (reset = true) => {
  loading.value = true;
  try {
    const res = await ufPinApi.list({ page: page.value, limit });
    const data = res?.data || [];
    pins.value = reset ? data : [...pins.value, ...data];
    totalRecords.value = res?.meta?.totalRecords || pins.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const loadMore = () => {
  page.value += 1;
  load(false);
};

const openRecord = (p) => router.push(routeFor(p.entityType, p.entityId));

const unpin = async (p) => {
  try {
    await ufPinApi.remove(p.id);
    pins.value = pins.value.filter((x) => x.id !== p.id);
    totalRecords.value = Math.max(0, totalRecords.value - 1);
    notify.success("Unpinned.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(() => load());
</script>
