<template>
  <div class="uf-tags">
    <!-- Title with the add control right beside it -->
    <div class="row items-center no-wrap q-mb-xs">
      <q-icon name="o_label" color="primary" size="20px" class="q-mr-xs" />
      <div class="text-subtitle1">Tags</div>
      <q-btn flat dense round size="sm" icon="o_add" color="primary" class="q-ml-xs">
        <q-tooltip>Add tag</q-tooltip>
        <q-menu>
          <q-list style="min-width: 220px; max-height: 280px;" class="scroll">
            <q-item v-if="!available.length" dense>
              <q-item-section class="text-grey-6">No tags. Create some under Tenant Settings → Tag Management.</q-item-section>
            </q-item>
            <q-item
              v-for="tag in available"
              :key="tag.id"
              v-close-popup
              clickable
              dense
              @click="apply(tag)"
            >
              <q-item-section avatar>
                <q-icon name="o_label" :style="{ color: tag.colour || '#888' }" />
              </q-item-section>
              <q-item-section>
                <q-item-label>{{ tag.name }}</q-item-label>
                <q-item-label v-if="tag.category" caption>{{ tag.category }}</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-menu>
      </q-btn>
    </div>

    <!-- Body: applied tags, or a default message when none. -->
    <div class="row items-center q-gutter-xs">
      <template v-if="applied.length">
        <q-chip
          v-for="t in applied"
          :key="t.id"
          :style="chipStyle(t.colour)"
          removable
          dense
          text-color="white"
          @remove="remove(t)"
        >
          {{ t.tagName }}
        </q-chip>
      </template>
      <div v-else class="text-grey-6 fs-13">No tags added yet. Use the + button to tag this record.</div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { ufTagsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true }
});

const notify = useNotify();
const applied = ref([]);
const allTags = ref([]);

const appliedIds = computed(() => new Set(applied.value.map((t) => t.tagId)));
const available = computed(() => allTags.value.filter((t) => !appliedIds.value.has(t.id)));

const chipStyle = (colour) => ({ backgroundColor: colour || "#607d8b" });

const load = async () => {
  try {
    applied.value = (await ufTagsApi.entityTags(props.entityType, props.entityId)) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const loadCatalogue = async () => {
  try {
    allTags.value = (await ufTagsApi.picker()) || [];
  } catch {
    allTags.value = [];
  }
};

const apply = async (tag) => {
  try {
    await ufTagsApi.apply({ entityType: props.entityType, entityId: props.entityId, tagId: tag.id });
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const remove = async (entityTag) => {
  try {
    await ufTagsApi.removeApplication(entityTag.id);
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(async () => {
  await Promise.all([load(), loadCatalogue()]);
});
defineExpose({ load });
</script>
