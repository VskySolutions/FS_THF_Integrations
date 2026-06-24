<template>
  <q-page padding>
    <div class="text-h6 q-mb-md">Modified Log Configuration</div>
    <p class="text-grey-7">
      System Tracked fields always record change history. Optional fields can be toggled off for this tenant.
    </p>

    <q-card flat bordered>
      <q-markup-table flat>
        <thead>
          <tr>
            <th class="text-left">Entity</th>
            <th class="text-left">Field</th>
            <th class="text-left">Type</th>
            <th class="text-center">Enabled</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in fields" :key="f.fieldKey">
            <td>{{ entityLabel(f.entityType) }}</td>
            <td>{{ f.displayName }}</td>
            <td>
              <q-badge :color="f.isSystemTracked ? 'indigo' : 'grey-6'" :label="f.isSystemTracked ? 'System' : 'Optional'" />
            </td>
            <td class="text-center">
              <q-toggle
                :model-value="f.isEnabled"
                :disable="f.isSystemTracked"
                @update:model-value="(val) => toggle(f, val)"
              />
            </td>
          </tr>
          <tr v-if="!fields.length">
            <td colspan="4" class="text-center text-grey-6 q-pa-md">No tracked fields registered.</td>
          </tr>
        </tbody>
      </q-markup-table>
    </q-card>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { ufModifiedLogApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useEntityMeta } from "composables/uf/useEntityMeta";

const notify = useNotify();
const { labelFor } = useEntityMeta();
const entityLabel = (t) => labelFor(t);

const fields = ref([]);

const load = async () => {
  try {
    fields.value = (await ufModifiedLogApi.config()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const toggle = async (field, value) => {
  try {
    const updated = await ufModifiedLogApi.toggleConfig(field.fieldKey, value);
    const idx = fields.value.findIndex((f) => f.fieldKey === field.fieldKey);
    if (idx >= 0 && updated) fields.value.splice(idx, 1, updated);
    notify.success("Updated.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    await load();
  }
};

onMounted(load);
</script>
