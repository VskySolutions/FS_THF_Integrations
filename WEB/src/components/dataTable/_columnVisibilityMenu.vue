<template>
  <q-btn
    icon="o_display_settings"
    outline
    no-caps
    class="text-primary btnRounded q-ml-sm"
    size="md"
  >
    <q-menu>
      <q-list style="min-width: 230px">

        <q-item
          v-for="col in allColumnNames"
          :key="col.value"
          v-ripple
          clickable
          @click="toggle(col.value)"
        >
          <q-item-section avatar>

            <q-toggle
              :model-value="selectedColumnNames.includes(col.value)"
              size="xs"
              @update:model-value="toggle(col.value)"
              @click.stop
            />

          </q-item-section>

          <q-item-section>
            {{ col.label }}
          </q-item-section>

        </q-item>

        <q-separator />

        <q-item v-ripple clickable @click="$emit('selectAllColumns')">
          <q-item-section avatar>
            <q-icon name="o_select_all" />
          </q-item-section>
          <q-item-section>
            Select All Columns
          </q-item-section>
        </q-item>

        <q-item v-ripple clickable @click="$emit('defaultColumns')">
          <q-item-section avatar>
            <q-icon name="o_refresh" />
          </q-item-section>
          <q-item-section>
            Default Columns
          </q-item-section>
        </q-item>

      </q-list>
    </q-menu>

    <q-tooltip>
      Hide/Unhide columns
    </q-tooltip>

  </q-btn>
</template>

<script setup>

const props = defineProps({
  allColumnNames: Array,
  selectedColumnNames: Array
});

const emit = defineEmits([
  "update:selectedColumnNames",
  "selectAllColumns",
  "defaultColumns"
]);

function toggle (colValue) {
  const updated = [...props.selectedColumnNames];

  const index = updated.indexOf(colValue);

  if (index > -1) {
    updated.splice(index, 1);
  } else {
    updated.push(colValue);
  }

  emit("update:selectedColumnNames", updated);
}

</script>
