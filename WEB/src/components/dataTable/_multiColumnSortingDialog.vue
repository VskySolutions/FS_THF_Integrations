<template>
  <q-dialog v-model="dialogModel">
    <q-card style="width: 650px; max-width: 90vw;">

      <!-- Header -->
      <q-card-section>
        <div class="row items-center justify-between">
          <div class="text-h3">Multi-Column Level Sorting</div>
          <div class="row items-center q-gutter-sm">
            <q-btn
              color="primary"
              icon="o_add"
              label="Add Level"
              no-caps
              @click="$emit('add')"
            />
            <q-btn
              v-close-popup
              icon="o_close"
              flat
              round
              dense
              color="grey-8"
            >
              <q-tooltip>Close</q-tooltip>
            </q-btn>
          </div>
        </div>
      </q-card-section>
      <q-separator />
      <!-- Table -->
      <q-card-section>
        <q-table
          flat
          :rows="multiSort"
          hide-bottom
          virtual-scroll
          separator="cell"
          no-data-label="No sort levels added"
          class="edges-bordered-table"
          :rows-per-page-options="[20]"
        >
          <!-- Header -->
          <template #header="props">
            <q-tr :props="props" class="bg-primary text-white">
              <q-th key="column" class="text-start" width="45%">Column</q-th>
              <q-th key="direction" class="text-start" width="45%">Order</q-th>
              <q-th key="actions" width="10%" class="col-auto">Action</q-th>
            </q-tr>
          </template>
          <!-- Body -->
          <template #body="props">
            <q-tr :props="props">
              <!-- Column -->
              <q-td key="column">
                <q-select
                  v-model="props.row.column"
                  clearable
                  use-input
                  transition-show="jump-up"
                  transition-hide="jump-up"
                  hide-bottom-space
                  outlined
                  :dense="true"
                  input-debounce="0"
                  emit-value
                  map-options
                  option-value="value"
                  option-label="label"
                  :options="columnOptions(props.index)"
                />
              </q-td>
              <!-- Direction -->
              <q-td key="direction">
                <q-select
                  v-model="props.row.direction"
                  clearable
                  use-input
                  transition-show="jump-up"
                  transition-hide="jump-up"
                  hide-bottom-space
                  outlined
                  dense
                  emit-value
                  map-options
                  option-value="value"
                  option-label="label"
                  :options="directionOptions"
                />
              </q-td>
              <!-- Delete -->
              <q-td key="actions" class="text-center">
                <q-btn
                  flat
                  round
                  dense
                  icon="o_delete"
                  size="sm"
                  color="negative"
                  @click="$emit('remove', props.row)"
                >
                  <q-tooltip>Delete Level</q-tooltip>
                </q-btn>
              </q-td>
            </q-tr>
          </template>
        </q-table>
      </q-card-section>
      <q-separator />
      <!-- Footer -->
      <q-card-actions align="right">
        <q-btn
          v-close-popup
          label="Cancel"
          flat
          bordered
          color="grey-8"
        />
        <q-btn
          v-close-popup
          label="Apply Sort"
          color="primary"
          flat
          bordered
          @click="$emit('apply')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: Boolean,
  columns: Array,
  multiSort: Array,
  excludeColumns: Array
});

const emit = defineEmits([
  "update:modelValue",
  "add",
  "remove",
  "apply"
]);

const dialogModel = computed({
  get: () => props.modelValue,
  set: val => emit("update:modelValue", val)
});

const directionOptions = [
  { label: "Ascending", value: "asc" },
  { label: "Descending", value: "desc" }
];

function columnOptions (index) {
  return props.columns
    .filter(c => !props.excludeColumns?.includes(c.label))
    .map(c => ({
      label: c.label,
      value: c.field,
      disable: props.multiSort.some(
        (s, idx) => s.column === c.field && idx !== index
      )
    }));
}
</script>
