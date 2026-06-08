<template>
  <q-menu
    v-model="menuModel"
    anchor="bottom right"
    self="top right"
    no-parent-event
    style="width: 320px;"
  >
    <q-card class="q-pa-sm">

      <div class="text-subtitle2 q-mt-sm q-mb-sm">
        Manage Dropdown Options
      </div>

      <q-separator />

      <q-list style="min-width: 200px">

        <q-item
          v-for="opt in manageDropDownTypes"
          :key="opt.id"
          clickable
          :active="selectedField === opt.id"
          active-class="bg-primary text-white"
          @click="navigate(opt)"
        >
          <q-item-section>
            {{ opt.type }}
          </q-item-section>
        </q-item>

      </q-list>

    </q-card>
  </q-menu>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const props = defineProps({
  modelValue: Boolean,
  manageDropDownTypes: {
    type: Array,
    default: () => []
  },
  selectedField: {
    type: [String, Number],
    default: null
  }
});

const emit = defineEmits(["update:modelValue"]);

const menuModel = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

function navigate (opt) {
  router.push({
    path: "/manage-dropdowns",
    state: {
      id: opt.id,
      groupName: opt.groupName,
      moduleName: opt.moduleName
    }
  });
}
</script>
